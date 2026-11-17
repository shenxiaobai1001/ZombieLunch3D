using System.Collections.Generic;
using Game.CityMap;
using UnityEngine;

namespace Game.OcclusionLean
{
    /// <summary>
    /// 遮挡倾斜管理器（独立系统）：
    /// 相机到玩家视线被挡住时，让遮挡物向两侧倾斜露出侧面。
    /// 只驱动挂了 OcclusionLeanTarget 的物体，不依赖具体建筑类型。
    /// </summary>
    public sealed class OcclusionLeanManager : MonoBehaviour
    {
        public static OcclusionLeanManager Instance { get; private set; }

        static readonly List<OcclusionLeanTarget> s_Targets = new List<OcclusionLeanTarget>();

        [Header("引用")]
        public Transform Player;
        public Camera Cam;
        public string PlayerTag = "Player";

        [Header("倾斜")]
        [Tooltip("最大倾斜角度（度），可调且作为硬限制")]
        [Range(1f, 60f)]
        public float MaxLeanAngle = 28f;
        [Tooltip("未遮挡时的回正速度")]
        public float RecoverSpeed = 10f;
        [Tooltip("进入遮挡时的倾斜速度")]
        public float LeanSpeed = 12f;
        [Tooltip("角度死区，小于此值视为 0")]
        public float AngleEpsilon = 0.5f;

        [Header("检测")]
        public LayerMask OccluderMask = ~0;
        [Tooltip("射线起点相对相机的前向偏移，避免打到近裁剪")]
        public float RayStartOffset = 0.5f;
        [Tooltip("玩家胸口高度偏移")]
        public float PlayerAimHeight = 1.2f;
        public int MaxRayHits = 16;

        [Header("近距辅助（两楼中间也会微倾）")]
        public bool EnableProximityLean = true;
        public float ProximityRadius = 4f;
        [Range(0f, 1f)]
        public float ProximityLeanScale = 0.45f;

        [Header("自动挂载")]
        [Tooltip("射线命中且无 Target 时自动添加（换建筑模型后仍可用）")]
        public bool AutoAddTargetOnHit = true;
        [Tooltip("填充地块建筑时建议保持开启")]
        public bool AutoAddOnRegisterRequest = true;

        readonly RaycastHit[] _hits = new RaycastHit[32];
        readonly HashSet<OcclusionLeanTarget> _occluding = new HashSet<OcclusionLeanTarget>();
        readonly Dictionary<OcclusionLeanTarget, float> _desiredAngles = new Dictionary<OcclusionLeanTarget, float>();
        readonly Dictionary<OcclusionLeanTarget, Vector3> _desiredAxes = new Dictionary<OcclusionLeanTarget, Vector3>();
        readonly Collider[] _overlap = new Collider[64];

        public static void Register(OcclusionLeanTarget target)
        {
            if (target == null) return;
            if (!s_Targets.Contains(target)) s_Targets.Add(target);
        }

        public static void Unregister(OcclusionLeanTarget target)
        {
            s_Targets.Remove(target);
        }

        /// <summary>供生成器调用：给任意物体挂上可倾斜目标。</summary>
        public static OcclusionLeanTarget EnsureTarget(GameObject go)
        {
            if (go == null) return null;
            if (Instance != null && !Instance.AutoAddOnRegisterRequest)
                return go.GetComponent<OcclusionLeanTarget>();
            return OcclusionLeanTarget.EnsureOn(go);
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            ResolveRefs();
            if (Player == null || Cam == null) return;

            _occluding.Clear();
            _desiredAngles.Clear();
            _desiredAxes.Clear();

            EvaluateRayOcclusion();
            if (EnableProximityLean)
                EvaluateProximityLean();

            ApplyLeans();
        }

        void ResolveRefs()
        {
            if (Player == null)
            {
                var go = GameObject.FindGameObjectWithTag(PlayerTag);
                if (go != null) Player = go.transform;
            }
            if (Cam == null) Cam = Camera.main;
        }

        void EvaluateRayOcclusion()
        {
            Vector3 aim = Player.position + Vector3.up * PlayerAimHeight;
            Vector3 origin = Cam.transform.position + Cam.transform.forward * RayStartOffset;
            Vector3 toPlayer = aim - origin;
            float dist = toPlayer.magnitude;
            if (dist < 0.01f) return;

            Vector3 dir = toPlayer / dist;
            int count = Physics.RaycastNonAlloc(origin, dir, _hits, dist, OccluderMask, QueryTriggerInteraction.Ignore);

            Vector3 camRight = Vector3.ProjectOnPlane(Cam.transform.right, Vector3.up);
            if (camRight.sqrMagnitude < 0.0001f) camRight = Vector3.right;
            camRight.Normalize();

            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider == null) continue;
                if (hit.collider.transform == Player || hit.collider.transform.IsChildOf(Player))
                    continue;

                if (!IsLeanableOccluder(hit.collider)) continue;

                var target = hit.collider.GetComponentInParent<OcclusionLeanTarget>();
                if (target == null && AutoAddTargetOnHit)
                {
                    var hostGo = ResolveLeanHost(hit.collider);
                    if (hostGo != null)
                        target = OcclusionLeanTarget.EnsureOn(hostGo);
                }

                if (target == null || !target.EnableLean) continue;

                target.RefreshBounds();
                Vector3 center = target.WorldBounds.center;
                Vector3 away = Vector3.ProjectOnPlane(center - Player.position, Vector3.up);
                if (away.sqrMagnitude < 0.0001f)
                    away = Vector3.ProjectOnPlane(center - Cam.transform.position, Vector3.up);
                if (away.sqrMagnitude < 0.0001f) continue;
                away.Normalize();

                // 绕垂直于“远离玩家”方向的轴倾倒，屋顶让开视线
                Vector3 axis = Vector3.Cross(Vector3.up, away).normalized;
                if (axis.sqrMagnitude < 0.0001f)
                {
                    float side = Mathf.Sign(Vector3.Dot(away, camRight));
                    if (Mathf.Abs(side) < 0.01f) side = 1f;
                    axis = Vector3.Cross(Vector3.up, camRight * side).normalized;
                }

                float maxA = target.GetMaxAngle(MaxLeanAngle);
                // 用左右符号让两楼中间呈现向两侧分开
                float sideSign = Mathf.Sign(Vector3.Dot(away, camRight));
                if (Mathf.Abs(sideSign) < 0.01f) sideSign = 1f;

                float angle = maxA;
                // AngleAxis 方向：正角度绕 axis，使顶部倾向 away
                // Cross(up, away) + 正角度 ≈ 顶部移向 away
                SetDesired(target, axis, angle);
                _occluding.Add(target);
            }
        }

        void EvaluateProximityLean()
        {
            Vector3 pos = Player.position;
            int n = Physics.OverlapSphereNonAlloc(pos, ProximityRadius, _overlap, OccluderMask, QueryTriggerInteraction.Ignore);
            Vector3 camRight = Vector3.ProjectOnPlane(Cam.transform.right, Vector3.up).normalized;
            if (camRight.sqrMagnitude < 0.0001f) camRight = Vector3.right;

            for (int i = 0; i < n; i++)
            {
                var col = _overlap[i];
                if (col == null) continue;
                if (col.transform == Player || col.transform.IsChildOf(Player)) continue;

                if (!IsLeanableOccluder(col)) continue;

                var target = col.GetComponentInParent<OcclusionLeanTarget>();
                if (target == null) continue;
                if (!target.EnableLean) continue;
                if (_occluding.Contains(target)) continue; // 已有遮挡倾角，不再削弱

                target.RefreshBounds();
                Vector3 center = target.WorldBounds.center;
                Vector3 away = Vector3.ProjectOnPlane(center - pos, Vector3.up);
                float dist = away.magnitude;
                if (dist < 0.01f) continue;
                away /= dist;

                float t = 1f - Mathf.Clamp01(dist / ProximityRadius);
                float maxA = target.GetMaxAngle(MaxLeanAngle) * ProximityLeanScale * t;
                if (maxA < AngleEpsilon) continue;

                Vector3 axis = Vector3.Cross(Vector3.up, away).normalized;
                if (axis.sqrMagnitude < 0.0001f) continue;
                SetDesired(target, axis, maxA);
            }
        }

        static bool IsLeanableOccluder(Collider col)
        {
            if (col == null) return false;

            var marker = col.GetComponentInParent<CityBlockMarker>();
            if (marker != null)
            {
                if (marker.Kind == CityBlockKind.LotPad
                    || marker.Kind == CityBlockKind.Road
                    || marker.Kind == CityBlockKind.Corridor
                    || marker.Kind == CityBlockKind.OpenFacility)
                    return false;
            }

            // 有建筑变体，或显式 Target，或普通网格（非地面薄片）
            if (col.GetComponentInParent<CityBuildingVariant>() != null) return true;
            if (col.GetComponentInParent<OcclusionLeanTarget>() != null) return true;

            var box = col as BoxCollider;
            if (box != null)
            {
                var size = Vector3.Scale(box.size, col.transform.lossyScale);
                if (size.y < 1.5f) return false; // 过矮：地砖/马路
            }
            return col.GetComponentInParent<MeshRenderer>() != null;
        }

        static GameObject ResolveLeanHost(Collider col)
        {
            var variant = col.GetComponentInParent<CityBuildingVariant>();
            if (variant != null) return variant.gameObject;
            var mr = col.GetComponentInParent<MeshRenderer>();
            if (mr != null) return mr.gameObject;
            return col.gameObject;
        }

        void SetDesired(OcclusionLeanTarget target, Vector3 axis, float angle)
        {
            if (_desiredAngles.TryGetValue(target, out float existing))
            {
                // 取较大倾角
                if (Mathf.Abs(angle) >= Mathf.Abs(existing))
                {
                    _desiredAngles[target] = angle;
                    _desiredAxes[target] = axis;
                }
            }
            else
            {
                _desiredAngles[target] = angle;
                _desiredAxes[target] = axis;
            }
        }

        void ApplyLeans()
        {
            for (int i = s_Targets.Count - 1; i >= 0; i--)
            {
                var target = s_Targets[i];
                if (target == null)
                {
                    s_Targets.RemoveAt(i);
                    continue;
                }

                float maxA = target.GetMaxAngle(MaxLeanAngle);
                if (_desiredAngles.TryGetValue(target, out float angle))
                {
                    var axis = _desiredAxes[target];
                    target.SetLean(axis, angle, LeanSpeed, maxA);
                }
                else
                {
                    target.SetLean(Vector3.right, 0f, RecoverSpeed, maxA);
                }
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (Player == null || Cam == null) return;
            Gizmos.color = Color.yellow;
            Vector3 aim = Player.position + Vector3.up * PlayerAimHeight;
            Gizmos.DrawLine(Cam.transform.position, aim);
            if (EnableProximityLean)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
                Gizmos.DrawWireSphere(Player.position, ProximityRadius);
            }
        }
#endif
    }
}

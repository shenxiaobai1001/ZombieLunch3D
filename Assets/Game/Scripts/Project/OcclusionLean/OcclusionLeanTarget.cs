using UnityEngine;

namespace Game.OcclusionLean
{
    /// <summary>
    /// 可被遮挡倾斜系统驱动的目标。挂在任意遮挡物上即可，与建筑生成逻辑解耦。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OcclusionLeanTarget : MonoBehaviour
    {
        [Tooltip("倾斜作用的节点；为空则自动创建 LeanPivot 并把子物体移入")]
        public Transform LeanPivot;

        [Tooltip("覆盖管理器最大倾角；小于 0 则使用管理器默认")]
        public float MaxAngleOverride = -1f;

        [Tooltip("关闭后管理器不会倾斜此物体")]
        public bool EnableLean = true;

        Quaternion _restLocalRotation;
        Vector3 _restLocalPosition;
        float _currentAngle;
        Vector3 _currentAxis = Vector3.right;
        bool _initialized;

        public float CurrentAngle => _currentAngle;
        public Bounds WorldBounds { get; private set; }

        void Awake()
        {
            EnsurePivot();
            CacheRest();
            RefreshBounds();
        }

        void OnEnable()
        {
            OcclusionLeanManager.Register(this);
        }

        void OnDisable()
        {
            OcclusionLeanManager.Unregister(this);
            ResetLeanImmediate();
        }

        public void EnsurePivot()
        {
            if (LeanPivot != null) return;

            // 单体网格：直接以自身为倾斜节点（占位方体、简单建筑都适用）
            bool hasOwnVisual = GetComponent<MeshRenderer>() != null || GetComponent<MeshFilter>() != null;
            if (hasOwnVisual && transform.childCount == 0)
            {
                LeanPivot = transform;
                return;
            }

            var existing = transform.Find("LeanPivot");
            if (existing != null)
            {
                LeanPivot = existing;
                return;
            }

            var pivot = new GameObject("LeanPivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = Vector3.zero;
            pivot.transform.localRotation = Quaternion.identity;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == pivot.transform) continue;
                child.SetParent(pivot.transform, true);
            }

            LeanPivot = pivot.transform;

            RefreshBounds();
            var localBottom = transform.InverseTransformPoint(new Vector3(WorldBounds.center.x, WorldBounds.min.y, WorldBounds.center.z));
            LeanPivot.localPosition = localBottom;
            for (int i = 0; i < LeanPivot.childCount; i++)
                LeanPivot.GetChild(i).localPosition -= localBottom;
        }

        void CacheRest()
        {
            if (LeanPivot == null) return;
            _restLocalRotation = LeanPivot.localRotation;
            _restLocalPosition = LeanPivot.localPosition;
            _initialized = true;
        }

        public void RefreshBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                WorldBounds = new Bounds(transform.position, Vector3.one * 2f);
                return;
            }

            WorldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                WorldBounds.Encapsulate(renderers[i].bounds);
        }

        public float GetMaxAngle(float managerDefault)
        {
            return MaxAngleOverride >= 0f ? MaxAngleOverride : managerDefault;
        }

        /// <summary>由管理器调用：世界轴向倾斜，角度有符号。</summary>
        public void SetLean(Vector3 worldAxis, float targetAngle, float lerpSpeed, float maxAbsAngle)
        {
            if (!EnableLean || LeanPivot == null) return;
            if (!_initialized) CacheRest();

            if (worldAxis.sqrMagnitude < 0.0001f)
                worldAxis = _currentAxis;
            else
                _currentAxis = worldAxis.normalized;

            targetAngle = Mathf.Clamp(targetAngle, -maxAbsAngle, maxAbsAngle);
            _currentAngle = Mathf.Lerp(_currentAngle, targetAngle, 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime));

            if (Mathf.Abs(_currentAngle) < 0.01f)
            {
                LeanPivot.localRotation = _restLocalRotation;
                return;
            }

            var worldRot = Quaternion.AngleAxis(_currentAngle, _currentAxis);
            // 保持 pivot 世界位置，只改旋转：用 rest 的父空间姿态叠加
            var restWorld = transform.rotation * _restLocalRotation;
            var desiredWorld = worldRot * restWorld;
            LeanPivot.rotation = desiredWorld;
        }

        public void ResetLeanImmediate()
        {
            _currentAngle = 0f;
            if (LeanPivot != null && _initialized)
            {
                LeanPivot.localRotation = _restLocalRotation;
                LeanPivot.localPosition = _restLocalPosition;
            }
        }

        /// <summary>在物体上确保存在 Target（建筑换皮后只要还挂这个就能用）。</summary>
        public static OcclusionLeanTarget EnsureOn(GameObject go)
        {
            if (go == null) return null;
            var t = go.GetComponent<OcclusionLeanTarget>();
            if (t == null) t = go.AddComponent<OcclusionLeanTarget>();
            t.EnsurePivot();
            return t;
        }
    }
}

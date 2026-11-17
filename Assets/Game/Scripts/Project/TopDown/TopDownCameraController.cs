using Cinemachine;
using DiasGames.ThirdPersonSystem;
using UnityEngine;

namespace Game.TopDown
{
    /// <summary>
    /// 俯视角相机：锁定世界朝向（不歪、不滚转），平滑跟随角色根节点，减轻物理移动抖动。
    /// </summary>
    public sealed class TopDownCameraController : MonoBehaviour
    {
        [Header("目标")]
        public Transform FollowTarget;
        public string PlayerTag = "Player";
        [Tooltip("俯视跟角色根节点更稳；不要跟会抖动的 Camera Track Pos")]
        public bool PreferPlayerRoot = true;
        public string TrackChildName = "Camera Track Pos";

        [Header("俯视参数")]
        public float Height = 16f;
        [Tooltip("相对竖直向下的前倾角（度）。0=完全正俯视，画面最正")]
        [Range(0f, 45f)]
        public float PitchFromVertical = 0f;
        [Tooltip("世界 yaw，0 表示屏幕上方=+Z")]
        public float WorldYaw = 0f;
        [Tooltip("跟随平滑时间，越大越稳、略有延迟")]
        public float PositionSmoothTime = 0.08f;
        public int CameraPriority = 80;

        [Header("输入")]
        public bool EnableTopDownMove = true;

        CinemachineVirtualCamera _vcam;
        CinemachineStateDrivenCamera _stateDriven;
        Transform _player;
        Transform _smoothFollow;
        Vector3 _smoothVelocity;

        void Start()
        {
            EnsureSmoothFollow();
            ResolvePlayer();
            SetupVirtualCamera();
            ApplyInputMode(true);
            ApplyLockedRotation();
        }

        void OnDisable()
        {
            ApplyInputMode(false);
        }

        void FixedUpdate()
        {
            // 角色用 Rigidbody 时，在 FixedUpdate 采样位置更稳
            UpdateSmoothFollow(Time.fixedDeltaTime);
        }

        void LateUpdate()
        {
            if (_player == null) ResolvePlayer();
            // 再补一帧插值，对齐渲染
            UpdateSmoothFollow(Time.deltaTime);
            ApplyLockedRotation();

            if (_vcam != null && _smoothFollow != null)
            {
                if (_vcam.Follow != _smoothFollow) _vcam.Follow = _smoothFollow;
                _vcam.LookAt = null; // 禁止 LookAt，避免歪斜/滚转
            }
        }

        void EnsureSmoothFollow()
        {
            if (_smoothFollow != null) return;
            var go = new GameObject("TopDownSmoothFollow");
            go.transform.SetParent(transform, false);
            _smoothFollow = go.transform;
        }

        void ResolvePlayer()
        {
            if (FollowTarget != null)
            {
                _player = FollowTarget;
                SnapSmoothToPlayer();
                return;
            }

            var playerGo = GameObject.FindGameObjectWithTag(PlayerTag);
            if (playerGo == null) return;

            if (PreferPlayerRoot)
                _player = playerGo.transform;
            else
            {
                var track = playerGo.transform.Find(TrackChildName);
                _player = track != null ? track : playerGo.transform;
            }

            SnapSmoothToPlayer();
        }

        void SnapSmoothToPlayer()
        {
            if (_player == null || _smoothFollow == null) return;
            _smoothFollow.position = FlatPosition(_player.position);
            _smoothVelocity = Vector3.zero;
        }

        static Vector3 FlatPosition(Vector3 p) => new Vector3(p.x, p.y, p.z);

        void UpdateSmoothFollow(float dt)
        {
            if (_player == null || _smoothFollow == null) return;
            var target = FlatPosition(_player.position);
            // 忽略动画带来的微小 Y 抖动：高度跟角色，但用平滑
            _smoothFollow.position = Vector3.SmoothDamp(
                _smoothFollow.position,
                target,
                ref _smoothVelocity,
                Mathf.Max(0.01f, PositionSmoothTime),
                Mathf.Infinity,
                dt);
        }

        void ApplyLockedRotation()
        {
            if (_vcam == null) return;
            // 固定世界朝向：无滚转。PitchFromVertical=0 → 镜头竖直向下，地平线水平。
            float pitch = 90f - PitchFromVertical;
            _vcam.transform.rotation = Quaternion.Euler(pitch, WorldYaw, 0f);
        }

        void SetupVirtualCamera()
        {
            if (Camera.main != null)
            {
                var brain = Camera.main.GetComponent<CinemachineBrain>();
                if (brain == null) brain = Camera.main.gameObject.AddComponent<CinemachineBrain>();
                // 与平滑代理配合，用 LateUpdate 输出最终画面
                brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.LateUpdate;
                brain.m_BlendUpdateMethod = CinemachineBrain.BrainUpdateMethod.LateUpdate;
            }

            // 彻底让第三人称相机让路，避免抢镜导致抖/歪
            _stateDriven = FindObjectOfType<CinemachineStateDrivenCamera>();
            if (_stateDriven != null)
            {
                _stateDriven.Priority = 0;
                _stateDriven.enabled = false;
            }

            var freeLooks = FindObjectsOfType<CinemachineFreeLook>();
            for (int i = 0; i < freeLooks.Length; i++)
            {
                freeLooks[i].Priority = 0;
                freeLooks[i].enabled = false;
            }

            var existing = transform.Find("TopDownVCam");
            GameObject vcamGo = existing != null
                ? existing.gameObject
                : new GameObject("TopDownVCam");
            if (existing == null) vcamGo.transform.SetParent(transform, false);

            _vcam = vcamGo.GetComponent<CinemachineVirtualCamera>();
            if (_vcam == null) _vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();

            _vcam.Priority = CameraPriority;
            _vcam.Follow = _smoothFollow;
            _vcam.LookAt = null;

            // 去掉会扭头的 Aim（HardLookAt 是歪斜主因）
            var oldAim = _vcam.GetCinemachineComponent(CinemachineCore.Stage.Aim);
            if (oldAim != null)
            {
                if (Application.isPlaying) Destroy(oldAim);
                else DestroyImmediate(oldAim);
            }

            var transposer = _vcam.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer == null)
                transposer = _vcam.AddCinemachineComponent<CinemachineTransposer>();

            // 位置只抬高；倾斜完全由锁定旋转控制，避免偏移+LookAt 组合出斜画面
            transposer.m_FollowOffset = new Vector3(0f, Height, 0f);
            transposer.m_XDamping = 0f;
            transposer.m_YDamping = 0f;
            transposer.m_ZDamping = 0f;
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;

            // 关掉可能存在的噪声
            var noise = _vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
            if (noise != null)
            {
                if (Application.isPlaying) Destroy(noise);
                else DestroyImmediate(noise);
            }

            ApplyLockedRotation();
        }

        void ApplyInputMode(bool enableTopDown)
        {
            var players = FindObjectsOfType<UnityInputManager>();
            for (int i = 0; i < players.Length; i++)
            {
                if (EnableTopDownMove && enableTopDown)
                    players[i].TopDownScreenRelative = true;
                else if (!enableTopDown)
                    players[i].TopDownScreenRelative = false;
            }
        }

        [ContextMenu("Reapply Top Down Setup")]
        public void Reapply()
        {
            EnsureSmoothFollow();
            ResolvePlayer();
            SetupVirtualCamera();
            ApplyInputMode(true);
            ApplyLockedRotation();
        }

        void OnValidate()
        {
            if (_vcam != null)
                ApplyLockedRotation();
        }
    }
}

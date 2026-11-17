using Cinemachine;
using DiasGames.ThirdPersonSystem;
using UnityEngine;

namespace Game.TopDown
{
    /// <summary>
    /// 俯视角相机：高空跟随玩家，并开启 WASD 屏幕相对移动。
    /// 与具体建筑/地块系统无关。
    /// </summary>
    public sealed class TopDownCameraController : MonoBehaviour
    {
        [Header("目标")]
        public Transform FollowTarget;
        public string PlayerTag = "Player";
        public string TrackChildName = "Camera Track Pos";

        [Header("俯视参数")]
        public float Height = 16f;
        [Tooltip("略向前倾一点，便于看到建筑侧面；0 为完全垂直")]
        public float PitchOffset = 8f;
        public float FollowDamping = 0.15f;
        public int CameraPriority = 50;

        [Header("输入")]
        public bool EnableTopDownMove = true;
        public bool ShowCursor = true;

        CinemachineVirtualCamera _vcam;
        CinemachineStateDrivenCamera _stateDriven;
        Transform _follow;

        void Start()
        {
            ResolveFollow();
            SetupVirtualCamera();
            ApplyInputMode(true);
        }

        void OnDisable()
        {
            ApplyInputMode(false);
        }

        void LateUpdate()
        {
            if (_follow == null) ResolveFollow();
            if (_vcam != null && _follow != null)
            {
                if (_vcam.Follow != _follow) _vcam.Follow = _follow;
                if (_vcam.LookAt != _follow) _vcam.LookAt = _follow;
            }
        }

        void ResolveFollow()
        {
            if (FollowTarget != null)
            {
                _follow = FollowTarget;
                return;
            }

            var player = GameObject.FindGameObjectWithTag(PlayerTag);
            if (player == null) return;

            var track = player.transform.Find(TrackChildName);
            _follow = track != null ? track : player.transform;
            FollowTarget = _follow;
        }

        void SetupVirtualCamera()
        {
            if (Camera.main != null && Camera.main.GetComponent<CinemachineBrain>() == null)
                Camera.main.gameObject.AddComponent<CinemachineBrain>();

            // 降低第三人称 StateDriven 优先级，避免抢镜
            _stateDriven = FindObjectOfType<CinemachineStateDrivenCamera>();
            if (_stateDriven != null)
                _stateDriven.Priority = Mathf.Min(_stateDriven.Priority, CameraPriority - 10);

            var existing = transform.Find("TopDownVCam");
            GameObject vcamGo;
            if (existing != null) vcamGo = existing.gameObject;
            else
            {
                vcamGo = new GameObject("TopDownVCam");
                vcamGo.transform.SetParent(transform, false);
            }

            _vcam = vcamGo.GetComponent<CinemachineVirtualCamera>();
            if (_vcam == null) _vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();

            _vcam.Priority = CameraPriority;
            if (_follow != null)
            {
                _vcam.Follow = _follow;
                _vcam.LookAt = _follow;
            }

            var transposer = _vcam.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer == null)
                transposer = _vcam.AddCinemachineComponent<CinemachineTransposer>();

            // 相机在角色上方，略偏后形成轻微俯斜
            float back = Mathf.Tan(PitchOffset * Mathf.Deg2Rad) * Height;
            transposer.m_FollowOffset = new Vector3(0f, Height, -back);
            transposer.m_XDamping = FollowDamping;
            transposer.m_YDamping = FollowDamping;
            transposer.m_ZDamping = FollowDamping;
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;

            var look = _vcam.GetCinemachineComponent<CinemachineHardLookAt>();
            if (look == null)
                _vcam.AddCinemachineComponent<CinemachineHardLookAt>();
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
            ResolveFollow();
            SetupVirtualCamera();
            ApplyInputMode(true);
        }
    }
}

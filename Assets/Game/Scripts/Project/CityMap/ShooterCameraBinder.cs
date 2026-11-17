using Cinemachine;
using UnityEngine;

namespace Game.CityMap
{
    /// <summary>
    /// 把 Dias Shooter 的 Cinemachine StateDriven 相机绑到角色的 Camera Track Pos。
    /// 挂在场景任意物体上即可（例如和 CityLotManager 同物体）。
    /// </summary>
    public sealed class ShooterCameraBinder : MonoBehaviour
    {
        [Tooltip("留空则找 Tag=Player")]
        public Transform Player;

        [Tooltip("留空则找场景里的 CinemachineStateDrivenCamera")]
        public CinemachineStateDrivenCamera StateDrivenCamera;

        public string TrackChildName = "Camera Track Pos";
        public bool BindOnStart = true;
        public bool RebindIfLost = true;

        void Start()
        {
            if (BindOnStart) Bind();
        }

        void LateUpdate()
        {
            if (!RebindIfLost) return;
            if (StateDrivenCamera == null) return;
            if (StateDrivenCamera.Follow == null || StateDrivenCamera.LookAt == null)
                Bind();
        }

        [ContextMenu("Bind Camera Now")]
        public void Bind()
        {
            if (Player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) Player = go.transform;
            }

            if (Player == null)
            {
                Debug.LogWarning("[ShooterCameraBinder] 找不到 Player（需要 Tag=Player）");
                return;
            }

            if (StateDrivenCamera == null)
                StateDrivenCamera = FindObjectOfType<CinemachineStateDrivenCamera>();

            if (StateDrivenCamera == null)
            {
                Debug.LogWarning("[ShooterCameraBinder] 场景里没有 Shooter Camera State Driven，请先用菜单 Dias Games/Shooter System/Setup Scene for Shooter → Setup the Camera");
                return;
            }

            var track = Player.Find(TrackChildName);
            if (track == null)
            {
                // 兜底：跟角色根节点
                track = Player;
                Debug.LogWarning($"[ShooterCameraBinder] 角色下没有「{TrackChildName}」，临时跟随角色根节点");
            }

            StateDrivenCamera.Follow = track;
            StateDrivenCamera.LookAt = track;

            var animator = Player.GetComponent<Animator>();
            if (animator != null)
                StateDrivenCamera.m_AnimatedTarget = animator;

            // 主相机需要 CinemachineBrain
            if (Camera.main != null && Camera.main.GetComponent<CinemachineBrain>() == null)
                Camera.main.gameObject.AddComponent<CinemachineBrain>();

            Debug.Log($"[ShooterCameraBinder] 已绑定相机跟随 → {track.name}");
        }
    }
}

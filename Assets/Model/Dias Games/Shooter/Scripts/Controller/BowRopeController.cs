using UnityEngine;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    public class BowRopeController : MonoBehaviour
    {
        // Hand Reference
        [SerializeField] private Transform m_RopeBone;
        [SerializeField] private Transform m_RopeSocket;

        private bool m_RopeOnHand = false;
        private WeaponController m_WeaponController;

        private Vector3 m_OriginalPos = Vector3.zero;
        private bool returnRope = false;

        private void Awake()
        {
            m_WeaponController = GetComponent<WeaponController>();
            m_OriginalPos = m_RopeBone.localPosition;

            GlobalEvents.AddEvent("SetRopeOnHand", EnableRope);
        }

        private void OnEnable()
        {
            if (m_WeaponController == null)
                return;

            m_WeaponController.OnStartReload += DisableRope;
        }
        private void OnDisable()
        {
            if (m_WeaponController == null)
                return;

            m_WeaponController.OnStartReload -= DisableRope;
        }

        private void FixedUpdate()
        {
            if (m_WeaponController == null)
                return;

            if (m_RopeOnHand)
            {
                if (!returnRope)
                    m_RopeBone.position = m_RopeSocket.position;

                returnRope = false;
            }
            else
            {
                if (!returnRope)
                {
                    returnRope = true;
                    return;
                }
                m_RopeBone.localPosition = Vector3.Lerp(m_RopeBone.localPosition, m_OriginalPos, Time.deltaTime / 0.025f);
            }
            
            switch (m_WeaponController.State)
            {
                case WeaponState.Holst:
                    m_RopeOnHand = false;
                    break;
                case WeaponState.Equipping:
                    m_RopeOnHand = false;
                    break;
                case WeaponState.Unequipping:
                    m_RopeOnHand = false;
                    break;
                case WeaponState.Idle:
                    m_RopeOnHand = false;
                    break;
                case WeaponState.Aiming:
                    m_RopeOnHand = m_WeaponController.LoadedBullets > 0;
                    break;
                case WeaponState.Firing:
                    m_RopeOnHand = false;
                    break;
                case WeaponState.Reloading:
                    break;
                case WeaponState.Holding:
                    m_RopeOnHand = true;
                    break;
                default:
                    break;
            }
        }

        void DisableRope()
        {
            m_RopeOnHand = false;
        }

        public void EnableRope(GameObject obj, object parameter)
        {
            if (m_WeaponController.m_System.gameObject == obj)
                m_RopeOnHand = true;
        }
    }
}
using UnityEngine;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    public class PickupItemAbility : ThirdPersonAbility
    {
        private PickableItem m_ItemCloser = null;          // Reference to item triggered by player

        public override bool TryEnterAbility()
        {
            if (m_ItemCloser != null)
                return true;

            return base.TryEnterAbility();
        }

        /// <summary>
        /// Get item after finish animation
        /// </summary>
        public override void OnExitAbility()
        {
            base.OnExitAbility();

            if (m_ItemCloser == null)
                return;

            m_ItemCloser.Pick();
            m_ItemCloser = null;
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.tag == "Item")
            {
                if (m_ItemCloser == null)
                    m_ItemCloser = other.GetComponent<PickableItem>();                
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.tag == "Item")
                m_ItemCloser = null;
        }

        private void Reset()
        {
            m_EnterState = "Pick Item";
            m_FinishOnAnimationEnd = true;

            m_UseInputStateToEnter = InputEnterType.ButtonDown;
            InputButton = InputReference.Interact;
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    public class CharacterShooterController : Modifier
    {
        [SerializeField] private InventoryData m_Inventory = null;

        // Conditions that allow character equip and unequip weapon
        [SerializeField] private bool m_UseWeaponOnlyGrounded = true;
        [SerializeField] private List<ThirdPersonAbility> m_AbilitiesAllowed = new List<ThirdPersonAbility>();

        // Components
        private UnityInputManager m_InputManager;

        // Private vars
        private List<WeaponController> m_WeaponsControllers = new List<WeaponController>();

        private WeaponController m_SelectedWeapon = null;       // Current selected weapon controller reference
        public WeaponController SelectedWeapon { get { return m_SelectedWeapon; } }

        private bool IsChangingWeapon = false;                  // Control if character is changing weapon or not inside coroutine

        public override void Initialize(ThirdPersonSystem system)
        {
            base.Initialize(system);

            m_InputManager = m_System.InputManager;
            m_WeaponsControllers.AddRange(GetComponentsInChildren<WeaponController>());

            foreach(WeaponController controller in m_WeaponsControllers)
            {
                controller.OnEquip += BlockAbilities;
                controller.OnUnequip += UnblockAbilities;
            }

            m_SelectedWeapon =  SelectNextWeapon();
        }

        private void OnDisable()
        {
            foreach (WeaponController controller in m_WeaponsControllers)
            {
                controller.OnEquip -= BlockAbilities;
                controller.OnUnequip -= UnblockAbilities;
            }
        }

        public override void UpdateModifier()
        {
            if (m_Inventory == null)
                return;

            if (Mathf.Approximately(Time.timeScale, 0) || IsChangingWeapon)
                return;

            if (m_UseWeaponOnlyGrounded && !m_System.IsGrounded)
                return;

            if (!m_AbilitiesAllowed.Contains(m_System.ActiveAbility))
                return;

            if (m_InputManager.toggleWeaponButton.WasPressed)
                ToggleWeapon();

            if (m_InputManager.rightWeaponButton.WasPressed)
            {
                if (m_Inventory.Weapons.Count < 2 || m_WeaponsControllers.Count < 2)
                    ToggleWeapon();
                else
                    StartCoroutine(ChangeWeapon(SelectNextWeapon()));
            }

            if (m_InputManager.leftWeaponButton.WasPressed)
            {
                if (m_Inventory.Weapons.Count < 2 || m_WeaponsControllers.Count < 2)
                    ToggleWeapon();
                else
                    StartCoroutine(ChangeWeapon(SelectPreviousWeapon()));
            }

        }

        public override void FixedUpdateModifier()
        {
            base.FixedUpdateModifier();
        }

        private void ToggleWeapon()
        {
            if (m_SelectedWeapon == null)
            {
                // Try to equip a new weapon
                StartCoroutine(ChangeWeapon(SelectNextWeapon()));
                return;
            }
            
            if(m_SelectedWeapon.State == WeaponState.Holst)
            {
                // Equip
                m_SelectedWeapon.Equip();
            }
            else
            {
                // Unequip
                m_SelectedWeapon.Unequip();
            }
        }

        private IEnumerator ChangeWeapon(WeaponController newWeapon)
        {
            IsChangingWeapon = true;

            if (newWeapon != null)
            {
                if (m_SelectedWeapon == null)
                {
                    m_SelectedWeapon = newWeapon;
                    m_SelectedWeapon.Equip();
                }
                else
                {
                    if (m_SelectedWeapon.State != WeaponState.Holst)
                    {
                        m_SelectedWeapon.Unequip();
                        while (m_SelectedWeapon.State != WeaponState.Holst)
                            yield return null;
                    }

                    m_SelectedWeapon = newWeapon;
                    m_SelectedWeapon.Equip();
                }
            }

            IsChangingWeapon = false;
        }

        private WeaponController SelectNextWeapon()
        {
            if (m_WeaponsControllers.Count == 0)
                return null;

            int index = (m_SelectedWeapon == null) ? -1 : m_WeaponsControllers.FindIndex(x => x == m_SelectedWeapon);

            for(int i=0; i < m_WeaponsControllers.Count; i++)
            {
                int finalIndex = (i + index + 1) % m_WeaponsControllers.Count;
                if (m_Inventory.Weapons.Contains(m_WeaponsControllers[finalIndex].WeaponData))
                    return m_WeaponsControllers[finalIndex];
                
            }

            return null;
        }

        private WeaponController SelectPreviousWeapon()
        {
            if (m_WeaponsControllers.Count == 0)
                return null;

            int index = (m_SelectedWeapon == null) ? m_WeaponsControllers.Count : m_WeaponsControllers.FindIndex(x => x == m_SelectedWeapon);

            for (int i = 0; i < m_WeaponsControllers.Count; i--)
            {
                index = (index < 1) ? m_WeaponsControllers.Count - 1 : index-1;
                if (m_Inventory.Weapons.Contains(m_WeaponsControllers[index].WeaponData))
                    return m_WeaponsControllers[index];

            }

            return null;
        }

        public void BlockAbilities()
        {
            foreach(ThirdPersonAbility ability in m_System.CharacterAbilities)
            {
                if(!m_AbilitiesAllowed.Contains(ability))
                    ability.BlockAbility();
            } 
        }

        public void UnblockAbilities()
        {
            foreach (ThirdPersonAbility ability in m_System.CharacterAbilities)
                    ability.UnblockAbility();
        }

        public void OnHandWeapon()
        {
            m_SelectedWeapon.TakeWeaponFromHolst();
        }
    }
}
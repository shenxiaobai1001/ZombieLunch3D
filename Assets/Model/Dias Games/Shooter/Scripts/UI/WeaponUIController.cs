using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    public class WeaponUIController : MonoBehaviour
    {
        private CanvasGroup canvasGroup;

        [SerializeField] private GameObject m_TargetCharacter;
        [SerializeField] private Image weaponSprite;

        [SerializeField] private Text m_TotalAmountField;
        [SerializeField] private Text m_LoadedAmountField;

        private CharacterShooterController shooterController;
        [SerializeField] private InventoryData m_Inventory;

        private float alpha = 0;
        private float alphaSpeed;
        
        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (m_TargetCharacter == null)
                m_TargetCharacter = GameObject.FindGameObjectWithTag("Player");         // Find a Game Object with tag player

            shooterController = m_TargetCharacter.GetComponent<CharacterShooterController>();
        }

        private void FixedUpdate()
        {
            canvasGroup.alpha = Mathf.SmoothDamp(canvasGroup.alpha, alpha, ref alphaSpeed, 0.1f);

            if (shooterController.SelectedWeapon != null)
            {
                if (shooterController.SelectedWeapon.State != WeaponState.Holst)
                {
                    alpha = 1f;
                    weaponSprite.sprite = shooterController.SelectedWeapon.WeaponData.ItemSprite;

                    m_LoadedAmountField.text = shooterController.SelectedWeapon.LoadedBullets.ToString();

                    if (shooterController.SelectedWeapon.HasInfinityBullets)
                        m_TotalAmountField.text = "∞";
                    else
                    {
                        BulletItem bullet = m_Inventory.Bullets.Find(x => x.Bullet == shooterController.SelectedWeapon.WeaponData.Bullets);
                        int amount = 0;
                        if(bullet != null)
                            amount = bullet.Amount - shooterController.SelectedWeapon.LoadedBullets;

                        m_TotalAmountField.text = amount.ToString();
                    }

                    return;
                }
            }

            alpha = 0f;
        }
    }
}
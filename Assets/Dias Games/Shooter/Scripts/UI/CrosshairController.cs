using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DiasGames.ThirdPersonSystem.Shooter {

    public class CrosshairController : MonoBehaviour {

        [SerializeField] private GameObject m_TargetCharacter;

        [SerializeField] private Image m_Center, m_Top, m_Bottom, m_Right, m_Left;

        private CharacterShooterController shooterController;
        private ThirdPersonSystem characterEngine;

        private float Recoil = 0;
        private float recoilReturnSpeed;

        private IEnumerator Start()
        {
            if (m_TargetCharacter == null)
                m_TargetCharacter = GameObject.FindGameObjectWithTag("Player");         // Find a Game Object with tag player

            shooterController = m_TargetCharacter.GetComponent<CharacterShooterController>();
            characterEngine = m_TargetCharacter.GetComponent<ThirdPersonSystem>();

            SetCrosshairsVisibility(false);
            GlobalEvents.AddEvent("AddRecoil", GetWeaponRecoil);

            yield return null;
        }


        private void FixedUpdate()
        {
            if (shooterController.SelectedWeapon != null && characterEngine.IsZooming)
            {
                if (shooterController.SelectedWeapon.State != WeaponState.Holst)
                {
                    if (shooterController.SelectedWeapon.State != WeaponState.Equipping &&
                        shooterController.SelectedWeapon.State != WeaponState.Unequipping &&
                        shooterController.SelectedWeapon.State != WeaponState.Idle)
                    {
                        if (shooterController.SelectedWeapon.State == WeaponState.Reloading)
                        {
                            if (shooterController.SelectedWeapon.WeaponData.FireType == WeaponFireType.HoldAndFire)
                            {
                                GetCrosshairSprites();
                                ControlCrosshairRecoil();
                                return;
                            }
                        }
                        else
                        {

                            GetCrosshairSprites();
                            ControlCrosshairRecoil();
                            return;
                        }
                    }
                }
            }

            SetCrosshairsVisibility(false);

        }


        private void GetCrosshairSprites()
        {
            Crosshair crosshair = shooterController.SelectedWeapon.WeaponData.WeaponCrosshair;
            ClearCrosshairSprites();             // Clear current sprite of all crosshairs

            m_Center.sprite = crosshair.Center;
            m_Bottom.sprite = crosshair.Bottom;
            m_Top.sprite = crosshair.Top;
            m_Right.sprite = crosshair.Right;
            m_Left.sprite = crosshair.Left;

            SetCrosshairsVisibility(true);
        }


        /// <summary>
        /// Set all crosshair visibility and also adjust it to native size
        /// </summary>
        /// <param name="visible"></param>
        private void SetCrosshairsVisibility(bool visible)
        {
            if (visible)
            {
                m_Center.SetNativeSize();
                m_Bottom.SetNativeSize();
                m_Top.SetNativeSize();
                m_Right.SetNativeSize();
                m_Left.SetNativeSize();
            }

            m_Center.enabled = (m_Center.sprite == null) ? false : visible;
            m_Top.enabled = (m_Top.sprite == null) ? false : visible;
            m_Bottom.enabled = (m_Bottom.sprite == null) ? false : visible;
            m_Right.enabled = (m_Right.sprite == null) ? false : visible;
            m_Left.enabled = (m_Left.sprite == null) ? false : visible;
        }


        /// <summary>
        /// Clear all crosshairs sprites
        /// </summary>
        private void ClearCrosshairSprites()
        {
            m_Center.sprite = null;
            m_Bottom.sprite = null;
            m_Top.sprite = null;
            m_Right.sprite = null;
            m_Left.sprite = null;
        }


        private void ControlCrosshairRecoil()
        {
            Crosshair crosshair = shooterController.SelectedWeapon.WeaponData.WeaponCrosshair;

            float offset = crosshair.CrosshairCenterOffset + Recoil * 100f;

            m_Top.rectTransform.localPosition = new Vector3(0, offset);
            m_Bottom.rectTransform.localPosition = new Vector3(0, -offset);
            m_Right.rectTransform.localPosition = new Vector3(offset, 0);
            m_Left.rectTransform.localPosition = new Vector3(-offset, 0);

            Recoil = Mathf.SmoothDamp(Recoil, 0, ref recoilReturnSpeed, crosshair.CrosshairReturnTime);
            Recoil = Mathf.Clamp(Recoil, 0, 1);
        }

        private void GetWeaponRecoil(GameObject target, object recoilAmount)
        {
            if (target != m_TargetCharacter || !(recoilAmount is float))
                return;

            Recoil += (float)recoilAmount;
        }
    }
}
using UnityEngine;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    public class PickableItem : MonoBehaviour
    {
        [SerializeField] private InventoryData m_Inventory;
        [SerializeField] private Bullet m_Bullet;
        [SerializeField] private Weapon m_Weapon;
        [SerializeField] private int amount = 1;

        [Header("Respawn Options")]
        [SerializeField] private bool m_RespawnItem = false;
        [SerializeField] private float m_TimeToRespawn = 10f;

        private Renderer[] renderers;
        private Collider coll;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            coll = GetComponent<Collider>();
        }

        private void SetRenderersVisibility(bool visible)
        {
            foreach (Renderer render in renderers)
                render.enabled = visible;

            coll.enabled = visible;
        }

        /// <summary>
        /// Pick this item
        /// </summary>
        public void Pick()
        {
            if (m_Bullet != null)
                m_Inventory.AddBulletToInventory(m_Bullet, amount);

            if (m_Weapon != null)
                m_Inventory.AddWeapon(m_Weapon);

            SetRenderersVisibility(false);

            if (m_RespawnItem)
                Invoke("Respawn", m_TimeToRespawn);
            
        }

        private void Respawn()
        {
            SetRenderersVisibility(true);
        }
    }
}
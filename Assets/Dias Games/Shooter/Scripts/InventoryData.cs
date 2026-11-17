using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    [CreateAssetMenu]
    public class InventoryData : ScriptableObject
    {
        public List<BulletItem> Bullets;                        // List of Bullets
        public List<Weapon> Weapons = new List<Weapon>();       // List of Weapons

        //Event used to update UI info
        public event Action OnInventoryUpdate;
        
        public void StartNewGameInventory(List<BulletItem> InitialItems, List<Weapon> InitialWeapons)
        {
            Bullets.Clear();
            Weapons.Clear();
            
            Bullets.AddRange(InitialItems);
            Weapons.AddRange(InitialWeapons);
        }

        /// <summary>
        /// Adds a new bullet to inventory list
        /// </summary>
        /// <param name="desiredBullet"></param>
        /// <param name="amount"></param>
        public void AddBulletToInventory(Bullet desiredBullet, int amount = 1)
        {
            BulletItem bulletListed = Bullets.Find(x => x.Bullet == desiredBullet);

            // Only add new bullet if there is nothing yet
            if (bulletListed == null)
                Bullets.Add(new BulletItem(desiredBullet, amount));
            else // Only increase amount
                bulletListed.Amount += amount;

            OnInventoryUpdate?.Invoke();
        }

        /// <summary>
        /// Loop trough all bullets to use it or return false if noone were found
        /// </summary>
        /// <param name="desiredBullet"></param>
        /// <returns></returns>
        public bool ConsumeBullet(Bullet desiredBullet)
        {
            BulletItem bullet = Bullets.Find(x => x.Bullet == desiredBullet);
            if(bullet != null)
            {
                bullet.Amount--;
                if (bullet.Amount <= 0)
                    Bullets.Remove(bullet);

                OnInventoryUpdate?.Invoke();
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Check if there is a certain bullet in inventory
        /// </summary>
        /// <param name="bullet">Target bullet to check</param>
        /// <param name="currentLoadedBullets">How many bullets weapon is carrying</param>
        /// <returns></returns>
        public bool HasBullets(Bullet bullet, float currentLoadedBullets)
        {
            foreach (BulletItem bulletInventory in Bullets)
            {
                if (bulletInventory.Bullet == bullet)
                    return (bulletInventory.Amount > currentLoadedBullets);
            }

            return false;
        }
        
        /// <summary>
        /// Add a new weapon to inventory if character doesn't have this weapon
        /// </summary>
        /// <param name="reference"></param>
        public void AddWeapon(Weapon reference)
        {
            if (!Weapons.Contains(reference))
            {
                Weapons.Add(reference);
                OnInventoryUpdate?.Invoke();
            }
        }
    }

    [Serializable]
    public class BulletItem
    {
        [SerializeField] private Bullet m_Bullet;
        public int Amount;

        public Bullet Bullet { get { return m_Bullet; } }

        public BulletItem(Bullet newItem, int amount)
        {
            m_Bullet = newItem;
            Amount = amount;
        }
    }    
}
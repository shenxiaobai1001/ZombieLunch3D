using UnityEngine;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    public enum WeaponFireType { Manual, Automatic, HoldAndFire }

    [CreateAssetMenu]
    public class Weapon : ScriptableObject
    {
        [Header("Bullets")]
        [SerializeField] private Bullet m_Bullets;
        [SerializeField][Min(1)] private int m_BulletSpreadCount = 1;

        [Header("Fire Parameters")]
        [Tooltip("Automatic: keep fire button pressed to keep firing. \nManual: must press down for each fire. \nHold and fire: for bow, hold to stretch and release fire button to fire")]
        [SerializeField] private WeaponFireType m_FireType = WeaponFireType.Automatic;
        [Range(0,1)][SerializeField] private float m_Recoil;
        [Range(0,1)][SerializeField] private float m_Accuracy;
        [Tooltip("Max Distance to find a target")] [SerializeField] private float m_MaxFireDistance = 100f;
        [SerializeField] private float m_Damage;

        [Tooltip("How many fires per second")] [SerializeField] private int m_FireRate = 2;

        [Header("Audio clips")]
        [SerializeField] private AudioClip m_EquipWeaponClip;
        [SerializeField] private AudioClip m_UnequipWeaponClip;

        [Header("Fire audio clips")]
        [SerializeField] private AudioClip m_EmptyFireClip;
        [SerializeField] private AudioClip[] m_FirstShotClips;
        [SerializeField] private AudioClip[] m_ShotClips;
        [SerializeField] private AudioClip[] m_LastShotClips;
        [SerializeField] private AudioClip[] m_StretchClips;


        // UI
        [Header("UI")]
        [SerializeField] private Sprite m_ItemSprite;
        [SerializeField] private Crosshair m_Crosshair;
        


        // Public getters
        public Bullet Bullets { get { return m_Bullets; } }
        public int BulletSpreadCount { get { return m_BulletSpreadCount; } }
        public float Recoil { get { return m_Recoil; } }
        public float Accuracy { get { return m_Accuracy; } }
        public float MaxFireDistance { get { return m_MaxFireDistance; } }
        public float Damage { get { return m_Damage;} }
        public int FireRate { get { return m_FireRate; } }
        public WeaponFireType FireType { get { return m_FireType; } }

        // Audio clips
        public AudioClip[] FirstShotClips { get { return m_FirstShotClips; } }
        public AudioClip[] ShotClips { get { return m_ShotClips; } }
        public AudioClip[] LastShotClips { get { return m_LastShotClips; } }
        public AudioClip[] StretchClips { get { return m_StretchClips; } }
        public AudioClip EmptyClip { get { return m_EmptyFireClip; } }
        public AudioClip GetWeaponClip { get { return m_EquipWeaponClip; } }
        public AudioClip HolsterWeapon { get { return m_UnequipWeaponClip; } }

        // UI sprites
        public Sprite ItemSprite { get { return m_ItemSprite; } }
        public Crosshair WeaponCrosshair { get { return m_Crosshair; } }

    }

    [System.Serializable]
    public class Crosshair
    {
        // Four Sprites + Center sprite
        [Header("Sprites")]
        [SerializeField] private Sprite m_Center;
        [SerializeField] private Sprite m_Top;
        [SerializeField] private Sprite m_Bottom;
        [SerializeField] private Sprite m_Right;
        [SerializeField] private Sprite m_Left;

        [Header("Crosshair parameters")]
        [SerializeField] private float m_CrosshairCenterOffset;
        [SerializeField] private float m_CrosshairReturnTime;
                
        public Sprite Center { get { return m_Center; } }
        public Sprite Top { get { return m_Top; } }
        public Sprite Bottom { get { return m_Bottom; } }
        public Sprite Right { get { return m_Right; } }
        public Sprite Left { get { return m_Left; } }
        public float CrosshairCenterOffset { get { return m_CrosshairCenterOffset; } }
        public float CrosshairReturnTime { get { return m_CrosshairReturnTime; } }
    }
}
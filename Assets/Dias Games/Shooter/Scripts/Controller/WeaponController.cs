using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    public enum AnimatorLayer { Base, Upper, RightArm, LeftArm }                                       // Layers for weapon play animation

    public enum WeaponState { Holst, Equipping, Unequipping, Idle, Aiming, Firing, Reloading, Holding }

    [RequireComponent(typeof(AudioSource))]
    public class WeaponController : MonoBehaviour
    {
        // ------------ Exposed parameters --------------- //

        [SerializeField] private Weapon m_WeaponData = null;
        [SerializeField] private InventoryData m_Inventory = null;
        [SerializeField] private LayerMask m_AimLayers = (1 << 0 | 1 << 14 | 1 << 16 | 1 << 17 | 1 << 18 | 1 << 19 | 1 << 20 | 1 << 21 | 1 << 22 | 1 << 24 | 1 << 25 | 1 << 26);
        [SerializeField] private int m_ClipSize = 30;
        [SerializeField] private bool m_AutoReload = true;
        [SerializeField] private bool m_InfinityBullets = false;
        [Tooltip("Use this var to set maximum time player can hold to fire. Only works with weapons that are Hold And Fire, as Bow.")]
        [SerializeField] private float m_MaxTimeHolding = 0f;

        [SerializeField] private Transform m_HolstTarget = null;

        public int ClipSize { get { return m_ClipSize; } }
        public bool HasInfinityBullets { get { return m_InfinityBullets; } }

        // Fire Parameters
        [Header("Fire Effects")]
        [SerializeField] private Transform m_FirePoint = null;
        [SerializeField] private GameObject m_Muzzle = null;
        [SerializeField] private GameObject m_Smoke = null;
        [SerializeField] private TracerController m_ShotLineTracer = null;

        [Header("IK Parameters")]
        [SerializeField] private Vector3 m_SpineRotationOffset = Vector3.zero;
        [SerializeField] private Transform m_HandAidIK = null;

        // Animator parameters
        [SerializeField] private AnimatorState EquipAnimator = new AnimatorState();
        [SerializeField] private AnimatorState UnequipAnimator = new AnimatorState();
        [SerializeField] private AnimatorState IdleAnimator = new AnimatorState();
        [SerializeField] private AnimatorState AimingAnimator = new AnimatorState();
        [SerializeField] private AnimatorState HoldingAnimator = new AnimatorState();
        [SerializeField] private AnimatorState FireAnimator = new AnimatorState();
        [SerializeField] private AnimatorState ReloadAnimator = new AnimatorState();

        // ------------ Important Components ------------- //

        public ThirdPersonSystem m_System { get; private set; }
        private UnityInputManager m_InputManager;
        private AnimatorManager m_AnimatorManager;
        private AimingIK m_AimingIK;
        private AudioSource m_Source;
        private Transform m_Camera;

        // ------------ UNITY EVENTS ------------------------ //

        public UnityEvent OnStartFireEvent, OnFireEvent, OnFinishFireEvent, OnStartReloadEvent, OnFinishReloadEvent;
        public UnityEvent OnEquipWeaponEvent, OnUnequipWeaponEvent;
        public UnityEvent OnEmptyFireEvent;
        public UnityEvent OnShakeEvent;

        // ------------ Action Events ----------------------- //

        public Action OnEquip, OnUnequip, OnFire, OnStartFire, OnFinishFire,
            OnStartReload, OnFinishReload, OnEmptyFire;

        // ----------- Internal Controller Vars ------------- //

        private int m_LoadedBullets = 0;
        public int LoadedBullets { get { return m_LoadedBullets; } }
        private float m_LastFiredTime = 0;
        private float m_TimeHolding = 0;

        private AnimatorState m_CurrentState = null;
        private Renderer[] m_Renderers;
        private HitImpactController m_ImpactController = null;

        private Strafe m_StrafeAbility;
        private float shakeTimeCounter = 0;

        // Recoil controllers
        private float accuracyLossByRecoil = 0;
        private float accuracyReturnSpeed;

        // Control weapon position
        Vector3 m_InitialPosition = Vector3.zero;
        Quaternion m_InitialRotation = Quaternion.identity;
        Vector3 m_InitialScale = Vector3.one;
        Transform m_Parent = null;

        // Control if weapon is in inventory
        private bool m_IsWeaponOnInventory = false;

        // ----------- Public Vars ------------------------- //

        public WeaponState State { get; private set; } = WeaponState.Holst;

        public Weapon WeaponData { get { return m_WeaponData; } }


        /// ------------------ GAME LOGIC ------------------------ ///
        /// 

        private void Awake()
        {
            m_System = GetComponentInParent<ThirdPersonSystem>();
            m_AnimatorManager = GetComponentInParent<AnimatorManager>();
            m_StrafeAbility = GetComponentInParent<Strafe>();
            m_AimingIK = GetComponentInParent<AimingIK>();
            m_Renderers = GetComponentsInChildren<Renderer>();
            m_Source = GetComponent<AudioSource>();
            m_ImpactController = FindObjectOfType<HitImpactController>();

            LoadBullets();

            // Get initial transform
            m_InitialPosition = transform.localPosition;
            m_InitialRotation = transform.localRotation;
            m_InitialScale = transform.localScale;
            m_Parent = transform.parent;

            // Start weapon on holst
            OnHolst();

            // Disable this weapon if there isn't this wepon in the list
            m_IsWeaponOnInventory = m_Inventory.Weapons.Contains(m_WeaponData);
            
            if(!m_IsWeaponOnInventory)
                UpdateVisibility(false);
        }

        /// <summary>
        /// Delegate function that checks if this weapon was taken by character
        /// </summary>
        private void UpdatedInventory()
        {
            if (!m_IsWeaponOnInventory)
            {
                if (m_Inventory.Weapons.Contains(m_WeaponData))
                {
                    UpdateVisibility(true);
                    OnHolst();
                    LoadBullets();
                    m_IsWeaponOnInventory = true;
                }
            }
        }


        private void Start()
        {
            if (m_System != null)
                m_InputManager = m_System.InputManager;

            m_Camera = Camera.main.transform;
        }

        private void OnEnable()
        {
            m_System.OnAbilityChanged += UpdateAnimations;
            m_Inventory.OnInventoryUpdate += UpdatedInventory;
        }
        private void OnDisable()
        {
            m_System.OnAbilityChanged -= UpdateAnimations;
            m_Inventory.OnInventoryUpdate -= UpdatedInventory;
        }

        private void UpdateAnimations(ThirdPersonAbility ability)
        {
            if (State != WeaponState.Holst && m_CurrentState != null)
                PlayAnimatorState(m_CurrentState, true);
            
        }

        /// <summary>
        /// Play animations related to weapon state
        /// </summary>
        /// <param name="targetState"></param>
        private void PlayAnimatorState(AnimatorState targetState, bool ignoreSameState = false, float transitionDuration = 0.2f)
        {
            // Do nothing if character is already playing this animation
            if (m_AnimatorManager == null)
                return;

            if (!ignoreSameState && m_CurrentState == targetState)
                return;

            // Get default states
            List<State> states = targetState.States; 
            
            AnimatorAbilityState AbilityAnimator = null;
            if(targetState.AbilitiesStates.Count > 0 && m_System.ActiveAbility != null)
                AbilityAnimator = targetState.AbilitiesStates.Find(x => x.AbilityReference == m_System.ActiveAbility.GetType().Name);
            
            List<State> abilityStates = new List<State>();
            if (AbilityAnimator != null)
                abilityStates = AbilityAnimator.States;

            for (int i=0; i <AnimatorManager.LayersCount; i++) 
            {
                if (abilityStates.Count > 0)
                { 
                    // There is a special animation for current active ability
                    State state = abilityStates.Find(x => (int)x.Layer == i);
                    if (state != null)
                    {
                        if (i == 0)
                        {
                            if (m_AnimatorManager.IsPlayingState(state.StateName, 0))
                                continue;
                        }
                        
                        m_AnimatorManager.SetAnimatorState(state.StateName, transitionDuration, i);
                    }
                    else
                    {
                        if(i == 0)
                        {
                            if (!m_AnimatorManager.IsPlayingState(m_System.ActiveAbility.CurrentStatePlaying, 0))
                                m_AnimatorManager.SetAnimatorState(m_System.ActiveAbility.CurrentStatePlaying, m_System.ActiveAbility.TransitionDuration,0);
                        }
                        else
                            m_AnimatorManager.DisableLayer(string.Empty, transitionDuration, i);
                    }
                }
                else
                {
                    // There is no special animation for current active ability, use default states
                    State state = states.Find(x => (int)x.Layer == i);
                    if (state != null)
                    {
                        if (i == 0)
                        {
                            if (m_AnimatorManager.IsPlayingState(state.StateName, 0))
                                continue;
                        }

                        m_AnimatorManager.SetAnimatorState(state.StateName, transitionDuration, i);
                    }
                    else
                    {
                        if (i == 0 && m_System.ActiveAbility != null)
                        {
                            if (!m_AnimatorManager.IsPlayingState(m_System.ActiveAbility.CurrentStatePlaying, 0))
                                m_AnimatorManager.SetAnimatorState(m_System.ActiveAbility.CurrentStatePlaying, m_System.ActiveAbility.TransitionDuration, 0);
                        }
                        else
                            m_AnimatorManager.DisableLayer(string.Empty, transitionDuration, i);
                    }
                }
            }

            m_CurrentState = targetState;
        }

        public void Equip()
        {
            PlayAnimatorState(EquipAnimator);
            State = WeaponState.Equipping;

            OnEquipWeaponEvent.Invoke();
            OnEquip?.Invoke();

            if (m_StrafeAbility != null)
                m_StrafeAbility.LookForward = false;
        }

        public void Unequip()
        {
            PlayAnimatorState(UnequipAnimator);
            State = WeaponState.Unequipping;

            OnUnequipWeaponEvent.Invoke();
            OnUnequip?.Invoke();

            m_AimingIK.StopHandIK();

            m_AnimatorManager.SetAnimatorState(m_System.ActiveAbility.CurrentStatePlaying);

            if (m_StrafeAbility != null)
                m_StrafeAbility.LookForward = true;
        }
        private void FinishUnequip()
        {
            State = WeaponState.Holst;
            for (int i = 1; i < AnimatorManager.LayersCount; i++)
                m_AnimatorManager.DisableLayer(string.Empty, 0.1f, i);

            if (m_AimingIK != null)
                m_AimingIK.StopBodyIK();

            OnHolst();

            // Play Holst Sound
            PlayWeaponSound(m_WeaponData.HolsterWeapon);
        }

        private void Update()
        {
            if (m_WeaponData == null || m_System == null)
                return;

            accuracyLossByRecoil = Mathf.SmoothDamp(accuracyLossByRecoil, 0, ref accuracyReturnSpeed, 
                WeaponData.WeaponCrosshair.CrosshairReturnTime);


            switch (State)
            {
                case WeaponState.Holst:
                    break;
                case WeaponState.Equipping:
                    if (HasFinishedAnimation(EquipAnimator))
                        State = WeaponState.Idle;
                    
                    break;
                case WeaponState.Unequipping:
                    if (HasFinishedAnimation(UnequipAnimator))
                        FinishUnequip();
                    
                    break;
                case WeaponState.Idle:
                    Idle();

                    if (m_InputManager.reloadButton.WasPressed)
                        TryReload();
                    
                    break;
                case WeaponState.Aiming:
                    Aim();
                    HandleFireInput();

                    if (m_InputManager.reloadButton.WasPressed)
                        TryReload();

                    if (m_System.ActiveAbility != null)
                    {
                        if (m_System.ActiveAbility.AllowCameraZoom == false)
                            State = WeaponState.Idle;
                    }

                    break;
                case WeaponState.Firing:

                    if (!m_System.IsZooming)
                        State = WeaponState.Idle;

                    if(m_WeaponData.FireType != WeaponFireType.HoldAndFire)
                        HandleFireInput();

                    if (m_WeaponData.FireType != WeaponFireType.Automatic)
                    {
                        if (HasFinishedAnimation(FireAnimator, true))
                            State = WeaponState.Aiming;
                    }

                    if(m_System.ActiveAbility != null)
                    if (m_System.ActiveAbility.AllowCameraZoom == false)
                        State = WeaponState.Idle;

                    if (m_InputManager.reloadButton.WasPressed)
                        TryReload();

                    m_AimingIK.ApplyHandIK(m_HandAidIK);

                    break;
                case WeaponState.Reloading:
                    Reload();
                    m_AimingIK.StopHandIK();
                    break;
                case WeaponState.Holding:
                    Holding();
                    break;  
                default:
                    break;
            }

            if (State != WeaponState.Holst)
            {
                if (m_System.IsZooming)
                    m_AimingIK.ApplyBodyIK(m_SpineRotationOffset);
                else
                    m_AimingIK.StopBodyIK();
            }
        }

        private void Idle()
        {
            if (m_System.IsZooming)
            {
                State = WeaponState.Aiming;
                return;
            }

            // Set animation
            PlayAnimatorState(IdleAnimator);

            if (m_System.ActiveAbility != null)
            {
                if (m_System.ActiveAbility is RollAbility)
                {
                    m_AimingIK.StopHandIK();
                    return;
                }
            }
            m_AimingIK.ApplyHandIK(m_HandAidIK);
        }

        private void Aim()
        {
            if (!m_System.IsZooming)
            {
                State = WeaponState.Idle;
                return;
            }

            if (m_AutoReload)
            {
                if (m_LoadedBullets <= 0 && m_WeaponData.FireType == WeaponFireType.HoldAndFire)
                {
                    if (TryReload())
                        return;
                }
            }
            // Set Aim animation
            PlayAnimatorState(AimingAnimator);
            m_AimingIK.ApplyHandIK(m_HandAidIK);
        }

        private void HandleFireInput()
        {
            RaycastHit hitPoint = GetHitPoint();

            // Check to Fire
            switch (m_WeaponData.FireType)
            {
                case WeaponFireType.Manual:

                    if (m_InputManager.fireButton.WasPressed)
                    {
                        State = WeaponState.Firing;
                        Fire(true);
                        return;
                    }

                    break;
                case WeaponFireType.Automatic:
                    if (m_InputManager.fireButton.IsPressed)
                    {
                        Fire(false);

                        if(State != WeaponState.Reloading)
                            State = WeaponState.Firing;

                        return;
                    }
                    else
                    {
                        if(State == WeaponState.Firing)
                        {
                            if (m_LastFiredTime > Time.time && m_LoadedBullets > 0)
                            {
                                OnFinishFireEvent.Invoke();
                                OnFinishFire?.Invoke();

                                PlayWeaponSound(m_WeaponData.LastShotClips);
                            }

                            State = WeaponState.Aiming;
                        }
                    }
                    break;
                case WeaponFireType.HoldAndFire:

                    if (m_LoadedBullets > 0)
                    {
                        if (m_InputManager.fireButton.IsPressed)
                        {
                            if (State != WeaponState.Holding)
                            {
                                m_TimeHolding = m_MaxTimeHolding;
                                PlayWeaponSound(m_WeaponData.StretchClips);
                            }

                            State = WeaponState.Holding;
                        }
                        else
                        {
                            if (State == WeaponState.Holding)
                            {
                                State = WeaponState.Firing;
                                Fire(false);
                            }
                        }
                    }

                    break;
            }
        }

        private void Holding()
        {
            // Check if weapon is empty
            if (m_LoadedBullets <= 0)
            {
                if (m_AutoReload)
                {
                    if (TryReload())
                        return;
                }

                State = WeaponState.Idle;
                return;
            }

            PlayAnimatorState(HoldingAnimator);

            // Don't fire if there is no time to hold
            if (m_MaxTimeHolding == 0)
                return;

            if (HasFinishedAnimation(HoldingAnimator))
            {
                if (m_TimeHolding <= 0)
                {
                    State = WeaponState.Firing;
                    Fire();
                    return;
                }

                m_TimeHolding -= Time.deltaTime;

                if(m_TimeHolding <= 1f)
                {
                    if(shakeTimeCounter <= 0)
                    {
                        OnShakeEvent.Invoke();
                        shakeTimeCounter = 0.1f;
                    }

                    shakeTimeCounter -= Time.deltaTime;
                }
            }

            HandleFireInput();
        }

        private void Fire(bool ignoreSameState = true)
        {
            // Check fire rate before fire
            if (m_LastFiredTime > Time.time)
                return;

            // Check if weapon is empty
            if (m_LoadedBullets <= 0)
            {
                if (m_AutoReload)
                {
                    if (TryReload())
                        return;
                }

                State = WeaponState.Aiming;

                OnEmptyFireEvent.Invoke();
                OnEmptyFire?.Invoke();

                // Play Empty clip
                PlayWeaponSound(m_WeaponData.EmptyClip);
                m_LastFiredTime = Time.time + (1.0f / m_WeaponData.FireRate) * 2;
                return;
            }

            for (int i = 0; i < m_WeaponData.BulletSpreadCount; i++)
            {
                RaycastHit hit = GetHitPoint();

                CauseDamage(hit);
                SpawnShot(hit);
            }

            SpawnMuzzle();
            RecoilEffect();
            ConsumeBullet();

            // Play animation
            PlayAnimatorState(FireAnimator, ignoreSameState, 0.001f);

            // Play sound effect
            if (State != WeaponState.Firing)
            {
                OnStartFireEvent.Invoke();
                OnStartFire?.Invoke();

                // Play First Shot clip
                PlayWeaponSound(m_WeaponData.FirstShotClips);
            }
            else
                PlayWeaponSound(m_WeaponData.ShotClips);

            m_LastFiredTime = Time.time + (1.0f / m_WeaponData.FireRate);

            OnFire?.Invoke();
            OnFireEvent.Invoke();
        }

        private void SpawnMuzzle()
        {
            if (m_Muzzle != null)
            {
                GameObject muzzle = Instantiate(m_Muzzle, m_FirePoint.position, Quaternion.identity, m_FirePoint);

                muzzle.transform.localRotation = Quaternion.identity;

                Vector3 local = muzzle.transform.localScale;
                Vector3 lossy = muzzle.transform.lossyScale;

                muzzle.transform.localScale = new Vector3(local.x / lossy.x, local.y / lossy.y, local.z / lossy.z);
            }

            if (m_Smoke != null)
                Instantiate(m_Smoke, m_FirePoint.position, Quaternion.identity, m_FirePoint);
        }

        private void SpawnShot(RaycastHit hit)
        {
            if (m_ShotLineTracer != null)
            {
                TracerController tracer = Instantiate(m_ShotLineTracer, m_FirePoint.position, Quaternion.identity);
                if (hit.collider == null)
                    tracer.SetTracerPoints(m_FirePoint.position, m_Camera.forward * m_WeaponData.MaxFireDistance);
                else
                    tracer.SetTracerPoints(m_FirePoint.position, hit.point);
                
            }

            if (m_ImpactController != null)
                m_ImpactController.SpawnImpact(hit);
        }

        private void CauseDamage(RaycastHit hit)
        {
            if (hit.collider == null)
                return;

            GlobalEvents.ExecuteEvent("Damage", hit.transform.gameObject, m_WeaponData.Damage / m_WeaponData.BulletSpreadCount);
        }

        private void ConsumeBullet()
        {
            m_LoadedBullets--;
            m_Inventory.ConsumeBullet(m_WeaponData.Bullets);
        }

        private void RecoilEffect()
        {
            // Add recoil effect
            accuracyLossByRecoil = UnityEngine.Random.Range(m_WeaponData.Recoil * 0.5f, m_WeaponData.Recoil);
            GlobalEvents.ExecuteEvent("AddRecoil", m_System.gameObject, accuracyLossByRecoil);
        }

        private bool TryReload()
        {
            if(m_Inventory.HasBullets(m_WeaponData.Bullets, m_LoadedBullets) || m_InfinityBullets)
            {
                if (m_LoadedBullets < m_ClipSize)
                {
                    State = WeaponState.Reloading;

                    OnStartReload?.Invoke();
                    OnStartReloadEvent.Invoke();

                    return true;
                }
            }

            return false;
        }

        private void Reload()
        {
            if(m_WeaponData.FireType == WeaponFireType.HoldAndFire)
            {
                if(!m_System.IsZooming)
                {
                    State = WeaponState.Idle;
                    return;
                }
            }

            // Go to reload animation
            PlayAnimatorState(ReloadAnimator);

            if (HasFinishedAnimation(ReloadAnimator, true))
            {
                LoadBullets();

                State = (m_System.IsZooming) ? WeaponState.Aiming : WeaponState.Idle;

                OnFinishReloadEvent.Invoke();
                OnFinishReload?.Invoke();
            }
        }

        /// <summary>
        /// Change weapon parent to make it fixed on character hand
        /// </summary>
        public void TakeWeaponFromHolst()
        {
            transform.parent = m_Parent;
            transform.localPosition = m_InitialPosition;
            transform.localRotation = m_InitialRotation;
            transform.localScale = m_InitialScale;

            UpdateVisibility(true);

            // Play take Sound
            PlayWeaponSound(m_WeaponData.GetWeaponClip);
        }

        public void OnHolst()
        {
            if(m_HolstTarget == null)
            {
                UpdateVisibility(false);
                return;
            }

            transform.parent = m_HolstTarget;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        private bool HasFinishedAnimation(AnimatorState targetState, bool loop = false)
        {
            if (m_System.ActiveAbility != null)
            {
                AnimatorAbilityState abilityState = targetState.AbilitiesStates.Find(x => x.AbilityReference == m_System.ActiveAbility.GetType().Name);
                if(abilityState != null)
                {
                    int i = 0;
                    while (abilityState.States[i].Layer == AnimatorLayer.Base && i < abilityState.States.Count)
                        i++;

                    if (i >= abilityState.States.Count)
                        return true;

                    if (abilityState.States.Count > 0)
                    {
                        return m_AnimatorManager.HasFinishedAnimation(abilityState.States[i].StateName, (int)abilityState.States[i].Layer, loop);
                    }
                }
            }

            if (targetState.States.Count > 0)
            {
                int i = 0;
                while (targetState.States[i].Layer == AnimatorLayer.Base && i < targetState.States.Count && targetState.States.Count > 1)
                    i++;

                if (i >= targetState.States.Count)
                    return true;

                return m_AnimatorManager.HasFinishedAnimation(targetState.States[i].StateName, (int)targetState.States[i].Layer, loop);
            }

            return true;
        }

        /// <summary>
        /// Load weapon clip with bullets in the inventory
        /// </summary>
        private void LoadBullets()
        {
            if (m_InfinityBullets)
            {
                m_LoadedBullets = m_ClipSize;       // Update clip bullets
                return;
            }

            foreach (BulletItem bullet in m_Inventory.Bullets)
            {
                if (bullet.Bullet == m_WeaponData.Bullets)
                {
                    int amount = bullet.Amount;
                    m_LoadedBullets = Mathf.Clamp(amount, 0, m_ClipSize);       // Update clip bullets
                    return;
                }
            }
        }

        private void UpdateVisibility(bool visible)
        {
            foreach (Renderer renderer in m_Renderers)
                renderer.enabled = visible;
        }

        private RaycastHit GetHitPoint()
        {
            RaycastHit camHit, weaponHit;

            Vector3 Start = m_FirePoint.position;
            Vector3 Origin = m_Camera.position;

            float spread = (1 - m_WeaponData.Accuracy + accuracyLossByRecoil) * 0.2f;
            Origin += m_Camera.transform.right * UnityEngine.Random.Range(-spread, spread);
            Origin += m_Camera.transform.up * UnityEngine.Random.Range(-spread, spread);

            Vector3 Direction = (m_Camera.position + m_Camera.forward * m_WeaponData.MaxFireDistance - Start).normalized;

            if(Physics.Raycast(Origin, m_Camera.forward, out camHit, m_WeaponData.MaxFireDistance, m_AimLayers, QueryTriggerInteraction.Collide))
                Direction = (camHit.point - Start).normalized;
            
            Physics.Raycast(Start, Direction, out weaponHit, m_WeaponData.MaxFireDistance, m_AimLayers, QueryTriggerInteraction.Collide);

            return weaponHit;
        }

        public void PlayWeaponSound(AudioClip[] m_Clips)
        {
            if (m_Source == null || m_Clips.Length == 0)
                return;

            int i = UnityEngine.Random.Range(0, m_Clips.Length);
            m_Source.PlayOneShot(m_Clips[i]);
        }
        public void PlayWeaponSound(AudioClip m_Clip)
        {
            if (m_Source == null || m_Clip == null)
                return;

            m_Source.PlayOneShot(m_Clip);
        }
    }

    [Serializable]
    public class State
    {
        [SerializeField] private string m_StateName = string.Empty;
        [SerializeField] private AnimatorLayer m_Layer;

        public string StateName { get { return m_StateName; } }
        public AnimatorLayer Layer { get { return m_Layer; } }
    }

    [Serializable]
    public class AnimatorState
    {
        [SerializeField] private List<State> m_States = new List<State>();
        [SerializeField] private List<AnimatorAbilityState> m_AbilitiesStates = new List<AnimatorAbilityState>();

        public List<State> States { get { return m_States; } }
        public List<AnimatorAbilityState> AbilitiesStates { get { return m_AbilitiesStates; } }
    }
    
    [Serializable]
    public class AnimatorAbilityState
    {
        [SerializeField] private string m_AbilityReference;
        [SerializeField] private List<State> m_States = new List<State>();

        public string AbilityReference { get { return m_AbilityReference; } }
        public List<State> States { get { return m_States; } }
    }
}
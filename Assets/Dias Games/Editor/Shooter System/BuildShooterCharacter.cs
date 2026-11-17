using UnityEngine;
using UnityEditor;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    public class WeaponList
    {
        public Weapon weapon;
        public GameObject prefab = null;

        public WeaponList(Weapon targetWeapon)
        {
            weapon = targetWeapon;
        }
    }

    public class BuildShooterCharacter : BuildBasicCharacter
    {
        public InventoryData m_Data;

        [MenuItem("Dias Games/Shooter System/Build Shooter Character")]
        public new static void ShowWindow()
        {
            GetWindow<BuildShooterCharacter>(true, "Shooter Character Builder");
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            label = "Shooter Character Builder";
            m_AnimatorPath = "Assets/Dias Games/Shooter/Demo Assets/Animator/Shooter Animator.controller";
            m_HelpInstruction = "This window will help you to create a Third Person Shooter Character. Start selecting your scene character";
        }

        protected override void ShowCharacterOptions()
        {
            base.ShowCharacterOptions();

            m_Data = EditorGUILayout.ObjectField("Inventory Data", m_Data, typeof(InventoryData), true) as InventoryData;
            if (m_Data == null)
            {
                EditorGUILayout.HelpBox("Please select an Inventory to be used as reference for character.", MessageType.Error, false);
            }

        }
        protected override void CreateCharacter()
        {
            base.CreateCharacter();
            CreateSockets();
        }


        protected override void AddAbilities()
        {
            base.AddAbilities();

            // Add ability of take item
            PickupItemAbility m_PickAbility = character.GetComponent<PickupItemAbility>();
            if(m_PickAbility == null)
            {
                m_PickAbility = Undo.AddComponent<PickupItemAbility>(character);
            }
            
            // Add cover ability
            Cover m_Cover = character.GetComponent<Cover>();
            if (m_Cover == null)
            {
                m_Cover = Undo.AddComponent<Cover>(character);
            }

            FreeLocomotion m_FreeLocomotion = character.GetComponent<FreeLocomotion>();
            Strafe m_Strafe = character.GetComponent<Strafe>();
            RollAbility m_Roll = character.GetComponent<RollAbility>();

            AddIgnoreAbility(m_PickAbility, m_FreeLocomotion);
            AddIgnoreAbility(m_PickAbility, m_Strafe);
            AddIgnoreAbility(m_Cover, m_FreeLocomotion);
            AddIgnoreAbility(m_Cover, m_Strafe);
            AddIgnoreAbility(m_Cover, m_Roll);

            AddIgnoreAbility(m_Roll, m_Cover);
        }

        protected override void AddModifiers()
        {
            base.AddModifiers();

            SerializedProperty list = modifier.FindProperty("m_Modifiers");

            // Shooter Controller
            CharacterShooterController m_Shooter = character.GetComponent<CharacterShooterController>(); 
            if (m_Shooter == null)
            {
                m_Shooter = Undo.AddComponent<CharacterShooterController>(character);

                SerializedObject shooterObj = new SerializedObject(m_Shooter);
                if (m_Data != null)
                    shooterObj.FindProperty("m_Inventory").objectReferenceValue = m_Data;

                UpdateModifierList(m_Shooter);
                shooterObj.ApplyModifiedProperties();

            }


            SerializedObject shooterObject = new SerializedObject(m_Shooter);

            SerializedProperty abilities = shooterObject.FindProperty("m_AbilitiesAllowed");
            AddAllowedAbility(character.GetComponent<FreeLocomotion>(), ref abilities);
            AddAllowedAbility(character.GetComponent<Strafe>(), ref abilities);
            AddAllowedAbility(character.GetComponent<RollAbility>(), ref abilities);
            AddAllowedAbility(character.GetComponent<JumpAbility>(), ref abilities);
            AddAllowedAbility(character.GetComponent<FallAbility>(), ref abilities);
            AddAllowedAbility(character.GetComponent<CrouchAbility>(), ref abilities);
            AddAllowedAbility(character.GetComponent<Cover>(), ref abilities);

            // Add Climbing abilities if it exists.
            ThirdPersonAbility[] allAbilities = character.GetComponents<ThirdPersonAbility>();
            for (int i = 0; i < allAbilities.Length; i++)
            {
                if (allAbilities[i].GetType().Name.Contains("LowerClimb"))
                    AddAllowedAbility(allAbilities[i], ref abilities);
                else if (allAbilities[i].GetType().Name.Contains("StepUp"))
                    AddAllowedAbility(allAbilities[i], ref abilities);
                else if (allAbilities[i].GetType().Name.Contains("Vault"))
                    AddAllowedAbility(allAbilities[i], ref abilities);
                else if (allAbilities[i].GetType().Name.Contains("WallRun"))
                    AddAllowedAbility(allAbilities[i], ref abilities);

            }

            shooterObject.ApplyModifiedProperties();

            // SAiming IK
            AimingIK m_Aim = character.GetComponent<AimingIK>();
            if (m_Aim == null)
            {
                m_Aim = Undo.AddComponent<AimingIK>(character);
                UpdateModifierList(m_Aim);
            }
        }

        void AddAllowedAbility(ThirdPersonAbility ability, ref SerializedProperty abilitiesProperty)
        {
            if (ability == null || abilitiesProperty == null)
                return;

            abilitiesProperty.InsertArrayElementAtIndex(abilitiesProperty.arraySize);
            abilitiesProperty.GetArrayElementAtIndex(abilitiesProperty.arraySize - 1).objectReferenceValue = ability;
        }

        private void CreateSockets()
        {
            // Create Socket for Right Hand

            GameObject socket = new GameObject("WeaponSocket");
            socket.transform.parent = m_Animator.GetBoneTransform(HumanBodyBones.RightHand);
            socket.transform.localPosition = Vector3.zero;
            socket.transform.localRotation = Quaternion.identity;
            socket.transform.localScale = Vector3.one;

            ///

            // Create Socket for Left Hand

            GameObject leftSocket = new GameObject("WeaponSocket");
            leftSocket.transform.parent = m_Animator.GetBoneTransform(HumanBodyBones.LeftHand);
            leftSocket.transform.localPosition = Vector3.zero;
            leftSocket.transform.localRotation = Quaternion.identity;
            leftSocket.transform.localScale = Vector3.one;

            ///
        }

    }
}
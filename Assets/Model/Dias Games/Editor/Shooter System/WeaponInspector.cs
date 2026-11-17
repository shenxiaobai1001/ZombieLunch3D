using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    public enum WeaponSection { Properties, Animations, Events, Noone }

    [CustomEditor(typeof(WeaponController))]
    [CanEditMultipleObjects]
    public class WeaponControllerInspector : BaseInspector
    {
        SerializedProperty m_WeaponData;
        SerializedProperty m_Inventory;

        // Animator parameters
        SerializedProperty EquipAnimator;
        SerializedProperty UnequipAnimator;
        SerializedProperty IdleAnimator;
        SerializedProperty AimingAnimator;
        SerializedProperty FireAnimator;
        SerializedProperty ReloadAnimator;
        SerializedProperty HoldingAnimator;

        SerializedProperty OnStartFireEvent, OnFireEvent, OnFinishFireEvent, OnStartReloadEvent, OnFinishReloadEvent;
        SerializedProperty OnEquipWeaponEvent, OnUnequipWeaponEvent;
        SerializedProperty OnEmptyFireEvent;
        SerializedProperty OnShakeEvent;

        WeaponSection section = WeaponSection.Properties;
        bool editWeapon;
        int selectedAnimator;
        float m_CurrentFade = 0;

        string[] animatorOptions = { "Equip", "Unequip", "Idle", "Aiming", "Fire", "Reload", "Holding" };
        string[] animatorOptionsNoHolding = { "Equip", "Unequip", "Idle", "Aiming", "Fire", "Reload" };
        int selectedAbility;


        GUIStyle defaultPressed;
        List<Type> allAbilities = new List<Type>();

        protected override void OnEnable()
        {
            base.OnEnable();

            if (EditorPrefs.HasKey("Weapon Section"))
                section = (WeaponSection)EditorPrefs.GetInt("Weapon Section");
            if (EditorPrefs.HasKey("Edit Weapon"))
                editWeapon = EditorPrefs.GetBool("Edit Weapon");
            if (EditorPrefs.HasKey("Selected Animation"))
                selectedAnimator = EditorPrefs.GetInt("Selected Animation");
            if (EditorPrefs.HasKey("Selected Ability"))
                selectedAbility = EditorPrefs.GetInt("Selected Ability");

            defaultPressed = new GUIStyle(EditorStyles.miniButton);
            defaultPressed.normal.background = defaultPressed.active.background;


            OnStartFireEvent = serializedObject.FindProperty("OnStartFireEvent");
            customProperties.Add("OnStartFireEvent");

            OnFireEvent = serializedObject.FindProperty("OnFireEvent");
            customProperties.Add("OnFireEvent");

            OnFinishFireEvent = serializedObject.FindProperty("OnFinishFireEvent");
            customProperties.Add("OnFinishFireEvent");

            OnStartReloadEvent = serializedObject.FindProperty("OnStartReloadEvent");
            customProperties.Add("OnStartReloadEvent");

            OnFinishReloadEvent = serializedObject.FindProperty("OnFinishReloadEvent");
            customProperties.Add("OnFinishReloadEvent");

            OnEquipWeaponEvent = serializedObject.FindProperty("OnEquipWeaponEvent");
            customProperties.Add("OnEquipWeaponEvent");

            OnUnequipWeaponEvent = serializedObject.FindProperty("OnUnequipWeaponEvent");
            customProperties.Add("OnUnequipWeaponEvent");

            OnEmptyFireEvent = serializedObject.FindProperty("OnEmptyFireEvent");
            customProperties.Add("OnEmptyFireEvent");

            OnShakeEvent = serializedObject.FindProperty("OnShakeEvent");
            customProperties.Add("OnShakeEvent");


            m_WeaponData = serializedObject.FindProperty("m_WeaponData");
            customProperties.Add("m_WeaponData");

            m_Inventory = serializedObject.FindProperty("m_Inventory");
            customProperties.Add("m_Inventory");


            EquipAnimator = serializedObject.FindProperty("EquipAnimator");
            customProperties.Add("EquipAnimator");

            UnequipAnimator = serializedObject.FindProperty("UnequipAnimator");
            customProperties.Add("UnequipAnimator");

            IdleAnimator = serializedObject.FindProperty("IdleAnimator");
            customProperties.Add("IdleAnimator");

            AimingAnimator = serializedObject.FindProperty("AimingAnimator");
            customProperties.Add("AimingAnimator");

            FireAnimator = serializedObject.FindProperty("FireAnimator");
            customProperties.Add("FireAnimator");

            ReloadAnimator = serializedObject.FindProperty("ReloadAnimator");
            customProperties.Add("ReloadAnimator");

            HoldingAnimator = serializedObject.FindProperty("HoldingAnimator");
            customProperties.Add("HoldingAnimator");

            // Search through all of the assemblies to find any types that derive from Ability.
            allAbilities.Clear();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; ++i)
            {
                var assemblyTypes = assemblies[i].GetTypes();
                for (int j = 0; j < assemblyTypes.Length; ++j)
                {
                    // Must derive from Modifier.
                    if (!typeof(ThirdPersonAbility).IsAssignableFrom(assemblyTypes[j]))
                    {
                        continue;
                    }

                    // Ignore abstract classes.
                    if (assemblyTypes[j].IsAbstract)
                    {
                        continue;
                    }

                    allAbilities.Add(assemblyTypes[j]);
                }
            }
        }

        private void OnDisable()
        {
            EditorPrefs.SetInt("Weapon Section", (int)section);
            EditorPrefs.SetBool("Edit Weapon", editWeapon);
            EditorPrefs.SetInt("Selected Animation", selectedAnimator);
            EditorPrefs.SetInt("Selected Ability", selectedAbility);
        }

        public override void OnInspectorGUI()
        {
            DrawImageHeader();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            serializedObject.Update();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Properties", (section == WeaponSection.Properties) ? active : normal))
                section = (section == WeaponSection.Properties) ? WeaponSection.Noone : WeaponSection.Properties;

            if (GUILayout.Button("Animations", (section == WeaponSection.Animations) ? active : normal))
                section = (section == WeaponSection.Animations) ? WeaponSection.Noone : WeaponSection.Animations;

            if (GUILayout.Button("Events", (section == WeaponSection.Events) ? active : normal))
                section = (section == WeaponSection.Events) ? WeaponSection.Noone : WeaponSection.Events;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            switch (section)
            {
                case WeaponSection.Properties:
                    DrawProperties();
                    break;
                case WeaponSection.Animations:
                    DrawAnimations();
                    break;
                case WeaponSection.Events:
                    DrawEvents();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawProperties()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PropertyField(m_WeaponData);
            if (GUILayout.Button("Edit", (editWeapon) ? defaultPressed : EditorStyles.miniButton, GUILayout.Width(50)))
            {
                editWeapon = !editWeapon;
            }

            if (m_WeaponData.objectReferenceValue == null)
                editWeapon = false;

            EditorGUILayout.EndHorizontal();

            if (EditorGUILayout.BeginFadeGroup(m_CurrentFade))
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUI.indentLevel++;

                DrawByIterator(new SerializedObject(m_WeaponData.objectReferenceValue));

                EditorGUILayout.Space();
                EditorGUILayout.EndVertical();

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFadeGroup();

            m_CurrentFade = Mathf.MoveTowards(m_CurrentFade, editWeapon ? 1 : 0, 0.015f);
            m_CurrentFade = Mathf.Clamp(m_CurrentFade, 0, 1);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PropertyField(m_Inventory);
            if (GUILayout.Button("Edit", GUILayout.Width(50)))
                Selection.activeObject = m_Inventory.objectReferenceValue;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            DrawByIterator();
        }

        void DrawEvents()
        {
            EditorGUILayout.PropertyField(OnEquipWeaponEvent);
            EditorGUILayout.PropertyField(OnUnequipWeaponEvent);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(OnStartFireEvent);
            EditorGUILayout.PropertyField(OnFireEvent);
            EditorGUILayout.PropertyField(OnFinishFireEvent);
            EditorGUILayout.PropertyField(OnEmptyFireEvent);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(OnStartReloadEvent);
            EditorGUILayout.PropertyField(OnFinishReloadEvent);

            if ((target as WeaponController).WeaponData.FireType == WeaponFireType.HoldAndFire)
            {
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(OnShakeEvent);
            }
        }

        private float fillFade = 0;
        private bool fill;
        private string substate;
        void DrawAnimations()
        {
            bool isHolding = (target as WeaponController).WeaponData.FireType == WeaponFireType.HoldAndFire;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Auto fill
            if (GUILayout.Button("Auto Fill animations states", fill ? defaultPressed : EditorStyles.miniButton, GUILayout.Width(200), GUILayout.Height(20)))
                fill = !fill;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (EditorGUILayout.BeginFadeGroup(fillFade))
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal(contentSkin.box);

                substate = EditorGUILayout.TextField("Weapon Sub-State", substate);

                if (GUILayout.Button("Fill", GUILayout.Width(50)))
                {
                    fill = false;
                    AutoFill();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndFadeGroup();

            EditorGUILayout.Space();

            fillFade = Mathf.MoveTowards(fillFade, fill ? 1 : 0, 0.04f);
            fillFade = Mathf.Clamp(fillFade, 0, 1);

            selectedAnimator = EditorGUILayout.Popup("Select State to Edit", selectedAnimator, (isHolding) ? animatorOptions : animatorOptionsNoHolding);

            switch (selectedAnimator)
            {
                case 0:
                    DrawSelectedAnimator(EquipAnimator);
                    break;
                case 1:
                    DrawSelectedAnimator(UnequipAnimator);
                    break;
                case 2:
                    DrawSelectedAnimator(IdleAnimator);
                    break;
                case 3:
                    DrawSelectedAnimator(AimingAnimator);
                    break;
                case 4:
                    DrawSelectedAnimator(FireAnimator);
                    break;
                case 5:
                    DrawSelectedAnimator(ReloadAnimator);
                    break;
                case 6:
                    DrawSelectedAnimator(HoldingAnimator);
                    break;
                default:
                    break;
            }
        }

        private void AutoFill()
        {
            // EQUIP STATES
            SerializedProperty states = EquipAnimator.FindPropertyRelative("m_States");

            while (states.arraySize < 2)
                states.InsertArrayElementAtIndex(states.arraySize);

            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_StateName").stringValue = substate + ".Equip";
            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.RightArm;
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_StateName").stringValue = substate + ".Equip";
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.LeftArm;

            // UNEQUIP STATES
            states = UnequipAnimator.FindPropertyRelative("m_States");

            while (states.arraySize < 2)
                states.InsertArrayElementAtIndex(states.arraySize);

            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_StateName").stringValue = substate + ".Unequip";
            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.RightArm;
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_StateName").stringValue = substate + ".Unequip";
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.LeftArm;

            // IDLE STATES
            states = IdleAnimator.FindPropertyRelative("m_States");

            while (states.arraySize < 2)
                states.InsertArrayElementAtIndex(states.arraySize);

            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_StateName").stringValue = substate + ".Idle";
            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.RightArm;
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_StateName").stringValue = substate + ".Idle";
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.LeftArm;

            SerializedProperty abilityStates = IdleAnimator.FindPropertyRelative("m_AbilitiesStates");

            while (abilityStates.arraySize < 5)
                abilityStates.InsertArrayElementAtIndex(abilityStates.arraySize);

            abilityStates.GetArrayElementAtIndex(0).FindPropertyRelative("m_AbilityReference").stringValue = "CrouchAbility";

            states = abilityStates.GetArrayElementAtIndex(0).FindPropertyRelative("m_States");
            while (states.arraySize < 2)
                states.InsertArrayElementAtIndex(states.arraySize);

            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_StateName").stringValue = substate + ".Idle High";
            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.RightArm;
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_StateName").stringValue = substate + ".Idle High";
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.LeftArm;



            abilityStates.GetArrayElementAtIndex(1).FindPropertyRelative("m_AbilityReference").stringValue = "FallAbility";

            states = abilityStates.GetArrayElementAtIndex(1).FindPropertyRelative("m_States");
            while (states.arraySize < 2)
                states.InsertArrayElementAtIndex(states.arraySize);

            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_StateName").stringValue = substate + ".Idle High";
            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.RightArm;
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_StateName").stringValue = substate + ".Idle High";
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.LeftArm;



            abilityStates.GetArrayElementAtIndex(2).FindPropertyRelative("m_AbilityReference").stringValue = "VaultAbility";

            states = abilityStates.GetArrayElementAtIndex(2).FindPropertyRelative("m_States");
            while (states.arraySize < 2)
                states.InsertArrayElementAtIndex(states.arraySize);

            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_StateName").stringValue = substate + ".Idle High";
            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.RightArm;
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_StateName").stringValue = substate + ".Idle High";
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.LeftArm;



            abilityStates.GetArrayElementAtIndex(3).FindPropertyRelative("m_AbilityReference").stringValue = "LowerClimbAbility";

            states = abilityStates.GetArrayElementAtIndex(3).FindPropertyRelative("m_States");
            while (states.arraySize < 2)
                states.InsertArrayElementAtIndex(states.arraySize);

            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_StateName").stringValue = substate + ".Idle High";
            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.RightArm;
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_StateName").stringValue = substate + ".Idle High";
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.LeftArm;

            // Cover
            abilityStates.GetArrayElementAtIndex(4).FindPropertyRelative("m_AbilityReference").stringValue = "Cover";

            states = abilityStates.GetArrayElementAtIndex(4).FindPropertyRelative("m_States");
            while (states.arraySize < 2)
                states.InsertArrayElementAtIndex(states.arraySize);

            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_StateName").stringValue = substate + ".Idle High";
            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.RightArm;
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_StateName").stringValue = substate + ".Idle High";
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.LeftArm;

            // AIMING STATES
            states = AimingAnimator.FindPropertyRelative("m_States");

            while (states.arraySize < 2)
                states.InsertArrayElementAtIndex(states.arraySize);

            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_StateName").stringValue = substate + ".Strafe";
            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.Base;
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_StateName").stringValue = substate + ".Aiming";
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.Upper;

            // HOLDING STATES
            states = HoldingAnimator.FindPropertyRelative("m_States");

            while (states.arraySize < 2)
                states.InsertArrayElementAtIndex(states.arraySize);

            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_StateName").stringValue = substate + ".Strafe";
            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.Base;
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_StateName").stringValue = substate + ".Holding";
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.Upper;


            // FIRE STATES
            states = FireAnimator.FindPropertyRelative("m_States");

            while (states.arraySize < 2)
                states.InsertArrayElementAtIndex(states.arraySize);

            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_StateName").stringValue = substate + ".Strafe";
            states.GetArrayElementAtIndex(0).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.Base;
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_StateName").stringValue = substate + ".Fire";
            states.GetArrayElementAtIndex(1).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.Upper;

            // RELOAD STATES
            states = ReloadAnimator.FindPropertyRelative("m_States");
            if ((target as WeaponController).WeaponData.FireType == WeaponFireType.HoldAndFire)
            {
                while (states.arraySize < 2)
                    states.InsertArrayElementAtIndex(states.arraySize);

                states.GetArrayElementAtIndex(0).FindPropertyRelative("m_StateName").stringValue = substate + ".Strafe";
                states.GetArrayElementAtIndex(0).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.Base;
                states.GetArrayElementAtIndex(1).FindPropertyRelative("m_StateName").stringValue = substate + ".Reload";
                states.GetArrayElementAtIndex(1).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.Upper;
            }
            else
            {
                while (states.arraySize < 2)
                    states.InsertArrayElementAtIndex(states.arraySize);

                states.GetArrayElementAtIndex(0).FindPropertyRelative("m_StateName").stringValue = substate + ".Reload";
                states.GetArrayElementAtIndex(0).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.RightArm;
                states.GetArrayElementAtIndex(1).FindPropertyRelative("m_StateName").stringValue = substate + ".Reload";
                states.GetArrayElementAtIndex(1).FindPropertyRelative("m_Layer").intValue = (int)AnimatorLayer.LeftArm;
            }

        }


        void DrawSelectedAnimator(SerializedProperty animator)
        {
            SerializedProperty defaultStates = animator.FindPropertyRelative("m_States");

            GUIStyle label = headerSkin.label;
            label.normal.textColor = Color.black;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(animator.displayName, label);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.Label("Default States", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            DrawState(defaultStates);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.Label("Ability States", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            SerializedProperty abilityStates = animator.FindPropertyRelative("m_AbilitiesStates");
            string[] display = new string[abilityStates.arraySize];
            for (int i = 0; i < display.Length; i++)
            {
                display[i] = abilityStates.GetArrayElementAtIndex(i).FindPropertyRelative("m_AbilityReference").stringValue;
            }

            selectedAbility = EditorGUILayout.Popup("Select Ability to edit", selectedAbility, display);

            if (GUILayout.Button("+", EditorStyles.miniButtonLeft, GUILayout.Width(20)))
            {
                GenericMenu menu = new GenericMenu();
                foreach (Type ability in allAbilities)
                {
                    if ((target as MonoBehaviour).GetComponentInParent(ability) == null)
                        continue;

                    bool canAdd = true;
                    for (int j = 0; j < abilityStates.arraySize; j++)
                    {
                        if (abilityStates.GetArrayElementAtIndex(j).FindPropertyRelative("m_AbilityReference").stringValue == ability.Name)
                        {
                            canAdd = false;
                            break;
                        }
                    }

                    if (canAdd)
                        menu.AddItem(new GUIContent(ability.Name), false, AddAbility, ability.Name);
                }

                CurrentAbilityStates = abilityStates;
                menu.ShowAsContext();
            }
            if (GUILayout.Button("-", EditorStyles.miniButtonRight, GUILayout.Width(20)))
            {
                GenericMenu menu = new GenericMenu();

                for (int j = 0; j < abilityStates.arraySize; j++)
                    menu.AddItem(new GUIContent(abilityStates.GetArrayElementAtIndex(j).FindPropertyRelative("m_AbilityReference").stringValue),
                        false, RemoveAbility, j);

                CurrentAbilityStates = abilityStates;
                menu.ShowAsContext();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            selectedAbility = Mathf.Clamp(selectedAbility, 0, abilityStates.arraySize - 1);

            if (abilityStates.arraySize > 0)
                DrawState(abilityStates.GetArrayElementAtIndex(selectedAbility).FindPropertyRelative("m_States"));

            EditorGUILayout.EndVertical();
        }

        void DrawState(SerializedProperty targetState)
        {
            for (int i = 0; i < targetState.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                EditorGUILayout.BeginVertical(GUI.skin.box);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    int size = targetState.arraySize;
                    targetState.DeleteArrayElementAtIndex(i);
                    if (targetState.arraySize == size)
                        targetState.DeleteArrayElementAtIndex(i);

                    serializedObject.ApplyModifiedProperties();
                    EditorGUILayout.EndHorizontal();
                    return;
                }

                EditorGUILayout.EndHorizontal();

                SerializedProperty name = targetState.GetArrayElementAtIndex(i).FindPropertyRelative("m_StateName");
                SerializedProperty layer = targetState.GetArrayElementAtIndex(i).FindPropertyRelative("m_Layer");

                EditorGUILayout.PropertyField(name);
                EditorGUILayout.PropertyField(layer);

                EditorGUILayout.EndVertical();

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();
            }


            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Add Animation", EditorStyles.miniButton, GUILayout.Width(100), GUILayout.Height(18)))
            {
                targetState.InsertArrayElementAtIndex(targetState.arraySize);
                serializedObject.ApplyModifiedProperties();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        SerializedProperty CurrentAbilityStates = null;
        void AddAbility(object target)
        {
            CurrentAbilityStates.InsertArrayElementAtIndex(CurrentAbilityStates.arraySize);
            CurrentAbilityStates.GetArrayElementAtIndex(CurrentAbilityStates.arraySize - 1).FindPropertyRelative("m_AbilityReference").stringValue = target as string;
            selectedAbility = CurrentAbilityStates.arraySize - 1;

            serializedObject.ApplyModifiedProperties();
        }

        void RemoveAbility(object target)
        {
            int i = (int)target;
            int size = CurrentAbilityStates.arraySize;

            CurrentAbilityStates.DeleteArrayElementAtIndex(i);
            if (CurrentAbilityStates.arraySize == size)
                CurrentAbilityStates.DeleteArrayElementAtIndex(i);

            selectedAbility = (selectedAbility == size - 1) ? size - 2 : selectedAbility;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
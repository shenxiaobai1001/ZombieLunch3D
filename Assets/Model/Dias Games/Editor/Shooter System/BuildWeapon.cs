using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    public enum Hand { Right, Left }

    public class BuildWeapon : EditorWindow
    {
        GameObject character, weapon; // Property to set character
        Weapon weaponData;
        InventoryData inventoryData;
        Hand hand = Hand.Right;

        #region style
        protected Texture2D icon;
        protected GUISkin headerSkin;
        protected GUISkin contentSkin;

        protected string underLabel = "Shooter System";
        protected string label = "";

        protected GUIStyle normal;
        protected GUIStyle active;
        protected virtual void OnEnable()
        {
            icon = (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/Dias Games/Editor/GUI/tps_icon.psd", typeof(Texture2D));
            headerSkin = Resources.Load("HeaderSkin") as GUISkin;
            contentSkin = Resources.Load("ContentSkin") as GUISkin;

            label = "Weapon Builder";

            normal = new GUIStyle(contentSkin.button);
            active = new GUIStyle(contentSkin.button);
            active.normal.background = active.active.background;
        }
        #endregion

        [MenuItem("Dias Games/Shooter System/Build Weapon")]
        public static void ShowWindow()
        {
            GetWindow<BuildWeapon>(true, "Weapon Builder");
        }


        protected void DrawImageHeader()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(headerSkin.box);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(icon);

            GUILayout.Label(label, headerSkin.label);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);

            // Set label style for header
            GUIStyle m_Style = new GUIStyle(EditorStyles.label);
            m_Style.wordWrap = true;
            m_Style.fontStyle = FontStyle.Bold;

            DrawImageHeader();
            EditorGUILayout.Space();

            // Draw GUI label and fields
            GUILayout.Label("This window will help you to create a Weapon for Character", m_Style);

            character = EditorGUILayout.ObjectField("Character", character, typeof(GameObject), true) as GameObject;
            if (character == null)
            {
                EditorGUILayout.HelpBox("Please select a character to add weapon", MessageType.Error, false);
            }

            weapon = EditorGUILayout.ObjectField("Weapon Object", weapon, typeof(GameObject), true) as GameObject;
            if (weapon == null)
            {
                EditorGUILayout.HelpBox("Please select a Weapon Game Object to build.", MessageType.Error, false);
            }

            weaponData = EditorGUILayout.ObjectField("Weapon Data", weaponData, typeof(Weapon), true) as Weapon;
            if (weaponData == null)
            {
                EditorGUILayout.HelpBox("Please select a Weapon Data reference.", MessageType.Error, false);
            }

            inventoryData = EditorGUILayout.ObjectField("Inventory Data", inventoryData, typeof(InventoryData), true) as InventoryData;
            if (inventoryData == null)
            {
                EditorGUILayout.HelpBox("Please select a Inventory", MessageType.Error, false);
            }

            EditorGUILayout.Space();

            hand = (Hand)EditorGUILayout.EnumPopup("Hand to add Weapon",hand);

            EditorGUILayout.Space();

            if (GUILayout.Button("Build Weapon") && character != null && weapon != null)
            {
                FindAndAttachToSocket();
                CreateWeponScripts();
                Close();
            }
            ///

        }

        private void FindAndAttachToSocket()
        {
            Animator m_Animator = character.GetComponent<Animator>();
            HumanBodyBones handSocket = (hand == Hand.Right) ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
            GameObject socket = (m_Animator.GetBoneTransform(handSocket).Find("WeaponSocket") == null) ? 
                null : m_Animator.GetBoneTransform(handSocket).Find("WeaponSocket").gameObject;

            if(socket == null)
            {
                socket = new GameObject("WeaponSocket");
                socket.transform.parent = m_Animator.GetBoneTransform(handSocket);
                socket.transform.localPosition = Vector3.zero;
                socket.transform.localRotation = Quaternion.identity;
                socket.transform.localScale = Vector3.one;
            }

            weapon.transform.parent = socket.transform;
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
        }

        private void CreateWeponScripts()
        {
            WeaponController controller = weapon.GetComponent<WeaponController>();
            if (controller == null)
                controller = Undo.AddComponent<WeaponController>(weapon);

            AudioSource audio = weapon.GetComponent<AudioSource>();
            if (audio == null)
                audio = Undo.AddComponent<AudioSource>(weapon);

            // Create fire point
            GameObject firePoint = (weapon.transform.Find("Fire Point") == null) ? null : weapon.transform.Find("Fire Point").gameObject;

            if (firePoint == null)
            {
                firePoint = new GameObject("Fire Point");
                firePoint.transform.parent = weapon.transform;
                firePoint.transform.localPosition = Vector3.zero;
                firePoint.transform.localRotation = Quaternion.identity;
                firePoint.transform.localScale = Vector3.one;
            }
            //

            SerializedObject obj = new SerializedObject(controller);

            SerializedProperty dataProperty = obj.FindProperty("m_WeaponData");
            SerializedProperty firepointProperty = obj.FindProperty("m_FirePoint");
            SerializedProperty inventoryProperty = obj.FindProperty("m_Inventory");

            dataProperty.objectReferenceValue = weaponData;
            firepointProperty.objectReferenceValue = firePoint.transform;
            inventoryProperty.objectReferenceValue = inventoryData;

            obj.ApplyModifiedProperties();

            audio.spatialBlend = 1;
            audio.playOnAwake = false;
        }
    }
}
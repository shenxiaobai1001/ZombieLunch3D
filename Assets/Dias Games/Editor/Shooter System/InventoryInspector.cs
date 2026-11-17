using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(InventoryData))]
    public class InventoryInspector : BaseInspector
    {
        SerializedProperty BulletsInventory;
        SerializedProperty Weapons;

        protected override void OnEnable()
        {
            base.OnEnable();
            BulletsInventory = serializedObject.FindProperty("Bullets");
            Weapons = serializedObject.FindProperty("Weapons");
        }

        protected override void FormatLabel()
        {
            label = "Shooter Inventory";
        }
        
        public override void OnInspectorGUI()
        {
            DrawImageHeader();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUILayout.Space();

            serializedObject.Update();
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(contentSkin.box);

            EditorGUILayout.Space();
            DrawWeapons();
            EditorGUILayout.Space();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(contentSkin.box);

            EditorGUILayout.Space();
            DrawBullets();
            EditorGUILayout.Space();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }

        protected void DrawBullets()
        {
            EditorGUILayout.BeginVertical();
            
            GUIStyle label = headerSkin.label;
            label.normal.textColor = Color.black;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Bullets", label);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("Bullet", EditorStyles.boldLabel, GUILayout.Width(250));
            GUILayout.Label("Amount", EditorStyles.boldLabel, GUILayout.Width(75));

            EditorGUILayout.EndHorizontal();

            // Draw bullets
            for (int i = 0; i < BulletsInventory.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();

                SerializedProperty bullet = BulletsInventory.GetArrayElementAtIndex(i).FindPropertyRelative("m_Bullet");
                EditorGUILayout.ObjectField(bullet, typeof(Bullet), new GUIContent(), GUILayout.Width(250));
                EditorGUILayout.PropertyField(BulletsInventory.GetArrayElementAtIndex(i).FindPropertyRelative("Amount"),
                    new GUIContent(""));

                if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    BulletsInventory.DeleteArrayElementAtIndex(i);
                    continue;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Add Bullet", EditorStyles.miniButton))
            {
                GenericMenu m_Menu = new GenericMenu();
                string[] assets = AssetDatabase.FindAssets("t:Bullet");
                foreach (string asset in assets)
                {
                    string path = AssetDatabase.GUIDToAssetPath(asset);
                    Bullet ammo = AssetDatabase.LoadAssetAtPath<Bullet>(path);
                    bool found = false;

                    for(int i=0; i < BulletsInventory.arraySize; i++)
                    {
                        if(BulletsInventory.GetArrayElementAtIndex(i).FindPropertyRelative("m_Bullet").objectReferenceValue == ammo)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        m_Menu.AddItem(new GUIContent(ammo.name), false, AddBullet, ammo);
                }

                m_Menu.ShowAsContext();
            }

            EditorGUILayout.EndVertical();
        }

        protected void DrawWeapons()
        {
            EditorGUILayout.BeginVertical();

            GUIStyle label = headerSkin.label;
            label.normal.textColor = Color.black;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Weapons", label);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();


            // Draw weapons
            for (int i = 0; i < Weapons.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.PropertyField(Weapons.GetArrayElementAtIndex(i), new GUIContent(Weapons.GetArrayElementAtIndex(i).objectReferenceValue.name));

                if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    Weapons.DeleteArrayElementAtIndex(i);
                    if (Weapons.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        Weapons.DeleteArrayElementAtIndex(i);

                    continue;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Add Weapon", EditorStyles.miniButton))
            {
                GenericMenu m_Menu = new GenericMenu();
                string[] assets = AssetDatabase.FindAssets("t:Weapon");
                foreach (string asset in assets)
                {
                    string path = AssetDatabase.GUIDToAssetPath(asset);
                    Weapon assetWeapon = AssetDatabase.LoadAssetAtPath<Weapon>(path);
                    bool alreadyExists = false;
                    //if (!inventory.Weapons.Contains(assetWeapon))
                    for (int i = 0; i < Weapons.arraySize; i++)
                    {
                        if (Weapons.GetArrayElementAtIndex(i).objectReferenceValue == assetWeapon)
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    if (!alreadyExists)
                        m_Menu.AddItem(new GUIContent(assetWeapon.name), false, AddWeapon, assetWeapon);
                }

                m_Menu.ShowAsContext();
            }

            EditorGUILayout.EndVertical();
        }

        void AddBullet(object item)
        {
            Bullet bullet = item as Bullet;
            int i = BulletsInventory.arraySize;

            BulletsInventory.arraySize++;
            BulletsInventory.GetArrayElementAtIndex(i).FindPropertyRelative("m_Bullet").objectReferenceValue = bullet;
            BulletsInventory.GetArrayElementAtIndex(i).FindPropertyRelative("Amount").intValue = 0;

            serializedObject.ApplyModifiedProperties();
        }

        void AddWeapon(object weapon)
        {
            Weapons.arraySize++;
            Weapons.GetArrayElementAtIndex(Weapons.arraySize - 1).objectReferenceValue = weapon as Weapon;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
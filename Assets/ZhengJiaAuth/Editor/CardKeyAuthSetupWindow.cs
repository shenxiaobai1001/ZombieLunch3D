using UnityEditor;
using UnityEngine;

namespace CardKeyAuth.Editor
{
    /// <summary>一键生成 Resources 配置并提示业务侧如何鉴权。</summary>
    public sealed class CardKeyAuthSetupWindow : EditorWindow
    {
        CardKeyAuthSettings _settings;
        string _status = "";

        [MenuItem("CardKey Auth/接入向导", false, 0)]
        public static void Open()
        {
            var win = GetWindow<CardKeyAuthSetupWindow>(true, "CardKey Auth 接入向导", true);
            win.minSize = new Vector2(480, 420);
            win.Show();
        }

        [MenuItem("CardKey Auth/选中配置资产", false, 1)]
        public static void PingSettings()
        {
            var s = EnsureSettingsAsset(createIfMissing: false);
            if (s == null)
            {
                EditorUtility.DisplayDialog("CardKey Auth", "还没有配置，请先打开「接入向导」。", "好的");
                return;
            }

            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
        }

        void OnEnable()
        {
            _settings = EnsureSettingsAsset(createIfMissing: true);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("正佳卡密鉴权 · 接入指南", EditorStyles.boldLabel);

            if (_settings == null)
            {
                if (GUILayout.Button("创建配置资产", GUILayout.Height(32)))
                    _settings = EnsureSettingsAsset(createIfMissing: true);
                return;
            }

            EditorGUI.BeginChangeCheck();
            var so = new SerializedObject(_settings);
            so.Update();
            DrawProp(so, "AutoStart");
            DrawProp(so, "BaseUrl");
            DrawProp(so, "ClientSignSecret");
            DrawProp(so, "GameName");
            DrawProp(so, "GameId");
            DrawProp(so, "ClientVer");
            DrawProp(so, "BodyEncryption");
            DrawProp(so, "AutoHeartbeat");
            DrawProp(so, "QuitOnIntegrityFail");
            DrawProp(so, "RememberCardKey");
            so.ApplyModifiedProperties();
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(_settings);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("业务侧鉴权（复制到功能入口）", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(
                "if (!CardKeyAuth.CardKeyGate.TryAuthorize(\"功能名\", out var deny))\n{\n    Debug.LogWarning(deny);\n    return;\n}",
                GUILayout.MinHeight(72));

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("保存并选中配置", GUILayout.Height(28)))
                {
                    AssetDatabase.SaveAssets();
                    Selection.activeObject = _settings;
                    EditorGUIUtility.PingObject(_settings);
                    _status = "已保存：Assets/Resources/CardKeyAuthSettings.asset";
                }

                if (GUILayout.Button("在场景预览运行时", GUILayout.Height(28)))
                {
                    if (!Application.isPlaying)
                    {
                        _status = "请先进入 Play Mode";
                    }
                    else
                    {
                        CardKeyAuthBootstrap.EnsureRuntime(_settings);
                        _status = "已确保 CardKeyAuth 运行时存在";
                    }
                }
            }

            if (!_settings.IsConfigured())
                EditorGUILayout.HelpBox("请填写 ClientSignSecret，以及 GameName 或 GameId。", MessageType.Warning);
            else
                EditorGUILayout.HelpBox("配置完整。进 Play 会自动弹出登录窗。", MessageType.None);

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.LabelField(_status, EditorStyles.wordWrappedLabel);
        }

        static void DrawProp(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            if (p != null)
                EditorGUILayout.PropertyField(p, true);
        }

        public static CardKeyAuthSettings EnsureSettingsAsset(bool createIfMissing)
        {
            const string resourcesDir = "Assets/Resources";
            const string assetPath = resourcesDir + "/CardKeyAuthSettings.asset";

            var existing = AssetDatabase.LoadAssetAtPath<CardKeyAuthSettings>(assetPath);
            if (existing != null)
                return existing;

            // 兼容已放在别处的同名资产
            var guids = AssetDatabase.FindAssets("t:CardKeyAuthSettings");
            if (guids != null && guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<CardKeyAuthSettings>(path);
            }

            if (!createIfMissing)
                return null;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var created = ScriptableObject.CreateInstance<CardKeyAuthSettings>();
            created.AutoStart = true;
            created.BaseUrl = CardKeyAuthConfig.DefaultAuthBaseUrl;
            AssetDatabase.CreateAsset(created, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return created;
        }
    }
}

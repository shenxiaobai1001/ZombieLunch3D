using UnityEngine;

namespace CardKeyAuth
{
    /// <summary>
    /// 自动挂载鉴权运行时，业务工程无需再写 Bootstrap。
    /// </summary>
    public static class CardKeyAuthBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoad()
        {
            var settings = CardKeyAuthSettings.Load();
            if (settings != null && !settings.AutoStart)
                return;

            // 无 Settings 时仍启动（方便先看到登录窗）；正式接入请用向导生成配置
            EnsureRuntime(settings);
        }

        public static CardKeyAuthRuntime EnsureRuntime(CardKeyAuthSettings settings = null)
        {
            if (CardKeyAuthRuntime.Instance != null)
            {
                if (settings != null)
                    CardKeyAuthRuntime.Instance.ApplySettings(settings);
                return CardKeyAuthRuntime.Instance;
            }

            var existing = Object.FindObjectOfType<CardKeyAuthRuntime>();
            if (existing != null)
            {
                if (settings != null)
                    existing.ApplySettings(settings);
                return existing;
            }

            var go = new GameObject("CardKeyAuth");
            Object.DontDestroyOnLoad(go);
            var config = go.AddComponent<CardKeyAuthConfig>();
            var runtime = go.AddComponent<CardKeyAuthRuntime>();
            if (settings != null)
                runtime.ApplySettings(settings);
            else
                config.BaseUrl = CardKeyAuthConfig.DefaultAuthBaseUrl;
            return runtime;
        }
    }
}

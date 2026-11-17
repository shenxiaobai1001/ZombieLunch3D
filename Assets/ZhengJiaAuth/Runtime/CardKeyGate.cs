using UnityEngine;

namespace CardKeyAuth
{
    /// <summary>
    /// 对外一句话鉴权入口，业务侧只需调用这里。
    /// </summary>
    public static class CardKeyGate
    {
        /// <summary>功能是否已授权。未登录会自动弹出登录窗。</summary>
        public static bool TryAuthorize(string featureName, out string denyReason)
        {
            denyReason = null;

            // AutoStart 关闭 = 开发期整系统旁路（不弹登录、不拦功能）
            var settings = CardKeyAuthSettings.Load();
            if (settings != null && !settings.AutoStart)
                return true;

            var runtime = CardKeyAuthRuntime.Instance;
            if (runtime == null)
            {
                denyReason = "鉴权未初始化（请用菜单 CardKey Auth / 接入向导）";
                Debug.LogWarning("[CardKey] " + denyReason);
                return false;
            }

            return runtime.TryAuthorizeFeature(featureName, out denyReason);
        }

        /// <summary>同 TryAuthorize，忽略拒绝原因。</summary>
        public static bool Allow(string featureName)
        {
            return TryAuthorize(featureName, out _);
        }

        public static bool IsLoggedIn =>
            CardKeyAuthRuntime.Instance != null && CardKeyAuthRuntime.Instance.IsLoggedIn;

        public static bool IsLoginVisible =>
            CardKeyAuthRuntime.Instance != null
            && CardKeyAuthRuntime.Instance.Overlay != null
            && CardKeyAuthRuntime.Instance.Overlay.IsVisible;
    }
}

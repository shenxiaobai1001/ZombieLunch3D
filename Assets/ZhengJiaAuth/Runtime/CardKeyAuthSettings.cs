using UnityEngine;

namespace CardKeyAuth
{
    /// <summary>
    /// 工程级鉴权配置（放 Resources 下，名为 CardKeyAuthSettings）。
    /// 用菜单「CardKey Auth / 接入向导」一键生成。
    /// </summary>
    [CreateAssetMenu(fileName = "CardKeyAuthSettings", menuName = "CardKey Auth/Settings", order = 0)]
    public sealed class CardKeyAuthSettings : ScriptableObject
    {
        public const string ResourcesName = "CardKeyAuthSettings";

        [Header("启动")]
        [Tooltip("关闭后整系统旁路：不弹登录、不拦功能（开发期可关；上线前务必打开）")]
        public bool AutoStart = true;

        [Header("鉴权服")]
        [Tooltip("留空则用默认 https://jmapi.zhengjia.live")]
        public string BaseUrl = CardKeyAuthConfig.DefaultAuthBaseUrl;

        [Tooltip("与服务端 CLIENT_SIGN_SECRET 一致")]
        public string ClientSignSecret = "ADJAZZmHtGSA0XhH5ArpKSXC4sa7K0wS";

        [Header("游戏标识（优先 GameName）")]
        [Tooltip("须与后台游戏显示名完全一致")]
        public string GameName = "";

        [Tooltip("GameName 为空时才用")]
        public string GameId = "";

        [Header("客户端")]
        public string ClientVer = "1.0.0";
        public bool BodyEncryption = true;

        [Header("策略")]
        public bool AutoHeartbeat = true;
        public bool QuitOnIntegrityFail = true;
        public bool RememberCardKey = true;

        public static CardKeyAuthSettings Load()
        {
            return Resources.Load<CardKeyAuthSettings>(ResourcesName);
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(ClientSignSecret)
                   && (!string.IsNullOrWhiteSpace(GameName) || !string.IsNullOrWhiteSpace(GameId));
        }
    }
}

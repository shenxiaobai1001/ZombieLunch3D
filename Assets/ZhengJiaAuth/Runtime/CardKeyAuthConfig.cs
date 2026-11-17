using System;
using UnityEngine;

namespace CardKeyAuth
{
    /// <summary>卡密鉴权运行时配置（服务器地址写死，不在登录界面展示）。</summary>
    public sealed class CardKeyAuthConfig : MonoBehaviour
    {
        public const string DefaultAuthBaseUrl = "https://jmapi.zhengjia.live/";

        [HideInInspector]
        public string BaseUrl = DefaultAuthBaseUrl;

        [Tooltip("与服务端 CLIENT_SIGN_SECRET 一致（仅用于登录/心跳请求 HMAC）")]
        public string ClientSignSecret = "ADJAZZmHtGSA0XhH5ArpKSXC4sa7K0wS";

        [Header("游戏标识（二选一，优先 GameName）")]
        [Tooltip("后台游戏显示名；须与管理端完全一致，不在登录界面展示")]
        public string GameName = "";

        [Tooltip("兼容字段；GameName 为空时才用数字 game_id")]
        public string GameId = "";

        [Header("客户端")]
        public string ClientVer = "1.0.0";
        public bool BodyEncryption = true;

        [Header("策略")]
        [Tooltip("登录成功后自动挂心跳")]
        public bool AutoHeartbeat = true;

        [Tooltip("完整性失败时退出进程")]
        public bool QuitOnIntegrityFail = true;

        [Tooltip("记住卡密并自动填入（不自动登录）")]
        public bool RememberCardKey = true;

        const string PrefCardKey = "ck_card_key";
        const string PrefExpireAt = "ck_card_expire_at";

        public string ResolveBaseUrl()
        {
            var url = string.IsNullOrWhiteSpace(BaseUrl) ? DefaultAuthBaseUrl : BaseUrl.Trim();
            if (!url.EndsWith("/"))
                url += "/";
            // CardKeyAuthClient 会 TrimEnd('/')，这里统一带斜杠仅作规范
            return url.TrimEnd('/');
        }

        public string LoadSavedCardKey()
        {
            if (!RememberCardKey) return "";
            return PlayerPrefs.GetString(PrefCardKey, "");
        }

        public void SaveCardKey(string key)
        {
            if (!RememberCardKey) return;
            PlayerPrefs.SetString(PrefCardKey, key ?? "");
            PlayerPrefs.Save();
        }

        public long LoadSavedExpireAt()
        {
            if (!RememberCardKey) return 0;
            var s = PlayerPrefs.GetString(PrefExpireAt, "0");
            return long.TryParse(s, out var v) ? v : 0;
        }

        public void SaveExpireAt(long expireAt)
        {
            if (!RememberCardKey) return;
            PlayerPrefs.SetString(PrefExpireAt, expireAt.ToString());
            PlayerPrefs.Save();
        }

        public void ClearSavedCardKey()
        {
            PlayerPrefs.DeleteKey(PrefCardKey);
            PlayerPrefs.DeleteKey(PrefExpireAt);
            PlayerPrefs.Save();
        }

        /// <summary>到期：yyyy-MM-dd HH:mm（本地时区）</summary>
        public static string FormatExpireAtDisplayText(long expireAtUnix)
        {
            if (expireAtUnix <= 0) return "";
            var local = DateTimeOffset.FromUnixTimeSeconds(expireAtUnix).ToLocalTime();
            return $"到期：{local:yyyy-MM-dd HH:mm}";
        }

        public void ApplyFromSettings(CardKeyAuthSettings settings)
        {
            if (settings == null) return;
            BaseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
                ? DefaultAuthBaseUrl
                : settings.BaseUrl;
            ClientSignSecret = settings.ClientSignSecret ?? "";
            GameName = settings.GameName ?? "";
            GameId = settings.GameId ?? "";
            ClientVer = string.IsNullOrWhiteSpace(settings.ClientVer) ? "1.0.0" : settings.ClientVer;
            BodyEncryption = settings.BodyEncryption;
            AutoHeartbeat = settings.AutoHeartbeat;
            QuitOnIntegrityFail = settings.QuitOnIntegrityFail;
            RememberCardKey = settings.RememberCardKey;
        }
    }
}

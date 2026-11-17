using System;
using System.Threading.Tasks;
using UnityEngine;

namespace CardKeyAuth
{
    /// <summary>
    /// 卡密鉴权运行时：悬浮登录门禁、会话持有、心跳、整蛊入口鉴权。
    /// </summary>
    public sealed class CardKeyAuthRuntime : MonoBehaviour
    {
        public static CardKeyAuthRuntime Instance { get; private set; }

        public CardKeyAuthConfig Config { get; private set; }
        public CardKeyAuthClient Client { get; private set; }
        public CardKeyLoginOverlay Overlay { get; private set; }
        public bool IsLoggedIn => Client != null && !string.IsNullOrEmpty(Client.SessionToken);

        CardKeyHeartbeatRunner _heartbeat;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Config = GetComponent<CardKeyAuthConfig>();
            if (Config == null)
                Config = gameObject.AddComponent<CardKeyAuthConfig>();

            var settings = CardKeyAuthSettings.Load();
            if (settings != null)
                Config.ApplyFromSettings(settings);
            else
                Config.BaseUrl = CardKeyAuthConfig.DefaultAuthBaseUrl;

            Overlay = GetComponent<CardKeyLoginOverlay>();
            if (Overlay == null)
                Overlay = gameObject.AddComponent<CardKeyLoginOverlay>();
            Overlay.Bind(this);

            RebuildClient();

            if (!IsLoggedIn)
                Overlay.Show();
            else
                Overlay.Hide();
        }

        /// <summary>运行中应用工程配置（接入向导 / Bootstrap 调用）。</summary>
        public void ApplySettings(CardKeyAuthSettings settings)
        {
            if (settings == null) return;
            if (Config == null)
                Config = GetComponent<CardKeyAuthConfig>() ?? gameObject.AddComponent<CardKeyAuthConfig>();
            Config.ApplyFromSettings(settings);
            if (!IsLoggedIn)
                RebuildClient();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Client?.ClearSession();
        }

        public void RebuildClient()
        {
            Client?.ClearSession();
            Client = new CardKeyAuthClient(
                Config.ResolveBaseUrl(),
                Config.ClientSignSecret,
                CardKeyAuthClient.DefaultDeviceId(),
                gameName: string.IsNullOrWhiteSpace(Config.GameName) ? null : Config.GameName,
                gameId: string.IsNullOrWhiteSpace(Config.GameId) ? null : Config.GameId,
                clientVer: Config.ClientVer,
                bodyEncryption: Config.BodyEncryption);
            if (_heartbeat != null)
                _heartbeat.Bind(Client);
        }

        /// <summary>
        /// 整蛊/弹幕触发前调用：不要求鉴权时直接放行；否则本地验 grant+lease。
        /// </summary>
        public bool TryAuthorizeFeature(string featureName, out string denyReason)
        {
            denyReason = null;
            if (Config == null)
                return true;
            if (Client == null || !IsLoggedIn)
            {
                denyReason = "未登录卡密";
                Overlay?.Show();
                return false;
            }
            if (!Client.TryGetPreauthorizedFeature(featureName, out _))
            {
                denyReason = "功能未授权或租约失效: " + featureName;
                return false;
            }
            return true;
        }

        public async Task LoginFromOverlayAsync(string cardKey)
        {
            if (Overlay != null && Overlay.IsBusy) return;
            Overlay?.SetBusy(true);
            Overlay?.SetStatus("正在验证…", false);
            try
            {
                Config.BaseUrl = CardKeyAuthConfig.DefaultAuthBaseUrl;
                RebuildClient();
                CardKeyAuthClient.RiskGateResponse gate = null;
                try
                {
                    gate = await Client.RiskGateAsync();
                    if (gate != null)
                        RiskWarnStore.ApplyServerReset(gate.risk_warn_reset_at);
                    if (gate != null && gate.blocked)
                    {
                        Overlay?.SetBusy(false);
                        Overlay?.SetStatus("检测到风险环境，请联系管理员解除", true);
                        return;
                    }
                }
                catch (Exception gateEx)
                {
                    Debug.LogWarning("[CardKey] risk gate failed: " + gateEx.Message);
                }

                var hits = RiskProcessScanner.Scan();
                if (hits != null && hits.Count > 0)
                {
                    if (gate != null && gate.whitelisted)
                    {
                        Debug.Log("[CardKey] risk gate whitelisted, skip local warn");
                    }
                    else
                    {
                        int count = RiskWarnStore.Increment();
                        string apps = string.Join(",", hits);
                        int max = gate != null && gate.risk_warn_max >= 1
                            ? gate.risk_warn_max
                            : RiskWarnStore.DefaultMax;
                        if (count >= max)
                        {
                            Overlay?.SetStatus("风险环境超限，正在上报…", true);
                            try
                            {
                                var report = await Client.ReportRiskAsync(apps, count, cardKey);
                                if (report != null && (report.whitelisted || report.skipped))
                                {
                                    // 上报瞬间被加白：放行登录
                                }
                                else
                                {
                                    Overlay?.SetBusy(false);
                                    Overlay?.SetStatus("检测到风险环境，请联系管理员解除", true);
                                    return;
                                }
                            }
                            catch (Exception reportEx)
                            {
                                Debug.LogWarning("[CardKey] risk report: " + reportEx.Message);
                                Overlay?.SetBusy(false);
                                Overlay?.SetStatus(
                                    CardKeyLoginOverlay.LocalizeError(reportEx.Message),
                                    true);
                                return;
                            }
                        }
                        else
                        {
                            // 未超限：累计次数并禁止本次登录（对齐 UE 取消确认后的文案）
                            Debug.Log($"[CardKey] risk warn {count}/{max}: {apps}");
                            Overlay?.SetBusy(false);
                            Overlay?.SetStatus("已取消登录，请关闭风险软件后重试", true);
                            return;
                        }
                    }
                }

                await LoginAsync(cardKey);
                string okMsg = "验证成功，正在进入…";
                string expire = CardKeyAuthConfig.FormatExpireAtDisplayText(Client?.CardExpireAt ?? 0);
                if (!string.IsNullOrEmpty(expire))
                    okMsg = "验证成功，" + expire;
                Overlay?.SetStatus(okMsg, false);
                Overlay?.PlaySuccessAndHide();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CardKey] login failed raw: " + ex.Message);
                Overlay?.SetBusy(false);
                Overlay?.SetStatus(CardKeyLoginOverlay.LocalizeError(ex.Message), true);
            }
        }

        public async Task LoginAsync(string cardKey)
        {
            Config.BaseUrl = CardKeyAuthConfig.DefaultAuthBaseUrl;
            RebuildClient();
            var resp = await Client.LoginAsync(cardKey);
            Config.SaveCardKey(cardKey);
            Config.SaveExpireAt(Client.CardExpireAt);
            EnsureHeartbeat();
            Debug.Log($"[CardKey] login ok session={resp.session_id} features={Client.PreauthorizedFeatures.Count}");
        }

        public void Logout()
        {
            Client?.ClearSession();
            Overlay?.Show();
        }

        void EnsureHeartbeat()
        {
            if (!Config.AutoHeartbeat) return;
            if (_heartbeat == null)
            {
                _heartbeat = gameObject.GetComponent<CardKeyHeartbeatRunner>();
                if (_heartbeat == null)
                    _heartbeat = gameObject.AddComponent<CardKeyHeartbeatRunner>();
            }
            _heartbeat.QuitOnIntegrityFail = Config.QuitOnIntegrityFail;
            _heartbeat.Bind(Client);
        }
    }
}

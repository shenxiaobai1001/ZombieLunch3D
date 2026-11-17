using System;
using System.Threading.Tasks;
using UnityEngine;

namespace CardKeyAuth
{
    /// <summary>
    /// 定时心跳：续租 + 风险进程扫描 + 缺页探针处理。
    /// </summary>
    public sealed class CardKeyHeartbeatRunner : MonoBehaviour
    {
        public float IntervalSec = 20f;
        public bool UseServerInterval = true;
        public bool QuitOnIntegrityFail = true;

        CardKeyAuthClient _client;
        float _elapsed;
        bool _busy;

        public void Bind(CardKeyAuthClient client)
        {
            _client = client;
            _elapsed = 0f;
            _busy = false;
        }

        async void Update()
        {
            if (_busy || _client == null || string.IsNullOrEmpty(_client.SessionToken))
                return;
            _elapsed += Time.unscaledDeltaTime;
            var effectiveInterval = UseServerInterval && _client.HeartbeatIntervalSec >= 5
                ? _client.HeartbeatIntervalSec
                : IntervalSec;
            if (_elapsed < effectiveInterval)
                return;
            _elapsed = 0f;
            _busy = true;
            try
            {
                // 运行期扫描：登录后再开风险软件也会抓到
                var hits = RiskProcessScanner.Scan();
                if (hits != null && hits.Count > 0)
                {
                    await HandleRiskAppsAsync(string.Join(",", hits));
                    return;
                }

                await _client.HeartbeatAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("heartbeat failed: " + ex.Message);
                var msg = ex.Message ?? "";
                if (msg.IndexOf("MEMORY_PROBE", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    await HandleRiskAppsAsync("memory_probe");
                }
                else if (QuitOnIntegrityFail &&
                    (msg.Contains("INTEGRITY_FAIL") || msg.Contains("CLIENT_OUTDATED")))
                {
                    Application.Quit();
                }
                else
                {
                    KickToLogin(CardKeyLoginOverlay.LocalizeError(msg));
                }
            }
            finally
            {
                _busy = false;
            }
        }

        async Task HandleRiskAppsAsync(string riskAppsCsv)
        {
            try
            {
                var key = CardKeyAuthRuntime.Instance != null &&
                          CardKeyAuthRuntime.Instance.Config != null
                    ? CardKeyAuthRuntime.Instance.Config.LoadSavedCardKey()
                    : null;
                var report = await _client.ReportRiskAsync(riskAppsCsv, 99, key);
                if (report != null && (report.whitelisted || report.skipped))
                {
                    Debug.Log("[CardKey] risk report skipped (whitelist), keep session");
                    return;
                }
            }
            catch (Exception reportEx)
            {
                Debug.LogWarning("[CardKey] risk report: " + reportEx.Message);
                // 服务端 403 RISK_IP_BLOCKED 会进这里，继续踢登录
            }

            KickToLogin("检测到风险环境，请联系管理员解除");
        }

        void KickToLogin(string status)
        {
            _client?.ClearSession();
            var runtime = CardKeyAuthRuntime.Instance;
            if (runtime != null && runtime.Overlay != null)
            {
                runtime.Overlay.Show();
                runtime.Overlay.SetStatus(status, true);
            }
        }
    }
}

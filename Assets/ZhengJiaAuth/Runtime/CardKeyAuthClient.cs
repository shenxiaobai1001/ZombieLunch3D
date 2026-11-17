using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace CardKeyAuth
{
    [Serializable]
    public class FeatureItem
    {
        public string feature_id;
        public string name;
    }

    [Serializable]
    public class PreauthorizedFeature
    {
        public string feature_id;
        public string name;
    }

    [Serializable]
    class SessionGrantPayload
    {
        public int v;
        public string session_id;
        public string game_id;
        public string device_id;
        public long issued_at;
        public PreauthorizedFeature[] features;
    }

    [Serializable]
    public class GameInfo
    {
        public int game_id;
        public string name;
        public string description;
        public string status;
    }

    [Serializable]
    public class LoginResponse
    {
        public string session_id;
        public string session_token;
        /// <summary>会话过期时间（Unix 秒）。心跳会刷新，默认约 1 天，不是卡密到期。</summary>
        public long expire_at;
        /// <summary>卡密到期时间（Unix 秒）。展示剩余时长请用此字段。</summary>
        public long card_expire_at;
        public bool bind_device;
        public FeatureItem[] features;
        public CardInfo card;
        public GameInfo game;
        public string session_grant;
        public string session_grant_sign;
        public string session_grant_alg;
        public string session_grant_kid;
        public long lease_expire_at;
        public string lease_sign;
        public string lease_alg;
        public string lease_kid;
        public int heartbeat_interval_sec;
    }

    [Serializable]
    public class CardInfo
    {
        public string card_id;
        public string game_id;
        public string status;
        public long expire_at;
    }

    [Serializable]
    public class HeartbeatResponse
    {
        public bool ok;
        public long expire_at;
        public bool force_relogin;
        public bool force_update;
        public string min_client_ver;
        public string card_status;
        public long card_expire_at;
        public long lease_expire_at;
        public string lease_sign;
        public string lease_alg;
        public string lease_kid;
        public int heartbeat_interval_sec;
    }

    [Serializable]
    public class ApiError
    {
        public string code;
        public string message;
    }

    [Serializable]
    class PublicKeyResponse
    {
        public int v;
        public string alg;
        public string kid;
        public string key_pem;
    }

    [Serializable]
    class CryptoEnvelope
    {
        public int v;
        public string alg;
        public string kid;
        public string ek;
        public string iv;
        public string ct;
        public string tag;
    }

    /// <summary>
    /// Unity 卡密鉴权客户端。
    /// HTTPS + RSA-OAEP/AES-256-GCM 请求体加密 + RSA-PSS 整场预授权 + HMAC 请求签名 + 短心跳租约。
    /// </summary>
    public class CardKeyAuthClient
    {
        const string GrantAlg = "RSA-PSS-SHA256";

        readonly string _baseUrl;
        readonly string _clientSignSecret;
        readonly string _deviceId;
        readonly string _clientVer;
        readonly string _gameName;
        readonly string _loginGameId;
        readonly bool _bodyEncryption;

        string _resolvedGameId;
        string _kid;
        string _publicPem;
        long _pubkeyFetchedAt;
        IntegrityGuard _guard;
        string _sessionGrant;
        string _sessionGrantSign;
        string _leaseSign;

        public string SessionToken { get; private set; }
        public string SessionId { get; private set; }
        /// <summary>会话过期时间（Unix 秒），心跳续期；勿当作卡密到期展示。</summary>
        public long ExpireAt { get; private set; }
        /// <summary>卡密到期时间（Unix 秒）；UI 剩余天数请用此字段。</summary>
        public long CardExpireAt { get; private set; }
        public long LeaseExpireAt { get; private set; }
        public int HeartbeatIntervalSec { get; private set; } = 20;
        public string GameId => !string.IsNullOrEmpty(_resolvedGameId) ? _resolvedGameId : _loginGameId;
        public string GameName => _gameName;
        public List<FeatureItem> Features { get; private set; } = new List<FeatureItem>();
        public List<PreauthorizedFeature> PreauthorizedFeatures { get; private set; } =
            new List<PreauthorizedFeature>();

        /// <param name="gameName">推荐：后台游戏显示名（与管理端一致）</param>
        /// <param name="gameId">兼容旧协议；仅当 gameName 为空时使用</param>
        public CardKeyAuthClient(
            string baseUrl,
            string clientSignSecret,
            string deviceId,
            string gameName = null,
            string gameId = null,
            string clientVer = "1.0.0",
            bool bodyEncryption = true)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _clientSignSecret = clientSignSecret;
            _deviceId = deviceId;
            _gameName = string.IsNullOrWhiteSpace(gameName) ? null : gameName.Trim();
            _loginGameId = string.IsNullOrWhiteSpace(gameId) ? null : gameId.Trim();
            _clientVer = clientVer;
            _bodyEncryption = bodyEncryption;
            if (string.IsNullOrEmpty(_gameName) && string.IsNullOrEmpty(_loginGameId))
                throw new ArgumentException("必须提供 gameName 或 gameId");
        }

        public bool HasFeature(string featureId)
        {
            if (string.IsNullOrEmpty(featureId) || !HasValidPreauthorization()) return false;
            foreach (var f in PreauthorizedFeatures)
            {
                if (f != null && (f.feature_id == featureId || f.name == featureId)) return true;
            }
            return false;
        }

        /// <summary>
        /// 高频弹幕/礼物触发只调用本方法：纯本地校验，不发 authorize/blob/ack。
        /// </summary>
        public bool TryGetPreauthorizedFeature(string featureId, out PreauthorizedFeature feature)
        {
            feature = null;
            if (!HasValidPreauthorization() || string.IsNullOrEmpty(featureId)) return false;
            foreach (var f in PreauthorizedFeatures)
            {
                if (f != null && (f.feature_id == featureId || f.name == featureId))
                {
                    feature = f;
                    return true;
                }
            }
            return false;
        }

        public async Task EnsurePublicKeyAsync(bool force = false)
        {
            if (!force && !string.IsNullOrEmpty(_publicPem) && UnixNow() - _pubkeyFetchedAt < 3600)
                return;
            var text = await GetTextAsync("/v1/crypto/public-key");
            var pk = JsonUtility.FromJson<PublicKeyResponse>(text);
            if (pk == null || string.IsNullOrEmpty(pk.key_pem) || string.IsNullOrEmpty(pk.kid))
                throw new Exception("拉取公钥失败");
            if (pk.v != 0 && pk.v != 1)
                throw new Exception("不支持的公钥版本");
            if (!string.IsNullOrEmpty(pk.alg) && pk.alg != "RSA-OAEP-SHA256+AES-256-GCM")
                throw new Exception("不支持的公钥算法: " + pk.alg);
            _publicPem = pk.key_pem;
            _kid = pk.kid;
            _pubkeyFetchedAt = UnixNow();
        }

        [Serializable]
        public class RiskGateResponse
        {
            public bool ok;
            public bool whitelisted;
            public bool blocked;
            public int risk_warn_max;
            public long risk_warn_reset_at;
            public string ip;
        }

        [Serializable]
        public class RiskReportResponse
        {
            public bool ok;
            public bool blocked;
            public bool skipped;
            public bool whitelisted;
            public string ip;
        }

        /// <summary>登录前查询当前 IP 是否白名单 / 已拉黑。</summary>
        public async Task<RiskGateResponse> RiskGateAsync()
        {
            await EnsurePublicKeyAsync();
            var ts = UnixNow();
            var nonce = NewNonce();
            var body = new Dictionary<string, object>
            {
                ["device_id"] = _deviceId,
                ["ts"] = ts,
                ["nonce"] = nonce,
            };
            body["sign"] = Sign(_clientSignSecret, body);
            var json = await PostJsonAsync("/v1/auth/risk-gate", body);
            var resp = JsonUtility.FromJson<RiskGateResponse>(json);
            return resp ?? new RiskGateResponse();
        }

        /// <summary>
        /// 风险软件预警超限后上报；服务端会拉黑 IP 并返回 RISK_IP_BLOCKED。
        /// 白名单 IP 返回 whitelisted/skipped，不抛错。
        /// </summary>
        public async Task<RiskReportResponse> ReportRiskAsync(string riskAppsCsv, int warnCount, string cardKey = null)
        {
            await EnsurePublicKeyAsync();
            var ts = UnixNow();
            var nonce = NewNonce();
            var body = new Dictionary<string, object>
            {
                ["device_id"] = _deviceId,
                ["risk_apps"] = riskAppsCsv ?? "",
                ["warn_count"] = warnCount,
                ["client_ver"] = _clientVer,
                ["ts"] = ts,
                ["nonce"] = nonce,
            };
            if (!string.IsNullOrEmpty(cardKey))
                body["card_key"] = cardKey;
            body["sign"] = Sign(_clientSignSecret, body);
            try
            {
                var json = await PostJsonAsync("/v1/auth/risk-report", body);
                var resp = JsonUtility.FromJson<RiskReportResponse>(json);
                return resp ?? new RiskReportResponse { ok = true };
            }
            catch (Exception ex)
            {
                if (ex.Message != null && ex.Message.IndexOf("RISK_IP_BLOCKED", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw;
                // 白名单等非错误 JSON 已在上方解析；其余继续抛
                throw;
            }
        }

        public async Task<LoginResponse> LoginAsync(string cardKey)
        {
            await EnsurePublicKeyAsync();

            var ts = UnixNow();
            var nonce = NewNonce();
            var body = new Dictionary<string, object>
            {
                ["card_key"] = cardKey,
                ["device_id"] = _deviceId,
                ["client_ver"] = _clientVer,
                ["ts"] = ts,
                ["nonce"] = nonce,
            };
            if (!string.IsNullOrEmpty(_gameName))
                body["game_name"] = _gameName;
            else
                body["game_id"] = _loginGameId;
            body["sign"] = Sign(_clientSignSecret, body);

            var json = await PostJsonAsync("/v1/auth/login", body);
            var resp = JsonUtility.FromJson<LoginResponse>(json);
            if (resp == null || string.IsNullOrEmpty(resp.session_token))
                throw new Exception("登录响应无效: " + json);

            SessionToken = resp.session_token;
            SessionId = resp.session_id;
            ExpireAt = resp.expire_at;
            CardExpireAt = ResolveCardExpireAt(resp);
            Features = resp.features != null ? new List<FeatureItem>(resp.features) : new List<FeatureItem>();
            if (resp.game != null && resp.game.game_id > 0)
                _resolvedGameId = resp.game.game_id.ToString();
            else if (resp.card != null && !string.IsNullOrEmpty(resp.card.game_id))
                _resolvedGameId = resp.card.game_id;
            else
                _resolvedGameId = _loginGameId;

            try
            {
                ApplySessionGrant(
                    resp.session_grant,
                    resp.session_grant_sign,
                    resp.session_grant_alg,
                    resp.session_grant_kid);
                ApplyHeartbeatLease(
                    resp.lease_expire_at,
                    resp.lease_sign,
                    resp.lease_alg,
                    resp.lease_kid,
                    resp.heartbeat_interval_sec);
            }
            catch
            {
                ClearSession();
                throw;
            }
            ResetGuard();
            return resp;
        }

        static long ResolveCardExpireAt(LoginResponse resp)
        {
            if (resp == null) return 0;
            if (resp.card_expire_at > 0) return resp.card_expire_at;
            if (resp.card != null && resp.card.expire_at > 0) return resp.card.expire_at;
            return 0;
        }

        /// <summary>
        /// 心跳：先做本地多层校验（动态物体名 token + 新分配的 Guard），
        /// 再把 integrity hash 上报服务端。
        /// </summary>
        public async Task<HeartbeatResponse> HeartbeatAsync()
        {
            EnsureSession();
            var hash = RunLocalIntegrityOrThrow();

            var ts = UnixNow();
            var nonce = NewNonce();
            var body = new Dictionary<string, object>
            {
                ["session_token"] = SessionToken,
                ["ts"] = ts,
                ["nonce"] = nonce,
                ["client_integrity_hash"] = hash,
            };

            body["sign"] = Sign(SessionSignSecret(), body);
            var json = await PostJsonAsync("/v1/auth/heartbeat", body);
            var resp = JsonUtility.FromJson<HeartbeatResponse>(json);
            if (resp == null)
                throw new Exception("心跳响应无效: " + json);
            if (resp.force_update)
            {
                ClearSession();
                throw new Exception("CLIENT_OUTDATED: 客户端版本过旧，请升级");
            }
            if (resp.expire_at > 0) ExpireAt = resp.expire_at;
            if (resp.card_expire_at > 0) CardExpireAt = resp.card_expire_at;
            if (resp.force_relogin) ClearSession();
            else
            {
                try
                {
                    ApplyHeartbeatLease(
                        resp.lease_expire_at,
                        resp.lease_sign,
                        resp.lease_alg,
                        resp.lease_kid,
                        resp.heartbeat_interval_sec);
                }
                catch
                {
                    ClearSession();
                    throw;
                }
                RotateGuard();
            }
            return resp;
        }

        /// <summary>从登录时缓存的整场预授权中读取参数并执行；不会产生网络请求。</summary>
        public async Task RunPreauthorizedAsync(
            string featureId,
            Func<PreauthorizedFeature, Task> run)
        {
            EnsureSession();
            if (!TryGetPreauthorizedFeature(featureId, out var feature))
                throw new Exception("PREAUTH_EXPIRED_OR_DENIED");
            await run(feature);
        }

        public void ClearSession()
        {
            MemoryPageProbe.Stop();
            DisposeGuard();
            SessionToken = null;
            SessionId = null;
            ExpireAt = 0;
            CardExpireAt = 0;
            LeaseExpireAt = 0;
            HeartbeatIntervalSec = 20;
            _sessionGrant = null;
            _sessionGrantSign = null;
            _leaseSign = null;
            _resolvedGameId = null;
            Features.Clear();
            PreauthorizedFeatures.Clear();
        }

        void ApplySessionGrant(string encodedGrant, string grantSign, string alg, string kid)
        {
            if (string.IsNullOrEmpty(encodedGrant) || string.IsNullOrEmpty(grantSign))
                throw new Exception("PREAUTH_INVALID: 缺少整场预授权");
            AssertServerSignAlg(alg);
            AssertKid(kid);
            if (!VerifyServerRsaPss("grant:" + encodedGrant, grantSign))
                throw new Exception("PREAUTH_INVALID: 预授权签名错误");

            SessionGrantPayload grant;
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encodedGrant));
                grant = JsonUtility.FromJson<SessionGrantPayload>(json);
            }
            catch (Exception ex)
            {
                throw new Exception("PREAUTH_INVALID: " + ex.Message);
            }
            if (grant == null || grant.v != 1 || grant.session_id != SessionId ||
                grant.game_id != GameId || grant.device_id != _deviceId)
                throw new Exception("PREAUTH_INVALID: 预授权上下文不匹配");

            _sessionGrant = encodedGrant;
            _sessionGrantSign = grantSign;
            PreauthorizedFeatures = grant.features != null
                ? new List<PreauthorizedFeature>(grant.features)
                : new List<PreauthorizedFeature>();
        }

        void ApplyHeartbeatLease(
            long leaseExpireAt,
            string leaseSign,
            string alg,
            string kid,
            int heartbeatIntervalSec)
        {
            if (leaseExpireAt <= UnixNow() || string.IsNullOrEmpty(leaseSign))
                throw new Exception("PREAUTH_EXPIRED");
            AssertServerSignAlg(alg);
            AssertKid(kid);
            if (!VerifyServerRsaPss("lease:" + SessionId + ":" + leaseExpireAt, leaseSign))
                throw new Exception("PREAUTH_INVALID: 心跳租约签名错误");
            LeaseExpireAt = leaseExpireAt;
            _leaseSign = leaseSign;
            if (heartbeatIntervalSec >= 5) HeartbeatIntervalSec = heartbeatIntervalSec;
        }

        bool HasValidPreauthorization()
        {
            if (string.IsNullOrEmpty(SessionToken) || string.IsNullOrEmpty(SessionId) ||
                string.IsNullOrEmpty(_sessionGrant) || string.IsNullOrEmpty(_sessionGrantSign) ||
                string.IsNullOrEmpty(_leaseSign) || UnixNow() >= LeaseExpireAt)
                return false;
            if (ExpireAt > 0 && UnixNow() >= ExpireAt) return false;
            if (CardExpireAt > 0 && UnixNow() >= CardExpireAt) return false;
            if (string.IsNullOrEmpty(_publicPem)) return false;

            return VerifyServerRsaPss("grant:" + _sessionGrant, _sessionGrantSign) &&
                   VerifyServerRsaPss("lease:" + SessionId + ":" + LeaseExpireAt, _leaseSign);
        }

        void AssertServerSignAlg(string alg)
        {
            if (string.IsNullOrEmpty(alg)) return; // 兼容旧响应缺字段
            if (!string.Equals(alg, GrantAlg, StringComparison.Ordinal))
                throw new Exception("PREAUTH_INVALID: 不支持的签名算法 " + alg);
        }

        void AssertKid(string kid)
        {
            if (string.IsNullOrEmpty(kid) || string.IsNullOrEmpty(_kid)) return;
            if (!string.Equals(kid, _kid, StringComparison.Ordinal))
                throw new Exception("PREAUTH_INVALID: 公钥指纹不匹配");
        }

        bool VerifyServerRsaPss(string message, string signatureBase64Url)
        {
            if (string.IsNullOrEmpty(_publicPem) || string.IsNullOrEmpty(signatureBase64Url))
                return false;
            try
            {
                var sig = FromBase64Url(signatureBase64Url);
                return CardKeyBodyCrypto.RsaPssSha256Verify(
                    Encoding.UTF8.GetBytes(message), sig, _publicPem);
            }
            catch
            {
                return false;
            }
        }

        static byte[] FromBase64Url(string s)
        {
            var padded = s.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }

        void ResetGuard()
        {
            DisposeGuard();
            if (!string.IsNullOrEmpty(SessionToken))
            {
                _guard = IntegrityGuard.Spawn(SessionId, SessionToken, SessionSignSecret(), 1);
                var decoy = Encoding.UTF8.GetBytes(Sha256Hex(SessionToken + "|probe"));
                MemoryPageProbe.Start(decoy);
            }
        }

        void RotateGuard()
        {
            if (_guard == null || !_guard.IsAlive)
                throw new Exception("INTEGRITY_FAIL");
            _guard = _guard.RotateNew();
            MemoryPageProbe.Rearm();
        }

        void DisposeGuard()
        {
            if (_guard != null)
            {
                _guard.DisposeSelf();
                _guard = null;
            }
        }

        string RunLocalIntegrityOrThrow()
        {
            if (MemoryPageProbe.ConsumeTripped())
            {
                MemoryPageProbe.Stop();
                throw new Exception("MEMORY_PROBE");
            }
            if (_guard == null || !_guard.IsAlive || !_guard.VerifyAll())
                throw new Exception("INTEGRITY_FAIL");
            var hash = _guard.ComputeIntegrityHash();
            if (string.IsNullOrEmpty(hash))
                throw new Exception("INTEGRITY_FAIL");
            MemoryPageProbe.Rearm();
            return hash;
        }

        string SessionSignSecret()
        {
            var hash = Sha256Hex(SessionToken);
            return _clientSignSecret + ":" + hash.Substring(0, 16);
        }

        void EnsureSession()
        {
            if (string.IsNullOrEmpty(SessionToken))
                throw new Exception("未登录，请先 LoginAsync");
            if (ExpireAt > 0 && UnixNow() >= ExpireAt)
                throw new Exception("SESSION_EXPIRED");
            if (CardExpireAt > 0 && UnixNow() >= CardExpireAt)
                throw new Exception("CARD_EXPIRED");
        }

        async Task<string> PostJsonAsync(string path, Dictionary<string, object> body, bool retriedKid = false)
        {
            var payload = DictToJson(body);
            byte[] raw;
            byte[] aesKey = null;
            if (_bodyEncryption)
            {
                await EnsurePublicKeyAsync();
                var sealedReq = SealRequest(payload);
                raw = Encoding.UTF8.GetBytes(sealedReq.json);
                aesKey = sealedReq.aesKey;
            }
            else
            {
                raw = Encoding.UTF8.GetBytes(payload);
            }

            using (var req = new UnityWebRequest(_baseUrl + path, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

#if UNITY_2020_2_OR_NEWER
                var ok = req.result == UnityWebRequest.Result.Success;
#else
                var ok = !req.isNetworkError && !req.isHttpError;
#endif
                var text = req.downloadHandler != null ? req.downloadHandler.text : "";
                if (!ok || req.responseCode >= 400)
                {
                    text = OpenResponse(text, aesKey, failClosed: false);
                    var err = SafeParseError(text);
                    if (err != null && err.code == "CRYPTO_KID_MISMATCH" && !retriedKid)
                    {
                        await EnsurePublicKeyAsync(true);
                        return await PostJsonAsync(path, body, true);
                    }
                    if (err != null && !string.IsNullOrEmpty(err.code))
                        throw new Exception(err.code + ": " + (err.message ?? ""));
                    // 仍是加密信封或空正文：不要把密文抛给 UI
                    if (string.IsNullOrEmpty(text) || (text.Contains("\"ct\"") && text.Contains("\"iv\"")))
                        throw new Exception("CRYPTO_DECRYPT_FAIL: 无法解密服务器响应");
                    if (!string.IsNullOrEmpty(req.error))
                        throw new Exception("NETWORK: " + req.error);
                    throw new Exception("NETWORK: 请求失败 (" + req.responseCode + ")");
                }
                return OpenResponse(text, aesKey, failClosed: true);
            }
        }

        async Task<string> GetTextAsync(string path)
        {
            using (var req = UnityWebRequest.Get(_baseUrl + path))
            {
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
#if UNITY_2020_2_OR_NEWER
                var ok = req.result == UnityWebRequest.Result.Success;
#else
                var ok = !req.isNetworkError && !req.isHttpError;
#endif
                var text = req.downloadHandler != null ? req.downloadHandler.text : "";
                if (!ok || req.responseCode >= 400)
                {
                    if (!string.IsNullOrEmpty(req.error))
                        throw new Exception("NETWORK: " + req.error);
                    throw new Exception(string.IsNullOrEmpty(text) ? "NETWORK: 请求失败" : text);
                }
                return text;
            }
        }

        (string json, byte[] aesKey) SealRequest(string plainJson)
        {
            var aesKey = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(aesKey);
            var ek = CardKeyBodyCrypto.RsaOaepSha256Encrypt(aesKey, _publicPem);
            CardKeyBodyCrypto.AesGcmEncrypt(
                aesKey, Encoding.UTF8.GetBytes(plainJson), out var iv, out var ct, out var tag);
            var json = DictToJson(new Dictionary<string, object>
            {
                ["v"] = 1,
                ["alg"] = "RSA-OAEP-SHA256+AES-256-GCM",
                ["kid"] = _kid,
                ["ek"] = Convert.ToBase64String(ek),
                ["iv"] = Convert.ToBase64String(iv),
                ["ct"] = Convert.ToBase64String(ct),
                ["tag"] = Convert.ToBase64String(tag),
            });
            return (json, aesKey);
        }

        string OpenResponse(string text, byte[] aesKey, bool failClosed)
        {
            if (!_bodyEncryption || aesKey == null || string.IsNullOrEmpty(text))
                return text;
            if (!text.Contains("\"ct\"") || !text.Contains("\"iv\""))
            {
                if (failClosed) throw new Exception("CRYPTO_REQUIRED: 响应不是加密信封");
                return text;
            }
            try
            {
                // Unity JsonUtility 无法正确处理 JSON null（服务端响应 ek:null），需先清洗
                var sanitized = SanitizeEnvelopeJson(text);
                var env = JsonUtility.FromJson<CryptoEnvelope>(sanitized);
                if (env == null || string.IsNullOrEmpty(env.ct) || string.IsNullOrEmpty(env.iv) ||
                    string.IsNullOrEmpty(env.tag))
                {
                    if (failClosed) throw new Exception("CRYPTO_REQUIRED: 响应信封无效");
                    return text;
                }
                if (env.v != 0 && env.v != 1)
                    throw new Exception("不支持的信封版本");
                // 请求用组合算法名；响应也可能是 AES-256-GCM
                if (!string.IsNullOrEmpty(env.alg) &&
                    env.alg != "RSA-OAEP-SHA256+AES-256-GCM" &&
                    env.alg != "AES-256-GCM")
                    throw new Exception("不支持的信封算法: " + env.alg);
                var iv = Convert.FromBase64String(env.iv);
                var ct = Convert.FromBase64String(env.ct);
                var tag = Convert.FromBase64String(env.tag);
                if (iv.Length != 12 || tag.Length != 16)
                    throw new Exception("响应信封 nonce/tag 长度无效");
                return CardKeyBodyCrypto.AesGcmDecryptToString(aesKey, iv, ct, tag);
            }
            catch (Exception ex)
            {
                if (failClosed) throw new Exception("CRYPTO_DECRYPT_FAIL: 解密响应失败");
                Debug.LogWarning("[CardKey] OpenResponse failed: " + ex.Message);
                return text;
            }
        }

        /// <summary>去掉/替换 JSON 中的 null 字段，避免 JsonUtility 丢字段。</summary>
        static string SanitizeEnvelopeJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;
            // "ek":null 或 "ek": null
            var s = System.Text.RegularExpressions.Regex.Replace(
                json, "\"ek\"\\s*:\\s*null\\s*,?", "");
            // 清理可能残留的多余逗号
            s = System.Text.RegularExpressions.Regex.Replace(s, ",\\s*}", "}");
            s = System.Text.RegularExpressions.Regex.Replace(s, ",\\s*,", ",");
            return s;
        }

        static ApiError SafeParseError(string json)
        {
            try { return JsonUtility.FromJson<ApiError>(json); }
            catch { return null; }
        }

        public static string Sign(string secret, Dictionary<string, object> payload)
        {
            var keys = new List<string>();
            foreach (var kv in payload)
            {
                if (kv.Key == "sign" || kv.Value == null) continue;
                keys.Add(kv.Key);
            }
            keys.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            for (var i = 0; i < keys.Count; i++)
            {
                if (i > 0) sb.Append('&');
                sb.Append(keys[i]).Append('=').Append(payload[keys[i]]);
            }
            return HmacSha256Hex(secret, sb.ToString());
        }

        static string DictToJson(Dictionary<string, object> body)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            var first = true;
            foreach (var kv in body)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(Escape(kv.Key)).Append('"').Append(':');
                if (kv.Value == null) sb.Append("null");
                else if (kv.Value is bool b) sb.Append(b ? "true" : "false");
                else if (kv.Value is int || kv.Value is long || kv.Value is float || kv.Value is double)
                    sb.Append(Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture));
                else
                    sb.Append('"').Append(Escape(kv.Value.ToString())).Append('"');
            }
            sb.Append('}');
            return sb.ToString();
        }

        static string Escape(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public static string HmacSha256Hex(string secret, string message)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                return ToHex(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));
            }
        }

        public static string Sha256Hex(string message)
        {
            using (var sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(message)));
            }
        }

        static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public static long UnixNow()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public static string NewNonce()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return ToHex(bytes);
        }

        /// <summary>
        /// 设备指纹：Android 用 Settings.Secure.ANDROID_ID（同签名+同用户下重装一般不变），
        /// 不再拼包名，避免重装/改包名导致误判换机。
        /// </summary>
        public static string DefaultDeviceId()
        {
            string raw = ResolveStableDeviceRawId();
            if (string.IsNullOrEmpty(raw))
                raw = SystemInfo.deviceUniqueIdentifier;
            return Sha256Hex(raw).Substring(0, 32);
        }

        static string ResolveStableDeviceRawId()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                using (var secure = new AndroidJavaClass("android.provider.Settings$Secure"))
                {
                    var androidId = secure.CallStatic<string>("getString", resolver, "android_id");
                    if (!string.IsNullOrEmpty(androidId))
                        return androidId;
                }
            }
            catch (Exception)
            {
                // fall through
            }
#endif
            return SystemInfo.deviceUniqueIdentifier;
        }
    }
}

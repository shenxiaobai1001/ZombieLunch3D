using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace CardKeyAuth
{
    /// <summary>
    /// 动态校验管理器：每次 Rotate 都 Destroy 自身并 new 一块内存，
    /// 避免静态单例地址被长期 hook / 改写。
    /// 多层：
    ///   L1 占位物体名称 == HMAC token
    ///   L2 组件 InstanceStamp / Slot / Round 一致
    ///   L3 物体仍存活且 HideFlags 未被清掉
    ///   L4 汇总 hash 随心跳上报
    /// </summary>
    public sealed class IntegrityGuard : MonoBehaviour
    {
        const int BeaconCount = 5;
        const HideFlags GuardFlags = HideFlags.HideAndDontSave;

        string _sessionId;
        string _sessionToken;
        string _secret;
        int _round;
        int _stamp;
        IntegrityBeacon[] _beacons;
        bool _alive = true;

        public bool IsAlive => _alive && this != null && gameObject != null;

        public static IntegrityGuard Spawn(string sessionId, string sessionToken, string secret, int round = 1)
        {
            var host = new GameObject(RandomHostName());
            host.hideFlags = GuardFlags;
            var guard = host.AddComponent<IntegrityGuard>();
            guard.Bootstrap(sessionId, sessionToken, secret, round);
            return guard;
        }

        void Bootstrap(string sessionId, string sessionToken, string secret, int round)
        {
            _sessionId = sessionId ?? "";
            _sessionToken = sessionToken ?? "";
            _secret = secret ?? "";
            _round = Math.Max(1, round);
            _stamp = unchecked(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
            _beacons = new IntegrityBeacon[BeaconCount];
            for (var i = 0; i < BeaconCount; i++)
                _beacons[i] = CreateBeacon(i);
        }

        IntegrityBeacon CreateBeacon(int slot)
        {
            var token = BeaconToken(_secret, _sessionId, _round, slot);
            var go = new GameObject(token);
            go.hideFlags = GuardFlags;
            go.transform.SetParent(transform, false);
            var beacon = go.AddComponent<IntegrityBeacon>();
            beacon.Slot = slot;
            beacon.Round = _round;
            beacon.InstanceStamp = unchecked(_stamp ^ (slot + 1) * (int)0x9E3779B9u);
            return beacon;
        }

        public bool VerifyAll()
        {
            if (!IsAlive || _beacons == null || _beacons.Length != BeaconCount)
                return false;
            if (string.IsNullOrEmpty(_sessionToken) || string.IsNullOrEmpty(_secret))
                return false;

            for (var i = 0; i < BeaconCount; i++)
            {
                var b = _beacons[i];
                if (b == null || b.gameObject == null)
                    return false;
                if ((b.gameObject.hideFlags & HideFlags.HideAndDontSave) == 0)
                    return false;
                if (b.Slot != i || b.Round != _round)
                    return false;
                if (b.InstanceStamp != unchecked(_stamp ^ (i + 1) * (int)0x9E3779B9u))
                    return false;
                var expected = BeaconToken(_secret, _sessionId, _round, i);
                if (!string.Equals(b.gameObject.name, expected, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        public string ComputeIntegrityHash()
        {
            if (!VerifyAll())
                return null;
            var sb = new StringBuilder(256);
            sb.Append(_round).Append('|').Append(_stamp).Append('|');
            for (var i = 0; i < BeaconCount; i++)
            {
                sb.Append(_beacons[i].gameObject.name);
                sb.Append('|');
                sb.Append(_beacons[i].GetInstanceID());
                sb.Append('|');
            }
            sb.Append(Head(_sessionToken, 12));
            return Sha256Hex(sb.ToString());
        }

        public IntegrityGuard RotateNew()
        {
            var sid = _sessionId;
            var tok = _sessionToken;
            var sec = _secret;
            var nextRound = _round + 1;
            DisposeSelf();
            return Spawn(sid, tok, sec, nextRound);
        }

        public void DisposeSelf()
        {
            _alive = false;
            if (_beacons != null)
            {
                for (var i = 0; i < _beacons.Length; i++)
                {
                    var b = _beacons[i];
                    _beacons[i] = null;
                    if (b != null && b.gameObject != null)
                        Destroy(b.gameObject);
                }
                _beacons = null;
            }
            _sessionToken = null;
            _secret = null;
            if (gameObject != null)
                Destroy(gameObject);
        }

        static string BeaconToken(string secret, string sessionId, int round, int slot)
        {
            var msg = (sessionId ?? "") + "|" + round + "|" + slot;
            var hex = HmacSha256Hex(secret ?? "", msg);
            return "ckb" + hex.Substring(0, 16);
        }

        static string RandomHostName()
        {
            var bytes = new byte[8];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return "ckg" + ToHex(bytes);
        }

        static string Head(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n);
        }

        static string HmacSha256Hex(string secret, string message)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
                return ToHex(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));
        }

        static string Sha256Hex(string message)
        {
            using (var sha = SHA256.Create())
                return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(message)));
        }

        static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}

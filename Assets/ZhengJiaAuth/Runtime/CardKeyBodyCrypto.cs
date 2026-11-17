using System;
using System.Text;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using UnityEngine;

namespace CardKeyAuth
{
    /// <summary>
    /// Unity Mono 不支持 AesGcm / 部分 RSA API（会抛 PlatformNotSupportedException）。
    /// 密码学统一走 BouncyCastle，与 UE OpenSSL / 服务端 Node 对齐。
    /// </summary>
    public static class CardKeyBodyCrypto
    {
        public static byte[] RsaOaepSha256Encrypt(byte[] data, string publicPem)
        {
            var pub = ParsePublicKey(publicPem);
            var engine = new OaepEncoding(new RsaEngine(), new Sha256Digest(), new Sha256Digest(), null);
            engine.Init(true, pub);
            return engine.ProcessBlock(data, 0, data.Length);
        }

        public static bool RsaPssSha256Verify(byte[] message, byte[] signature, string publicPem)
        {
            try
            {
                var pub = ParsePublicKey(publicPem);
                var signer = new PssSigner(new RsaEngine(), new Sha256Digest(), 32);
                signer.Init(false, pub);
                signer.BlockUpdate(message, 0, message.Length);
                return signer.VerifySignature(signature);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CardKey] RSA-PSS verify failed: " + ex.Message);
                return false;
            }
        }

        public static void AesGcmEncrypt(
            byte[] key,
            byte[] plain,
            out byte[] iv,
            out byte[] ct,
            out byte[] tag)
        {
            iv = new byte[12];
            new SecureRandom().NextBytes(iv);

            var cipher = new GcmBlockCipher(new AesEngine());
            var parms = new AeadParameters(new KeyParameter(key), 128, iv);
            cipher.Init(true, parms);

            var output = new byte[cipher.GetOutputSize(plain.Length)];
            var len = cipher.ProcessBytes(plain, 0, plain.Length, output, 0);
            len += cipher.DoFinal(output, len);

            // BouncyCastle 输出 = ciphertext || tag(16)
            tag = new byte[16];
            ct = new byte[len - 16];
            Array.Copy(output, 0, ct, 0, ct.Length);
            Array.Copy(output, ct.Length, tag, 0, 16);
        }

        public static byte[] AesGcmDecrypt(byte[] key, byte[] iv, byte[] ct, byte[] tag)
        {
            var cipher = new GcmBlockCipher(new AesEngine());
            var parms = new AeadParameters(new KeyParameter(key), 128, iv);
            cipher.Init(false, parms);

            var input = new byte[ct.Length + tag.Length];
            Array.Copy(ct, 0, input, 0, ct.Length);
            Array.Copy(tag, 0, input, ct.Length, tag.Length);

            var output = new byte[cipher.GetOutputSize(input.Length)];
            var len = cipher.ProcessBytes(input, 0, input.Length, output, 0);
            len += cipher.DoFinal(output, len);

            if (len == output.Length) return output;
            var trimmed = new byte[len];
            Array.Copy(output, 0, trimmed, 0, len);
            return trimmed;
        }

        public static string AesGcmDecryptToString(byte[] key, byte[] iv, byte[] ct, byte[] tag)
        {
            return Encoding.UTF8.GetString(AesGcmDecrypt(key, iv, ct, tag));
        }

        static RsaKeyParameters ParsePublicKey(string publicPem)
        {
            if (string.IsNullOrEmpty(publicPem))
                throw new Exception("公钥为空");

            var sb = new StringBuilder();
            foreach (var line in publicPem.Replace("\r", "").Split('\n'))
            {
                var t = line.Trim();
                if (t.Length == 0 || t.StartsWith("-----")) continue;
                sb.Append(t);
            }
            var der = Convert.FromBase64String(sb.ToString());
            var spki = SubjectPublicKeyInfo.GetInstance(Asn1Object.FromByteArray(der));
            var key = PublicKeyFactory.CreateKey(spki) as RsaKeyParameters;
            if (key == null)
                throw new Exception("公钥不是 RSA");
            return key;
        }
    }
}

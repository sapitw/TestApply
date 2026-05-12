using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace CXMTCode.Infrastructure.Crypto;

/// <summary>
/// 国密算法辅助类 - 基于 BouncyCastle 的真实 SM2/SM3/SM4 实现。
/// <para>SM2：256 位椭圆曲线（GMT 0003.2-2012），公钥加密 + 签名</para>
/// <para>SM3：256 位密码杂凑（GMT 0004-2012）</para>
/// <para>SM4：128 位分组对称加密（GMT 0002-2012），CBC + PKCS7</para>
/// </summary>
public static class CryptoHelper
{
    public const string DefaultKey16 = "CXMTCodeSm4Key!!";   // 16 byte
    public const string DefaultIv16  = "CXMTCodeSm4Iv0!!";   // 16 byte

    public static byte[] DefaultKey => Encoding.UTF8.GetBytes(DefaultKey16);
    public static byte[] DefaultIv  => Encoding.UTF8.GetBytes(DefaultIv16);

    private static readonly X9ECParameters Sm2Curve = GMNamedCurves.GetByName("sm2p256v1");
    private static readonly ECDomainParameters Sm2Domain =
        new(Sm2Curve.Curve, Sm2Curve.G, Sm2Curve.N, Sm2Curve.H);

    // ============ SM3 ============

    /// <summary>SM3 杂凑（输出 64 hex 字符 = 256 位）</summary>
    public static string ComputeSm3Hash(string input)
    {
        var data = Encoding.UTF8.GetBytes(input ?? string.Empty);
        var digest = new SM3Digest();
        digest.BlockUpdate(data, 0, data.Length);
        var result = new byte[digest.GetDigestSize()];
        digest.DoFinal(result, 0);
        return Convert.ToHexString(result);
    }

    public static string ComputeSm3HashWithSalt(string input, string salt) =>
        ComputeSm3Hash($"{salt}:{input}");

    // ============ SM4 ============

    /// <summary>SM4-CBC-PKCS7 加密，返回 Base64</summary>
    public static string EncryptSm4(string plainText, byte[]? key = null, byte[]? iv = null)
    {
        if (plainText is null) return string.Empty;
        return Convert.ToBase64String(Sm4Crypt(
            true,
            Encoding.UTF8.GetBytes(plainText),
            NormalizeKey(key ?? DefaultKey),
            NormalizeIv(iv ?? DefaultIv)));
    }

    /// <summary>SM4-CBC-PKCS7 解密，输入 Base64</summary>
    public static string DecryptSm4(string cipherText, byte[]? key = null, byte[]? iv = null)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;
        var plain = Sm4Crypt(
            false,
            Convert.FromBase64String(cipherText),
            NormalizeKey(key ?? DefaultKey),
            NormalizeIv(iv ?? DefaultIv));
        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] Sm4Crypt(bool forEncryption, byte[] data, byte[] key, byte[] iv)
    {
        var engine = new SM4Engine();
        var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(engine), new Pkcs7Padding());
        cipher.Init(forEncryption, new ParametersWithIV(new KeyParameter(key), iv));
        var output = new byte[cipher.GetOutputSize(data.Length)];
        var len = cipher.ProcessBytes(data, 0, data.Length, output, 0);
        len += cipher.DoFinal(output, len);
        if (len == output.Length) return output;
        var trimmed = new byte[len];
        Array.Copy(output, trimmed, len);
        return trimmed;
    }

    private static byte[] NormalizeKey(byte[] key)
    {
        if (key.Length == 16) return key;
        var fixed16 = new byte[16];
        Array.Copy(SHA256.HashData(key), fixed16, 16);
        return fixed16;
    }
    private static byte[] NormalizeIv(byte[] iv)
    {
        if (iv.Length == 16) return iv;
        var fixed16 = new byte[16];
        Array.Copy(SHA256.HashData(iv), fixed16, 16);
        return fixed16;
    }

    // ============ SM2 ============

    /// <summary>SM2 签名（默认 userId='1234567812345678'，符合 GMT 0009-2012）</summary>
    public static string SignSm2(string data, byte[] privateKeyDer)
    {
        var d = new BigInteger(1, ExtractScalar(privateKeyDer));
        var privKey = new ECPrivateKeyParameters(d, Sm2Domain);
        var signer  = new SM2Signer();
        signer.Init(true, new ParametersWithRandom(privKey, new SecureRandom()));
        var msg = Encoding.UTF8.GetBytes(data ?? string.Empty);
        signer.BlockUpdate(msg, 0, msg.Length);
        return Convert.ToBase64String(signer.GenerateSignature());
    }

    /// <summary>SM2 验签</summary>
    public static bool VerifySm2(string data, string base64Signature, byte[] publicKeyDer)
    {
        try
        {
            var q = Sm2Curve.Curve.DecodePoint(ExtractPublicPoint(publicKeyDer));
            var pubKey = new ECPublicKeyParameters(q, Sm2Domain);
            var signer = new SM2Signer();
            signer.Init(false, pubKey);
            var msg = Encoding.UTF8.GetBytes(data ?? string.Empty);
            signer.BlockUpdate(msg, 0, msg.Length);
            return signer.VerifySignature(Convert.FromBase64String(base64Signature));
        }
        catch { return false; }
    }

    /// <summary>生成 SM2 密钥对（私钥 32 字节大端 + 公钥 65 字节未压缩 04||X||Y）</summary>
    public static (byte[] PrivateKey, byte[] PublicKey) GenerateSm2KeyPair()
    {
        var generator = new ECKeyPairGenerator();
        generator.Init(new ECKeyGenerationParameters(Sm2Domain, new SecureRandom()));
        var pair = generator.GenerateKeyPair();
        var priv = (ECPrivateKeyParameters)pair.Private;
        var pub  = (ECPublicKeyParameters)pair.Public;
        return (BigIntegerTo32(priv.D), pub.Q.GetEncoded(false));
    }

    private static byte[] BigIntegerTo32(BigInteger n)
    {
        var raw = n.ToByteArrayUnsigned();
        if (raw.Length == 32) return raw;
        var fixed32 = new byte[32];
        Array.Copy(raw, 0, fixed32, 32 - raw.Length, raw.Length);
        return fixed32;
    }

    /// <summary>使用 SM2 加密任意明文（输出 C1||C3||C2 Base64）</summary>
    public static string EncryptSm2(string plainText, byte[] publicKeyDer)
    {
        var q = Sm2Curve.Curve.DecodePoint(ExtractPublicPoint(publicKeyDer));
        var pubKey = new ECPublicKeyParameters(q, Sm2Domain);
        var engine = new SM2Engine(SM2Engine.Mode.C1C3C2);
        engine.Init(true, new ParametersWithRandom(pubKey, new SecureRandom()));
        var input = Encoding.UTF8.GetBytes(plainText ?? string.Empty);
        var output = engine.ProcessBlock(input, 0, input.Length);
        return Convert.ToBase64String(output);
    }

    /// <summary>使用 SM2 解密</summary>
    public static string DecryptSm2(string cipherBase64, byte[] privateKeyDer)
    {
        var d = new BigInteger(1, ExtractScalar(privateKeyDer));
        var privKey = new ECPrivateKeyParameters(d, Sm2Domain);
        var engine = new SM2Engine(SM2Engine.Mode.C1C3C2);
        engine.Init(false, privKey);
        var input = Convert.FromBase64String(cipherBase64);
        var output = engine.ProcessBlock(input, 0, input.Length);
        return Encoding.UTF8.GetString(output);
    }

    private static byte[] ExtractScalar(byte[] bytes)
    {
        if (bytes.Length == 32) return bytes;
        var s = new byte[32];
        Array.Copy(bytes, bytes.Length - 32, s, 0, 32);
        return s;
    }

    private static byte[] ExtractPublicPoint(byte[] bytes)
    {
        if (bytes.Length == 65 && bytes[0] == 0x04) return bytes;
        if (bytes.Length == 64)
        {
            var encoded = new byte[65];
            encoded[0] = 0x04;
            Array.Copy(bytes, 0, encoded, 1, 64);
            return encoded;
        }
        var pt = new byte[65];
        Array.Copy(bytes, bytes.Length - 65, pt, 0, 65);
        return pt;
    }
}

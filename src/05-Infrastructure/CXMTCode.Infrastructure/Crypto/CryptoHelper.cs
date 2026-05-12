using System.Security.Cryptography;
using System.Text;

namespace CXMTCode.Infrastructure.Crypto;

/// <summary>
/// 国密算法辅助类（占位实现）。
/// <para>
/// TODO：等保 2.0 / 国密合规需求下，需替换为真正的 GMSSL 实现：
///   - SM2：椭圆曲线公钥密码（签名/验签/加密）
///   - SM3：哈希算法（256-bit）
///   - SM4：分组对称密码（128-bit 分组）
/// </para>
/// <para>
/// 当前以 .NET 内置算法占位以保证 dotnet build 通过：
///   SM3  → SHA256
///   SM4  → AES-128-CBC
///   SM2  → ECDsa(NIST P-256)
/// 占位实现仅供开发期跑通流程，不可在生产使用。
/// </para>
/// </summary>
public static class CryptoHelper
{
    public const string DefaultKey16 = "CXMTCodeSM4Key16";
    public const string DefaultIv16  = "CXMTCodeSM4Iv016";

    public static byte[] DefaultKey => Encoding.UTF8.GetBytes(DefaultKey16);
    public static byte[] DefaultIv  => Encoding.UTF8.GetBytes(DefaultIv16);

    /// <summary>SM3 哈希（占位：SHA256）</summary>
    public static string ComputeSm3Hash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    /// <summary>SM3 哈希（带盐值）</summary>
    public static string ComputeSm3HashWithSalt(string input, string salt) =>
        ComputeSm3Hash($"{salt}:{input}");

    /// <summary>SM4 加密（占位：AES-128-CBC）</summary>
    public static string EncryptSm4(string plainText, byte[]? key = null, byte[]? iv = null)
    {
        key ??= DefaultKey;
        iv  ??= DefaultIv;
        using var aes = Aes.Create();
        aes.Key = key.Length == 16 ? key : SHA256.HashData(key).AsSpan(0, 16).ToArray();
        aes.IV  = iv.Length  == 16 ? iv  : SHA256.HashData(iv ).AsSpan(0, 16).ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var enc = aes.CreateEncryptor();
        var input  = Encoding.UTF8.GetBytes(plainText ?? string.Empty);
        var output = enc.TransformFinalBlock(input, 0, input.Length);
        return Convert.ToBase64String(output);
    }

    /// <summary>SM4 解密（占位：AES-128-CBC）</summary>
    public static string DecryptSm4(string cipherText, byte[]? key = null, byte[]? iv = null)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;
        key ??= DefaultKey;
        iv  ??= DefaultIv;
        using var aes = Aes.Create();
        aes.Key = key.Length == 16 ? key : SHA256.HashData(key).AsSpan(0, 16).ToArray();
        aes.IV  = iv.Length  == 16 ? iv  : SHA256.HashData(iv ).AsSpan(0, 16).ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var dec = aes.CreateDecryptor();
        var input  = Convert.FromBase64String(cipherText);
        var output = dec.TransformFinalBlock(input, 0, input.Length);
        return Encoding.UTF8.GetString(output);
    }

    /// <summary>SM2 签名（占位：ECDsa P-256，私钥 PKCS#8 DER）</summary>
    public static string SignSm2(string data, byte[] privateKeyPkcs8)
    {
        using var ec = ECDsa.Create();
        ec.ImportPkcs8PrivateKey(privateKeyPkcs8, out _);
        var sig = ec.SignData(Encoding.UTF8.GetBytes(data ?? string.Empty), HashAlgorithmName.SHA256);
        return Convert.ToBase64String(sig);
    }

    /// <summary>SM2 验签（占位：ECDsa P-256，公钥 SubjectPublicKeyInfo DER）</summary>
    public static bool VerifySm2(string data, string base64Signature, byte[] publicKeySpki)
    {
        try
        {
            using var ec = ECDsa.Create();
            ec.ImportSubjectPublicKeyInfo(publicKeySpki, out _);
            return ec.VerifyData(
                Encoding.UTF8.GetBytes(data ?? string.Empty),
                Convert.FromBase64String(base64Signature),
                HashAlgorithmName.SHA256);
        }
        catch { return false; }
    }

    /// <summary>生成一对 SM2 占位密钥（ECDsa P-256）</summary>
    public static (byte[] PrivateKey, byte[] PublicKey) GenerateSm2KeyPair()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (ec.ExportPkcs8PrivateKey(), ec.ExportSubjectPublicKeyInfo());
    }
}

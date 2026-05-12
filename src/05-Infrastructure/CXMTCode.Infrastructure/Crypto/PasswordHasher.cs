using System.Security.Cryptography;

namespace CXMTCode.Infrastructure.Crypto;

/// <summary>
/// 密码哈希工具 - 基于 SM3 占位（实际 SHA256）+ 随机盐。
/// </summary>
public static class PasswordHasher
{
    public static string Hash(string password, out string saltBase64)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        saltBase64 = Convert.ToBase64String(saltBytes);
        return CryptoHelper.ComputeSm3HashWithSalt(password, saltBase64);
    }

    public static bool Verify(string password, string expectedHash, string saltBase64)
    {
        var computed = CryptoHelper.ComputeSm3HashWithSalt(password, saltBase64);
        return string.Equals(computed, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}

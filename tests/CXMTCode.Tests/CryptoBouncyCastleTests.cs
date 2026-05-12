using CXMTCode.Infrastructure.Crypto;

namespace CXMTCode.Tests;

/// <summary>额外验证：BouncyCastle 真实算法实现的关键特征</summary>
public class CryptoBouncyCastleTests
{
    [Fact]
    public void Sm3_of_empty_string_matches_known_value()
    {
        // SM3("") = 1AB21D8355CFA17F8E61194831E81A8F22BEC8C728FEFB747ED035EB5082AA2B
        var hex = CryptoHelper.ComputeSm3Hash("");
        hex.Should().Be("1AB21D8355CFA17F8E61194831E81A8F22BEC8C728FEFB747ED035EB5082AA2B");
    }

    [Fact]
    public void Sm3_of_known_input_matches_known_value()
    {
        // SM3("abc") = 66C7F0F462EEEDD9D1F2D46BDC10E4E24167C4875CF2F7A2297DA02B8F4BA8E0
        var hex = CryptoHelper.ComputeSm3Hash("abc");
        hex.Should().Be("66C7F0F462EEEDD9D1F2D46BDC10E4E24167C4875CF2F7A2297DA02B8F4BA8E0");
    }

    [Fact]
    public void Sm4_block_size_is_16_with_pkcs7_padding()
    {
        // 单字节明文 → 1 个 16 字节分组 → Base64 长度 24
        var c = CryptoHelper.EncryptSm4("a");
        var raw = Convert.FromBase64String(c);
        raw.Length.Should().Be(16);
    }

    [Fact]
    public void Sm4_long_text_roundtrip()
    {
        var plain = new string('X', 1024);
        var c = CryptoHelper.EncryptSm4(plain);
        CryptoHelper.DecryptSm4(c).Should().Be(plain);
    }

    [Fact]
    public void Sm2_key_pair_sizes_match_curve_parameters()
    {
        var (priv, pub) = CryptoHelper.GenerateSm2KeyPair();
        priv.Length.Should().Be(32);
        pub.Length.Should().Be(65);
        pub[0].Should().Be((byte)0x04); // 未压缩
    }

    [Fact]
    public void Sm2_encryption_roundtrip()
    {
        var (priv, pub) = CryptoHelper.GenerateSm2KeyPair();
        var c = CryptoHelper.EncryptSm2("hello CXMT", pub);
        CryptoHelper.DecryptSm2(c, priv).Should().Be("hello CXMT");
    }

    [Fact]
    public void Sm2_signature_detects_tampered_data()
    {
        var (priv, pub) = CryptoHelper.GenerateSm2KeyPair();
        var sig = CryptoHelper.SignSm2("approve-001", priv);
        CryptoHelper.VerifySm2("approve-002", sig, pub).Should().BeFalse();
    }
}

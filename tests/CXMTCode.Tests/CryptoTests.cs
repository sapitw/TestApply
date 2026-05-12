using CXMTCode.Infrastructure.Crypto;
using FluentAssertions;

namespace CXMTCode.Tests;

public class CryptoTests
{
    [Fact]
    public void Sm3_should_be_deterministic_64hex()
    {
        var a = CryptoHelper.ComputeSm3Hash("CXMTCode");
        var b = CryptoHelper.ComputeSm3Hash("CXMTCode");
        a.Should().Be(b);
        a.Length.Should().Be(64);
    }

    [Fact]
    public void Sm4_roundtrip_should_recover_plaintext()
    {
        var c = CryptoHelper.EncryptSm4("password@123");
        CryptoHelper.DecryptSm4(c).Should().Be("password@123");
    }

    [Fact]
    public void Sm2_sign_then_verify_should_succeed()
    {
        var (priv, pub) = CryptoHelper.GenerateSm2KeyPair();
        var sig = CryptoHelper.SignSm2("approve-001", priv);
        CryptoHelper.VerifySm2("approve-001", sig, pub).Should().BeTrue();
        CryptoHelper.VerifySm2("tampered",    sig, pub).Should().BeFalse();
    }

    [Fact]
    public void PasswordHasher_should_round_trip()
    {
        var hash = PasswordHasher.Hash("admin@123", out var salt);
        PasswordHasher.Verify("admin@123", hash, salt).Should().BeTrue();
        PasswordHasher.Verify("wrong",     hash, salt).Should().BeFalse();
    }
}

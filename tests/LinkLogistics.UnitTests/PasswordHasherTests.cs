using LinkLogistics.Infrastructure.Security;

namespace LinkLogistics.UnitTests;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_verify_succeeds()
    {
        var hash = _hasher.Hash("Driver123!");
        _hasher.Verify("Driver123!", hash).ShouldBeTrue();
    }

    [Fact]
    public void Verify_rejects_the_wrong_password()
    {
        var hash = _hasher.Hash("Driver123!");
        _hasher.Verify("driver123!", hash).ShouldBeFalse();
        _hasher.Verify("", hash).ShouldBeFalse();
    }

    [Fact]
    public void Hash_uses_the_documented_pbkdf2_format()
    {
        var hash = _hasher.Hash("x");
        var parts = hash.Split('$');
        parts.Length.ShouldBe(5);
        parts[0].ShouldBe("pbkdf2");
        parts[1].ShouldBe("sha256");
        parts[2].ShouldBe("100000");
    }

    [Fact]
    public void Verify_rejects_a_tampered_hash()
    {
        var hash = _hasher.Hash("secret");
        var tampered = hash[..^4] + "AAAA";
        _hasher.Verify("secret", tampered).ShouldBeFalse();
    }

    [Fact]
    public void Verify_matches_a_hash_produced_by_the_seed_script()
    {
        // the exact value seeded for the demo ops user (900_seed.sql)
        const string seeded = "pbkdf2$sha256$100000$Qk7QyCH6WD397x2NC32ghg==$GcER5iRA5QR9YYm5CovUUY1n1bZBhj4h0SN0mlkNhOw=";
        _hasher.Verify("Ops12345!", seeded).ShouldBeTrue();
        _hasher.Verify("wrong", seeded).ShouldBeFalse();
    }
}

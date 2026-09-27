using System.Security.Cryptography;
using System.Text.Json;
using Hub.Api.Infrastructure;

namespace Hub.Tests.Domain;

public sealed class LocalPasswordTests
{
    [Fact]
    public void Empty_bootstrap_verifier_accepts_no_account()
    {
        var store = new LocalPasswordStore("{\"users\":[]}", isJson: true);
        Assert.Null(store.Verify("taylor", "synthetic review password"));
    }

    [Fact]
    public void Verifier_maps_only_the_correct_individual_account()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        const string password = "synthetic review password one";
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, LocalPasswordStore.Iterations, HashAlgorithmName.SHA256, 32);
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new { users = new[] {
                new { userId = first.ToString(), userName = "taylor", salt = Convert.ToHexStringLower(salt), hash = Convert.ToHexStringLower(hash) },
                new { userId = second.ToString(), userName = "jay", salt = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)), hash = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)) }
            } }));
            var store = new LocalPasswordStore(path);
            Assert.Equal(first, store.Verify("Taylor", password));
            Assert.Null(store.Verify("jay", password));
            Assert.Null(store.Verify("unknown", password));
            Assert.Null(store.Verify("taylor", "wrong password"));
            Assert.Equal(first, new LocalPasswordStore(File.ReadAllText(path), isJson: true).Verify("taylor", password));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Verifier_rejects_duplicate_account_mappings()
    {
        var id = Guid.NewGuid(); var path = Path.GetTempFileName();
        try
        {
            var users = new[] { "taylor", "jay" }.Select(name => new { userId = id.ToString(), userName = name,
                salt = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)), hash = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)) });
            File.WriteAllText(path, JsonSerializer.Serialize(new { users }));
            Assert.Throws<InvalidOperationException>(() => new LocalPasswordStore(path));
        }
        finally { File.Delete(path); }
    }
}

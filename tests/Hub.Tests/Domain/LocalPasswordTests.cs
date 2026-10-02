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

    [Fact]
    public void Sign_in_throttle_uses_cloudflare_client_only_from_the_trusted_tunnel_hop()
    {
        var proxy = System.Net.IPAddress.Parse("172.30.245.1");
        Microsoft.AspNetCore.Http.DefaultHttpContext Req(string remote, string? cf)
        {
            var ctx = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remote);
            if (cf is not null) ctx.Request.Headers["CF-Connecting-IP"] = cf;
            return ctx;
        }
        Assert.Equal("cf:203.0.113.7", AuthSetup.ClientKey(Req("172.30.245.1", "203.0.113.7"), proxy));
        Assert.Equal("cf:203.0.113.7", AuthSetup.ClientKey(Req("::ffff:172.30.245.1", "203.0.113.7"), proxy));
        Assert.NotEqual(AuthSetup.ClientKey(Req("172.30.245.1", "203.0.113.7"), proxy), AuthSetup.ClientKey(Req("172.30.245.1", "198.51.100.9"), proxy));
        Assert.Equal("10.0.0.5", AuthSetup.ClientKey(Req("10.0.0.5", "203.0.113.7"), proxy)); // spoofed header from an untrusted hop
        Assert.Equal("172.30.245.1", AuthSetup.ClientKey(Req("172.30.245.1", "not-an-ip"), proxy));
        var multiple = Req("172.30.245.1", "203.0.113.7"); multiple.Request.Headers["CF-Connecting-IP"] = new[] { "203.0.113.7", "198.51.100.9" };
        Assert.Equal("172.30.245.1", AuthSetup.ClientKey(multiple, proxy));
        Assert.Equal("172.30.245.1", AuthSetup.ClientKey(Req("172.30.245.1", "203.0.113.7"), null)); // tunnel mode off
        Assert.Equal("172.30.245.1", AuthSetup.ClientKey(Req("172.30.245.1", null), proxy));
    }

    [Fact]
    public void Bootstrap_admin_list_reads_email_and_name_pairs_and_refuses_typos()
    {
        Assert.Equal(new[] { ("a@x.test", "Ann Admin"), ("b@x.test", "Bo") },
            AuthSetup.ParseBootstrapAdmins(" a@x.test | Ann Admin ; b@x.test|Bo; A@X.TEST|Repeated ;"));
        Assert.Empty(AuthSetup.ParseBootstrapAdmins(" ; "));
        foreach (var typo in new[] { "a@x.test", "a@x.test|", "no-at-sign|Name", "a@@x.test|Name", "a b@x.test|Name", "@x.test|Name", "a@|Name", "a@x.test|Name|Extra" })
            Assert.Throws<InvalidOperationException>(() => AuthSetup.ParseBootstrapAdmins(typo));
    }

    static object Login(Guid id, string userName, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, LocalPasswordStore.Iterations, HashAlgorithmName.SHA256, 32);
        return new { userId = id.ToString(), userName, salt = Convert.ToHexStringLower(salt), hash = Convert.ToHexStringLower(hash) };
    }

    /// An operator edit; the clock step makes the change certain on file systems with coarse timestamps.
    static void Rewrite(string path, string json)
    {
        var before = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, json);
        File.SetLastWriteTimeUtc(path, before.AddSeconds(1));
    }

    [Fact]
    public void Changed_verifier_file_is_reread_and_rotation_or_removal_moves_only_that_persons_stamp()
    {
        var taylor = Guid.NewGuid(); var jay = Guid.NewGuid();
        var jayLogin = Login(jay, "jay", "second synthetic password");
        static string Users(params object[] users) => JsonSerializer.Serialize(new { users });
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Users(Login(taylor, "taylor", "first synthetic password"), jayLogin));
            var store = new LocalPasswordStore(path);
            Assert.Equal(taylor, store.Verify("taylor", "first synthetic password", out var issued));
            Assert.Equal(issued, store.Stamp(taylor));
            var jayStamp = store.Stamp(jay);
            Assert.NotNull(jayStamp);

            Rewrite(path, Users(Login(taylor, "taylor", "rotated synthetic password"), jayLogin)); // no restart
            Assert.Null(store.Verify("taylor", "first synthetic password"));
            Assert.Equal(taylor, store.Verify("taylor", "rotated synthetic password", out var rotated));
            Assert.NotEqual(issued, rotated);
            Assert.Equal(rotated, store.Stamp(taylor));
            Assert.Equal(jayStamp, store.Stamp(jay)); // other people's sessions are untouched

            Rewrite(path, Users(jayLogin));
            Assert.Null(store.Stamp(taylor));
            Assert.Null(store.Verify("taylor", "rotated synthetic password"));

            Rewrite(path, "{ not json"); // a broken edit admits no one rather than keeping removed logins alive
            Assert.Null(store.Stamp(jay));
            Rewrite(path, Users(jayLogin));
            Assert.Equal(jayStamp, store.Stamp(jay));
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);
                Rewrite(path, Users(jayLogin));
                Assert.Null(store.Stamp(jay)); // no longer private
            }
        }
        finally { File.Delete(path); }
    }
}

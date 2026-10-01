using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hub.Api.Infrastructure;

/// <summary>Review-only credentials. The JSON file is mounted outside Git and never returned by an API.
/// A file source is re-read when it changes, so adding, rotating or removing a login needs no restart.</summary>
public sealed class LocalPasswordStore
{
    public sealed record Entry(string UserId, string UserName, string Salt, string Hash);
    sealed record FileFormat(Entry[] Users);
    /// One consistent set of verifiers; `Stamps` fingerprints each person's current verifier for session binding.
    sealed record Snapshot(Dictionary<string, Entry> Users, Dictionary<Guid, string> Stamps, DateTime Written, long Length);
    readonly string? path; // null: the verifiers came from configuration (Key Vault) and change only with a restart
    readonly object gate = new();
    volatile Snapshot current;
    readonly byte[] decoySalt = RandomNumberGenerator.GetBytes(16);
    readonly byte[] decoyHash = RandomNumberGenerator.GetBytes(32);
    public const int Iterations = 600_000;

    public LocalPasswordStore(string value, bool isJson = false)
    {
        if (isJson)
        {
            if (value.Length is < 1 or > 65536) throw new InvalidOperationException("LocalPassword credential setting is invalid.");
            current = Parse(value, default, 0);
        }
        else
        {
            path = value;
            current = Load(value); // start-up refuses a missing, invalid or non-private file
        }
    }

    static Snapshot Load(string file)
    {
        var info = new FileInfo(file);
        if (!info.Exists || info.Length is < 1 or > 65536 ||
            (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(file) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0))
            throw new InvalidOperationException("LocalPassword credential file is missing, invalid or not private.");
        return Parse(File.ReadAllText(file), info.LastWriteTimeUtc, info.Length);
    }

    static Snapshot Parse(string json, DateTime written, long length)
    {
        var parsed = JsonSerializer.Deserialize<FileFormat>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        // An empty list is a safe bootstrap state: the review database can seed without a login.
        if (parsed?.Users is not { Length: <= 64 }) throw new InvalidOperationException("LocalPassword credential setting is invalid.");
        var users = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in parsed.Users)
        {
            if (!Guid.TryParse(user.UserId, out _) || string.IsNullOrWhiteSpace(user.UserName) || user.UserName.Length > 64 ||
                user.UserName.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '@' or '-')) ||
                !Hex(user.Salt, 16) || !Hex(user.Hash, 32) || !users.TryAdd(user.UserName, user))
                throw new InvalidOperationException("LocalPassword credential file contains an invalid or duplicate user.");
        }
        var stamps = new Dictionary<Guid, string>();
        foreach (var user in users.Values)
            if (!stamps.TryAdd(Guid.Parse(user.UserId), Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(user.Salt + user.Hash)))[..16]))
                throw new InvalidOperationException("LocalPassword credential file repeats an account.");
        return new Snapshot(users, stamps, written, length);
    }

    static bool Hex(string value, int bytes) => value.Length == bytes * 2 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// The verifiers now in force. A changed file is re-read; one that has become unreadable, invalid or
    /// non-private admits no one until it is fixed, so a broken edit can never keep a removed login alive.
    Snapshot Current()
    {
        if (path is null) return current;
        var info = new FileInfo(path);
        var (written, length) = info.Exists ? (info.LastWriteTimeUtc, info.Length) : (default, -1L);
        var known = current;
        if (known.Written == written && known.Length == length) return known;
        lock (gate)
        {
            if (current.Written != written || current.Length != length)
                try { current = Load(path); }
                catch (Exception) { current = new Snapshot(new(StringComparer.OrdinalIgnoreCase), [], written, length); }
            return current;
        }
    }

    public Guid? Verify(string userName, string password) => Verify(userName, password, out _);

    /// Checks one login and, on success, returns the stamp of the verifier it matched, to bind the session to.
    public Guid? Verify(string userName, string password, out string? stamp)
    {
        stamp = null;
        if (userName.Length is < 1 or > 64 || password.Length is < 12 or > 1024 || password.Any(c => c is '\r' or '\n' or '\0')) return null;
        var snapshot = Current();
        var matched = snapshot.Users.TryGetValue(userName, out var entry);
        var salt = matched ? Convert.FromHexString(entry!.Salt) : decoySalt;
        var expected = matched ? Convert.FromHexString(entry!.Hash) : decoyHash;
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected) || !matched) return null;
        var id = Guid.Parse(entry!.UserId);
        stamp = snapshot.Stamps[id];
        return id;
    }

    /// The stamp of a person's current verifier, or null when they have none. A session issued with any other
    /// stamp has outlived a rotated or removed password and is ended.
    public string? Stamp(Guid userId) => Current().Stamps.GetValueOrDefault(userId);
}

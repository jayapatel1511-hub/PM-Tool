using System.Security.Cryptography;
using System.Text.Json;

namespace Hub.Api.Infrastructure;

/// <summary>Review-only credentials. The JSON file is mounted outside Git and never returned by an API.</summary>
public sealed class LocalPasswordStore
{
    public sealed record Entry(string UserId, string UserName, string Salt, string Hash);
    sealed record FileFormat(Entry[] Users);
    readonly Dictionary<string, Entry> users;
    readonly byte[] decoySalt = RandomNumberGenerator.GetBytes(16);
    readonly byte[] decoyHash = RandomNumberGenerator.GetBytes(32);
    public const int Iterations = 600_000;

    public LocalPasswordStore(string value, bool isJson = false)
    {
        string json;
        if (isJson)
        {
            if (value.Length is < 1 or > 65536) throw new InvalidOperationException("LocalPassword credential setting is invalid.");
            json = value;
        }
        else
        {
            var info = new FileInfo(value);
            if (!info.Exists || info.Length is < 1 or > 65536 ||
                (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(value) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0))
                throw new InvalidOperationException("LocalPassword credential file is missing, invalid or not private.");
            json = File.ReadAllText(value);
        }
        var parsed = JsonSerializer.Deserialize<FileFormat>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        // An empty list is a safe bootstrap state: the review database can seed without a login.
        if (parsed?.Users is not { Length: <= 64 }) throw new InvalidOperationException("LocalPassword credential setting is invalid.");
        users = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in parsed.Users)
        {
            if (!Guid.TryParse(user.UserId, out _) || string.IsNullOrWhiteSpace(user.UserName) || user.UserName.Length > 64 ||
                user.UserName.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '@' or '-')) ||
                !Hex(user.Salt, 16) || !Hex(user.Hash, 32) || !users.TryAdd(user.UserName, user))
                throw new InvalidOperationException("LocalPassword credential file contains an invalid or duplicate user.");
        }
        if (users.Values.Select(u => u.UserId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != users.Count)
            throw new InvalidOperationException("LocalPassword credential file repeats an account.");
    }

    static bool Hex(string value, int bytes) => value.Length == bytes * 2 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public Guid? Verify(string userName, string password)
    {
        if (userName.Length is < 1 or > 64 || password.Length is < 12 or > 1024 || password.Any(c => c is '\r' or '\n' or '\0')) return null;
        var matched = users.TryGetValue(userName, out var entry);
        var salt = matched ? Convert.FromHexString(entry!.Salt) : decoySalt;
        var expected = matched ? Convert.FromHexString(entry!.Hash) : decoyHash;
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actual, expected) && matched ? Guid.Parse(entry!.UserId) : null;
    }
}

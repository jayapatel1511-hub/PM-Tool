using System.Text.RegularExpressions;

namespace Hub.Domain;

/// Document and project links (§12.7): pointers only, recognised by pattern (DOC-01, DOC-02, FR-DOC-02).
public static partial class Links
{
    /// The whole path: \\server\share, then any further segments and an optional closing backslash; no segment holds a
    /// character Windows refuses in a path, a control character or a line break. Any server is accepted (no allow-list setting).
    [GeneratedRegex(@"^\\\\[^\\/:*?""<>|\x00-\x1f]+(?:\\[^\\/:*?""<>|\x00-\x1f]+)+\\?\z", RegexOptions.CultureInvariant)]
    private static partial Regex Unc();

    public static bool IsUnc(string url) => Unc().IsMatch(url);

    /// DOC-01: http(s) addresses or UNC network paths only.
    public static bool IsValid(string url) =>
        IsUnc(url) || (Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp));

    public static string Detect(string url)
    {
        if (IsUnc(url)) return LinkType.NetworkFolder;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return LinkType.Other;
        var host = u.Host.ToLowerInvariant();
        if (host.EndsWith("-my.sharepoint.com") || host is "onedrive.live.com" or "1drv.ms") return LinkType.OneDrive;
        if (host.EndsWith(".sharepoint.com")) return LinkType.SharePoint;
        if (host is "teams.microsoft.com" or "teams.live.com" || host.EndsWith(".teams.microsoft.com")) return LinkType.Teams;
        return LinkType.Other;
    }

    /// DOC-02: the title defaults to the last path segment.
    public static string DefaultTitle(string url)
    {
        var path = IsUnc(url) ? url : Uri.TryCreate(url, UriKind.Absolute, out var u) ? Uri.UnescapeDataString(u.AbsolutePath) : url;
        var seg = path.TrimEnd('/', '\\').Split('/', '\\').LastOrDefault(s => s.Length > 0);
        return string.IsNullOrEmpty(seg) ? url : seg;
    }
}

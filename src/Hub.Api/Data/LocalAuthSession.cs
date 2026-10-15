namespace Hub.Api.Data;

/// Local-password tickets are private authentication state, never project activity.
public sealed class LocalAuthSession
{
    public Guid Id { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string Ticket { get; set; } = "";
}

namespace Hub.Api.Data;

public sealed class ExternalIdentity
{
    public string Provider { get; set; } = "";
    public string Subject { get; set; } = "";
    public Guid UserId { get; set; }
}

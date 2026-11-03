namespace Hub.Api.Data;

/// Organisation vocabulary only; no person counts or qualification verification.
public class CertificationTag : RefData { }

/// A person's recorded tag and optional expiry. Warning state is derived, never stored.
public class UserCertification : Audited, IAuditable
{
    public Guid UserId { get; set; }
    public Guid CertificationTagId { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string AuditType => "UserCertification";
    public Guid? AuditProjectId => null;
    public string? AuditKey => UserId.ToString();
    public string? AuditName => CertificationTagId.ToString();
}

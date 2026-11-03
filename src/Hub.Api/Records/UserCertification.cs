using Hub.Api.Data;
namespace Hub.Api.Records;
public static class UserCertificationRecord { public static RegistryEntry Entry { get; } = new(typeof(UserCertification), "UserCertification", ["UserId", "CertificationTagId", "ExpiresOn"], false, "personal facts; only permission-scoped readers and administrative history"); }

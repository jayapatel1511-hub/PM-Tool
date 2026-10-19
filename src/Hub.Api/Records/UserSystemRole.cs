using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class UserSystemRoleRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.UserSystemRole), "User", ["UserId", "Role", "Source"], false, "no item-history surface today; child and organisation history retain existing queries"); }

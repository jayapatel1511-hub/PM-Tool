using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ProjectMemberRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ProjectMember), "Member", ["UserId", "Roles", "PrimaryDisciplineId", "ResponsibilityNote", "RemovedAt"], false, "no item-history surface today; child and organisation history retain existing queries"); }

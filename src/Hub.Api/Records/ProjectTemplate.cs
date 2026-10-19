using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ProjectTemplateRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ProjectTemplate), "Template", ["Name", "Description", "ProjectTypeId", "Version", "Status", "OffsetDayKind"], false, "no item-history surface today; child and organisation history retain existing queries"); }

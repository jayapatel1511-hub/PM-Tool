using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class TaskTimeEntryRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.TaskTimeEntry), "TimeEntry", ["TaskId", "UserId", "WorkDate", "Hours", "Note", "DeletedAt"], false, "personal record; no item-history surface today");     public static readonly Hub.Api.Features.Col[] TimeCols = [
        new("workDate", "date", "date"), new("person", "person"), new("projectNumber", "project"), new("taskKey", "key"), new("taskName", "task"),
        new("hours", "hours", "number"), new("note", "note"),
    ];
}

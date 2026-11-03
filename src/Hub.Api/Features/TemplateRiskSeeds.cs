using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record TemplateRiskIn(string Ref, string Title, int? Probability, int? Impact, string? Mitigation = null, string? TriggerIndicator = null)
{
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
}

/// S40: declarations copied once into ordinary project risks, never a live template binding.
public static class TemplateRiskSeeds
{
    public const string Flag = "register_screens";
    public static Task<List<TemplateRisk>> Load(HubDb db, Guid id) => db.TemplateRisks.AsNoTracking().Where(row => row.TemplateId == id)
        .OrderBy(row => row.SortOrder).ThenBy(row => row.Id).ToListAsync();
    public static TemplateRiskIn[] Wire(IEnumerable<TemplateRisk> rows) => rows.Select(row => new TemplateRiskIn(row.Id.ToString(), row.Title,
        row.Probability, row.Impact, row.Mitigation, row.TriggerIndicator)).ToArray();

    public static List<TemplateRisk> Replace(Guid id, TemplateRiskIn[]? input, IReadOnlyList<TemplateRisk> previous, bool enabled)
    {
        if (input is not null && !enabled) throw ApiException.NotFound();
        if (input is null) return previous.ToList();
        Check.That(input.All(item => item is not null), "risks", "error.invalid");
        var seen = new HashSet<string>(StringComparer.Ordinal); var old = previous.Select(row => row.Id).ToHashSet();
        return input.Select((item, index) => {
            Check.That(item.Extra is null || item.Extra.Count == 0, "risks", "error.invalid");
            Check.That(!string.IsNullOrWhiteSpace(item.Ref), "risks", "template.bad_ref");
            var parsed = Guid.TryParse(item.Ref, out var existing);
            Check.That(seen.Add(parsed ? existing.ToString() : item.Ref), "risks", "template.bad_ref");
            Check.That(!parsed || old.Contains(existing), "risks", "template.bad_ref");
            Check.That(item.Probability is >= 1 and <= 3, "risks", "risk.level");
            Check.That(item.Impact is >= 1 and <= 3, "risks", "risk.level");
            return new TemplateRisk { Id = parsed ? existing : Guid.CreateVersion7(), TemplateId = id, SortOrder = index,
                Title = Check.Required(item.Title, "risks", 300), Probability = item.Probability!.Value, Impact = item.Impact!.Value,
                Mitigation = Check.Optional(item.Mitigation, "risks", 4000), TriggerIndicator = Check.Optional(item.TriggerIndicator, "risks", 2000) };
        }).ToList();
    }

    public static TemplateRisk Clone(TemplateRisk source, Guid templateId, Guid id) => new() {
        Id = id, TemplateId = templateId, SortOrder = source.SortOrder, Title = source.Title, Probability = source.Probability,
        Impact = source.Impact, Mitigation = source.Mitigation, TriggerIndicator = source.TriggerIndicator
    };

    public static async Task Instantiate(HubDb db, Project project, IReadOnlyList<TemplateRisk> seeds, DateTimeOffset now)
    {
        if (seeds.Count == 0) return;
        await DeliverableEndpoints.ActivePerson(db, project.ProjectManagerId, "ownerId");
        var next = await Keys.Reserve(db, project.Id, "risk", seeds.Count);
        foreach (var seed in seeds)
        {
            // Revalidate before any project commit; corrupt source data rolls back the outer create transaction.
            Check.That(seed.Probability is >= 1 and <= 3 && seed.Impact is >= 1 and <= 3, "risks", "risk.level");
            var seq = next++;
            db.Risks.Add(new Risk { ProjectId = project.Id, Seq = seq, Key = Keys.Format(project.ProjectNumber, "risk", seq),
                TemplateRiskId = seed.Id, Title = Check.Required(seed.Title, "risks", 300), Probability = seed.Probability, Impact = seed.Impact,
                Mitigation = Check.Optional(seed.Mitigation, "risks", 4000), TriggerIndicator = Check.Optional(seed.TriggerIndicator, "risks", 2000),
                OwnerId = project.ProjectManagerId, Status = RiskStatus.Open, StatusChangedAt = now, LastActivityAt = now });
        }
    }
}

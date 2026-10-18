using System.Text.Json;
using Hub.Api.Infrastructure;

namespace Hub.Api.Features;

/// Registration point for each packet's services and endpoints, so Program.cs stays stable.
public static class HubModules
{
    public static void AddServices(IServiceCollection s, IConfiguration cfg)
    {
        // A malformed route or query value reaches ProblemMiddleware as a problem body in every environment (§25.6).
        s.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
        s.AddScoped<Notifier>();
        s.AddScoped<TeamService>();
        s.AddScoped<IProjectCreateHook, CopyStructureHook>();
        s.AddScoped<IProjectCreateHook, TemplateHook>();
        s.AddScoped<EvaluationService>();
        s.AddHostedService<EvaluationWorker>();
        s.AddSingleton<IJob, EvaluationRetryJob>();
        s.AddSingleton<IJob, NightlyJob>();
        s.AddSingleton<IJob, EmailJob>();
        s.AddSingleton<IJob, DigestJob>();
        s.AddSingleton<IJob, WeeklySummaryJob>();
        s.AddSingleton<IJob, OpsWatchdogJob>();
        if (cfg["Graph:Mail"] == "true") s.AddSingleton<IEmailSender, GraphEmailSender>();
        else if (cfg["Email:Mode"] == "Smtp") s.AddSingleton<IEmailSender, SmtpEmailSender>();
        else s.AddSingleton<IEmailSender, LogEmailSender>();
    }

    public static void Map(RouteGroupBuilder api)
    {
        ProjectEndpoints.Map(api);
        TeamEndpoints.Map(api);
        MilestoneEndpoints.Map(api);
        DeliverableEndpoints.Map(api);
        TaskEndpoints.Map(api);
        DependencyEndpoints.Map(api);
        EvaluationEndpoints.Map(api);
        CommentEndpoints.Map(api);
        DocumentLinkEndpoints.Map(api);
        NotificationEndpoints.Map(api);
        DashboardEndpoints.Map(api);
        DisciplineCoordinationEndpoints.Map(api);
        MyWorkEndpoints.Map(api);
        DecisionEndpoints.Map(api);
        SearchEndpoints.Map(api);
        ReportEndpoints.Map(api);
        ListExportEndpoints.Map(api);
        ReassignEndpoints.Map(api);
        PortfolioEndpoints.Map(api);
        WorkloadEndpoints.Map(api);
        PlanningEndpoints.Map(api);
        AvailabilityEndpoints.Map(api);
        WeeklyPlanningEndpoints.Map(api);
        CalendarFeedEndpoints.Map(api);
        AllocationEndpoints.Map(api);
        OpenRoleEndpoints.Map(api);
        TimelineEndpoints.Map(api);
        ViewEndpoints.Map(api);
        CalendarEndpoints.Map(api);
        TimeEndpoints.Map(api);
        WorkspaceEndpoints.Map(api);
        TemplateEndpoints.Map(api);
        DecisionExtraEndpoints.Map(api);
        RegisterEndpoints.Map(api);
        MeetingEndpoints.Map(api);
        HandoffEndpoints.Map(api);
        ReviewEndpoints.Map(api);
        ChangeEndpoints.Map(api);
        SubmissionEndpoints.Map(api);
        DesignBasisEndpoints.Map(api);
        ReadinessEndpoints.Map(api);
        WeeklyCommitmentsEndpoints.Map(api);
        RecoveryEndpoints.Map(api);
        // Support: an Admin can re-run a project's evaluation on demand.
        api.MapPost("/admin/evaluate/{projectId:guid}", async (Guid projectId, CurrentUser me, EvaluationService eval) =>
        {
            Access.Demand(Hub.Domain.Permissions.Administer(me.Actor));
            await eval.EvaluateProject(projectId);
            return Results.NoContent();
        });
        api.MapPost("/client-errors", async (HttpContext ctx, CurrentUser me, ILoggerFactory logs) =>
        {
            if (ctx.Request.ContentLength is > 8192) throw ApiException.Invalid("body", "error.too_long", 8192);
            var bytes = new byte[8193]; var length = 0;
            while (length < bytes.Length)
            {
                var read = await ctx.Request.Body.ReadAsync(bytes.AsMemory(length, bytes.Length - length));
                if (read == 0) break;
                length += read;
            }
            if (length > 8192) throw ApiException.Invalid("body", "error.too_long", 8192);
            ClientErrorBody body;
            try { body = JsonSerializer.Deserialize<ClientErrorBody>(bytes.AsSpan(0, length), JsonOpts.Web) ?? throw new JsonException(); }
            catch (JsonException) { throw ApiException.Invalid("body", "error.bad_json"); }
            static string Clean(string? value, int max = 200)
            {
                var clean = new string((value ?? "").Where(c => !char.IsControl(c)).ToArray()).Replace("\r", " ").Replace("\n", " ");
                return clean[..Math.Min(max, clean.Length)];
            }
            logs.CreateLogger("Hub.ClientErrors").LogWarning("Client error {Reference} kind={Kind} route={Route} build={Build} user={User} name={Name} frame={Frame}: {Message}",
                Clean(body.Reference, 256), Clean(body.Kind, 32), Clean(body.Route, 200), Clean(body.Build, 64), me.Id, Clean(body.Name, 80), Clean(body.StackFrame, 200), Clean(body.Message));
            return Results.NoContent();
        }).RequireRateLimiting("client-errors");
        // Public client configuration for the SPA sign-in (no secrets: client and tenant IDs are public values).
        api.MapGet("/config", (IConfiguration cfg, IHostEnvironment env) => new
        {
            authMode = AuthSetup.DevAuthAllowed(env, cfg) ? "Development" : AuthSetup.LocalAuthAllowed(env, cfg) ? "LocalPassword" : cfg["Auth:Mode"] == SocialAuth.Scheme ? "Social" : "Entra",
            providers = cfg["Auth:Mode"] == SocialAuth.Scheme ? SocialAuth.Providers.Where(p => SocialAuth.Configured(cfg, p)).ToArray() : [],
            entra = new { clientId = cfg["Auth:Entra:SpaClientId"], tenantId = cfg["Auth:Entra:TenantId"], apiScope = cfg["Auth:Entra:ApiScope"] },
        }).AllowAnonymous();
    }

    public sealed record ClientErrorBody(string? Reference, string? Route, string? Build, string? Message, string? Kind = null, string? Name = null, string? StackFrame = null);
}

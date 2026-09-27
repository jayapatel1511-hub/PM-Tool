using Hub.Api.Infrastructure;

namespace Hub.Api.Features;

/// Registration point for each packet's services and endpoints, so Program.cs stays stable.
public static class HubModules
{
    public static void AddServices(IServiceCollection s, IConfiguration cfg)
    {
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
        MyWorkEndpoints.Map(api);
        DecisionEndpoints.Map(api);
        SearchEndpoints.Map(api);
        ReportEndpoints.Map(api);
        ListExportEndpoints.Map(api);
        ReassignEndpoints.Map(api);
        PortfolioEndpoints.Map(api);
        WorkloadEndpoints.Map(api);
        AllocationEndpoints.Map(api);
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
        // Support: an Admin can re-run a project's evaluation on demand.
        api.MapPost("/admin/evaluate/{projectId:guid}", async (Guid projectId, CurrentUser me, EvaluationService eval) =>
        {
            Access.Demand(Hub.Domain.Permissions.Administer(me.Actor));
            await eval.EvaluateProject(projectId);
            return Results.NoContent();
        });
        // Public client configuration for the SPA sign-in (no secrets: client and tenant IDs are public values).
        api.MapGet("/config", (IConfiguration cfg, IHostEnvironment env) => new
        {
            authMode = AuthSetup.DevAuthAllowed(env, cfg) ? "Development" : AuthSetup.LocalAuthAllowed(env, cfg) ? "LocalPassword" : "Entra",
            entra = new { clientId = cfg["Auth:Entra:SpaClientId"], tenantId = cfg["Auth:Entra:TenantId"], apiScope = cfg["Auth:Entra:ApiScope"] },
        }).AllowAnonymous();
    }
}

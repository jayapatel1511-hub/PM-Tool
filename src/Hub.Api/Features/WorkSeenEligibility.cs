using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hub.Api.Features;

/// One typed stream relation drives both decoration and the atomic INSERT/SELECT acknowledgement cut.
public static class WorkSeenEligibility
{
    static string Table<T>(HubDb db) where T : class
    {
        var entity = db.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException("Missing seen-state relation.");
        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        return Quote(entity.GetSchema() ?? "hub") + "." + Quote(entity.GetTableName()!);
    }
    sealed record Query(string Cte, object[] Parameters);
    static Query Build(HubDb db, Access access, OrgSettings settings, WorkSeenContext context, IReadOnlyCollection<WorkSeenReadBound>? items)
    {
        var args = new List<object> {
            new NpgsqlParameter("me", access.Actor.Id), new NpgsqlParameter("all_restricted", access.SeesAllRestricted),
            new NpgsqlParameter("follow_up", settings.IsFlagOn("my_work_follow_up")),
            new NpgsqlParameter("context_on", settings.IsFlagOn("screen_patterns")),
            new NpgsqlParameter("now", context.Now),
            new NpgsqlParameter("recent", context.Now.AddDays(-14)),
            new NpgsqlParameter("today", context.Today),
            new NpgsqlParameter("last_day", DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber,
                context.Today.DayNumber + Math.Max(0, settings.CoordinationLookaheadWeeks * 7 - 1))))
        };
        // Identifiers come only from the maintained EF model. All actor/time/flag/bound data is parameterized.
        var cte = $"""
            WITH visible AS (
              SELECT p.* FROM {Table<Project>(db)} p
              WHERE @all_restricted OR p.visibility <> 'Restricted' OR p.project_manager_id=@me
                OR EXISTS(SELECT 1 FROM {Table<ProjectMember>(db)} m WHERE m.project_id=p.id AND m.user_id=@me AND m.removed_at IS NULL)
            ), live_projects AS (
              SELECT id FROM visible WHERE status NOT IN ('Archived','Cancelled')
            ), mine_tasks AS (
              SELECT t.* FROM {Table<WorkTask>(db)} t WHERE t.deleted_at IS NULL
                AND t.project_id IN (SELECT id FROM live_projects)
                AND t.assignee_id=@me
              UNION ALL SELECT t.* FROM {Table<WorkTask>(db)} t WHERE t.deleted_at IS NULL
                AND t.project_id IN (SELECT id FROM live_projects) AND t.assignee_id IS DISTINCT FROM @me
                AND EXISTS(SELECT 1 FROM {Table<TaskCollaborator>(db)} c WHERE c.task_id=t.id AND c.user_id=@me)
            ), eligible_items AS (
              SELECT 'Task'::text item_type,t.id item_id FROM mine_tasks t WHERE t.status <> 'Cancelled'
              UNION SELECT 'Task',t.id FROM {Table<WorkTask>(db)} t WHERE t.deleted_at IS NULL
                AND t.project_id IN(SELECT id FROM live_projects) AND t.created_by=@me AND t.status<>'Cancelled'
              UNION SELECT 'Task',t.id FROM {Table<WorkTask>(db)} t WHERE t.deleted_at IS NULL
                AND t.project_id IN(SELECT id FROM live_projects) AND t.reviewer_id=@me AND t.status IN('Ready for Review','In Review')
              UNION SELECT 'Deliverable',d.id FROM {Table<Deliverable>(db)} d WHERE d.deleted_at IS NULL
                AND d.project_id IN(SELECT id FROM live_projects)
                AND (d.owner_id=@me AND d.status NOT IN('Issued','Accepted','Cancelled') OR d.reviewer_id=@me AND d.status='In Review')
              UNION SELECT 'Decision',d.id FROM {Table<Decision>(db)} d WHERE d.deleted_at IS NULL
                AND d.project_id IN(SELECT id FROM live_projects) AND (d.owner_user_id=@me OR d.requested_by_id=@me)
                AND d.status IN('Pending','Under Review','Deferred')
              UNION SELECT 'Action',a.id FROM {Table<MeetingAction>(db)} a WHERE a.deleted_at IS NULL
                AND a.project_id IN(SELECT id FROM live_projects) AND a.status IN('Open','In Progress')
                AND a.owner_type='User' AND a.owner_user_id=@me
              UNION SELECT 'Project',p.id FROM visible p WHERE p.id IN(SELECT id FROM live_projects) AND p.project_manager_id=@me
              UNION SELECT a.item_type,a.item_id FROM {Table<AttentionItem>(db)} a
                WHERE a.project_id IN(SELECT id FROM live_projects) AND @me=ANY(a.route_to_user_ids)
                AND NOT EXISTS(SELECT 1 FROM {Table<AttentionSnooze>(db)} z
                  WHERE z.rule_id=a.rule_id AND z.item_type=a.item_type AND z.item_id=a.item_id
                    AND z.ended_at IS NULL AND z.snoozed_until>@now
                    AND (CASE a.severity WHEN 'Critical' THEN 0 WHEN 'Warning' THEN 1 ELSE 2 END)
                      >=(CASE z.severity_at_snooze WHEN 'Critical' THEN 0 WHEN 'Warning' THEN 1 ELSE 2 END))
              UNION SELECT w.item_type,w.item_id FROM {Table<ItemWatcher>(db)} w WHERE @follow_up
                AND w.project_id IN(SELECT id FROM visible) AND w.user_id=@me AND w.source IN('Manual','Mention')
                AND (
                  w.item_type='Task' AND EXISTS(SELECT 1 FROM {Table<WorkTask>(db)} t WHERE t.id=w.item_id AND t.deleted_at IS NULL AND t.status NOT IN('Complete','Cancelled'))
                  OR w.item_type='Deliverable' AND EXISTS(SELECT 1 FROM {Table<Deliverable>(db)} d WHERE d.id=w.item_id AND d.deleted_at IS NULL AND d.status NOT IN('Issued','Accepted','Cancelled'))
                  OR w.item_type='Decision' AND EXISTS(SELECT 1 FROM {Table<Decision>(db)} d WHERE d.id=w.item_id AND d.deleted_at IS NULL AND d.status IN('Pending','Under Review','Deferred'))
                  OR w.item_type='Risk' AND EXISTS(SELECT 1 FROM {Table<Risk>(db)} r WHERE r.id=w.item_id AND r.deleted_at IS NULL AND r.status IN('Open','Monitoring'))
                  OR w.item_type='Issue' AND EXISTS(SELECT 1 FROM {Table<Issue>(db)} i WHERE i.id=w.item_id AND i.deleted_at IS NULL AND i.status IN('Open','In Progress'))
                  OR w.item_type='Action' AND EXISTS(SELECT 1 FROM {Table<MeetingAction>(db)} a WHERE a.id=w.item_id AND a.deleted_at IS NULL AND a.status IN('Open','In Progress'))
                )
              UNION SELECT 'Decision',d.id FROM {Table<Decision>(db)} d WHERE @follow_up AND d.deleted_at IS NULL
                AND d.project_id IN(SELECT id FROM visible) AND d.requested_by_id=@me
                AND (d.status IN('Pending','Under Review','Deferred') OR COALESCE(
                  (SELECT MAX(l.occurred_at) FROM {Table<ActivityLog>(db)} l WHERE l.item_type='Decision' AND l.item_id=d.id AND 'status'=ANY(l.categories)),
                  d.status_changed_at,d.created_at)>=@recent)
              UNION SELECT 'Handoff',h.id FROM {Table<Handoff>(db)} h WHERE @follow_up AND h.deleted_at IS NULL
                AND h.project_id IN(SELECT id FROM visible) AND h.created_by=@me
                AND (h.status NOT IN('Incorporated','Cancelled') OR COALESCE(
                  (SELECT MAX(l.occurred_at) FROM {Table<ActivityLog>(db)} l WHERE l.item_type='Handoff' AND l.item_id=h.id AND 'status'=ANY(l.categories)),h.created_at)>=@recent)
              UNION SELECT 'ChangeNotice',c.id FROM {Table<ChangeNotice>(db)} c WHERE @follow_up AND c.deleted_at IS NULL
                AND c.project_id IN(SELECT id FROM visible) AND c.created_by=@me
                AND (c.status NOT IN('Closed','Cancelled') OR COALESCE(
                  (SELECT MAX(l.occurred_at) FROM {Table<ActivityLog>(db)} l WHERE l.item_type='ChangeNotice' AND l.item_id=c.id AND 'status'=ANY(l.categories)),c.created_at)>=@recent)
              UNION SELECT 'Handoff',h.id FROM {Table<Handoff>(db)} h WHERE h.deleted_at IS NULL
                AND h.project_id IN(SELECT id FROM live_projects) AND
                (h.sending_owner_id=@me AND h.status NOT IN('Incorporated','Cancelled')
                 OR h.receiving_owner_id=@me AND h.status IN('Submitted','Clarification Requested','Returned','Accepted'))
              UNION SELECT 'Handoff',h.id FROM {Table<Handoff>(db)} h WHERE h.deleted_at IS NULL
                AND h.project_id IN(SELECT id FROM live_projects) AND h.status NOT IN('Cancelled','Incorporated','Accepted')
                AND EXISTS(SELECT 1 FROM {Table<WorkTask>(db)} t WHERE t.id=h.target_task_id AND t.project_id=h.project_id
                  AND t.deleted_at IS NULL AND t.status NOT IN('Complete','Cancelled'))
              UNION SELECT 'ChangeNotice',c.id FROM {Table<ChangeNotice>(db)} c WHERE c.deleted_at IS NULL
                AND c.project_id IN(SELECT id FROM live_projects)
                AND (c.status='Open' OR EXISTS(SELECT 1 FROM {Table<ChangeAssessment>(db)} a WHERE a.change_notice_id=c.id
                  AND a.project_id=c.project_id AND a.status='Pending Assessment'))
              UNION SELECT 'Issue',i.id FROM {Table<Issue>(db)} i WHERE @context_on AND i.deleted_at IS NULL
                AND i.project_id IN(SELECT id FROM live_projects) AND EXISTS(
                  SELECT 1 FROM {Table<ReviewFinding>(db)} f JOIN {Table<ReviewPackage>(db)} p ON p.id=f.package_id AND p.current_round_id=f.round_id
                  WHERE f.project_id=i.project_id AND f.issue_id=i.id)
              UNION SELECT u.target_type,u.target_id FROM {Table<InputUse>(db)} u WHERE @context_on AND u.project_id IN(SELECT id FROM live_projects)
                AND (u.target_type='Task' AND EXISTS(SELECT 1 FROM {Table<WorkTask>(db)} t WHERE t.id=u.target_id AND t.project_id=u.project_id AND t.deleted_at IS NULL)
                  OR u.target_type='Deliverable' AND EXISTS(SELECT 1 FROM {Table<Deliverable>(db)} d WHERE d.id=u.target_id AND d.project_id=u.project_id AND d.deleted_at IS NULL))
              UNION SELECT a.target_type,a.target_id FROM {Table<ReadinessAssessment>(db)} a
                WHERE a.project_id IN(SELECT id FROM live_projects) AND (
                  a.target_type='Task' AND EXISTS(SELECT 1 FROM {Table<WorkTask>(db)} t WHERE t.id=a.target_id AND t.project_id=a.project_id AND t.deleted_at IS NULL
                    AND t.due_date BETWEEN @today AND @last_day AND t.status NOT IN('Complete','Cancelled','On Hold'))
                  OR a.target_type='Deliverable' AND EXISTS(SELECT 1 FROM {Table<Deliverable>(db)} d WHERE d.id=a.target_id AND d.project_id=a.project_id AND d.deleted_at IS NULL
                    AND d.due_date BETWEEN @today AND @last_day AND d.status NOT IN('Issued','Accepted','Cancelled','On Hold')))
            ), canonical_roles AS (
              SELECT 'Task'::text item_type,t.id item_id,t.project_id,t.row_version read_version,'Assignee'::text role,t.assignee_id recipient_id FROM {Table<WorkTask>(db)} t WHERE t.deleted_at IS NULL
              UNION ALL SELECT 'Task',t.id,t.project_id,t.row_version,'Reviewer',t.reviewer_id FROM {Table<WorkTask>(db)} t WHERE t.deleted_at IS NULL
              UNION ALL SELECT 'Deliverable',d.id,d.project_id,d.row_version,'Owner',d.owner_id FROM {Table<Deliverable>(db)} d WHERE d.deleted_at IS NULL
              UNION ALL SELECT 'Deliverable',d.id,d.project_id,d.row_version,'Reviewer',d.reviewer_id FROM {Table<Deliverable>(db)} d WHERE d.deleted_at IS NULL
              UNION ALL SELECT 'Decision',d.id,d.project_id,d.row_version,'InternalOwner',d.owner_user_id FROM {Table<Decision>(db)} d WHERE d.deleted_at IS NULL AND d.owner_external_party_id IS NULL
              UNION ALL SELECT 'Action',a.id,a.project_id,a.row_version,'InternalOwner',a.owner_user_id FROM {Table<MeetingAction>(db)} a WHERE a.deleted_at IS NULL AND a.owner_type='User'
              UNION ALL SELECT 'Risk',r.id,r.project_id,r.row_version,'Owner',r.owner_id FROM {Table<Risk>(db)} r WHERE r.deleted_at IS NULL
              UNION ALL SELECT 'Issue',i.id,i.project_id,i.row_version,'Owner',i.owner_id FROM {Table<Issue>(db)} i WHERE i.deleted_at IS NULL
              UNION ALL SELECT 'ChangeNotice',c.id,c.project_id,c.row_version,'Owner',c.owner_id FROM {Table<ChangeNotice>(db)} c WHERE c.deleted_at IS NULL
              UNION ALL SELECT 'Handoff',h.id,h.project_id,h.row_version,'SendingOwner',h.sending_owner_id FROM {Table<Handoff>(db)} h WHERE h.deleted_at IS NULL
              UNION ALL SELECT 'Handoff',h.id,h.project_id,h.row_version,'ReceivingOwner',h.receiving_owner_id FROM {Table<Handoff>(db)} h WHERE h.deleted_at IS NULL
              UNION ALL SELECT 'Project',p.id,p.id,p.row_version,'ProjectManager',p.project_manager_id FROM visible p
            ), eligible_roles AS (
              SELECT r.* FROM canonical_roles r JOIN eligible_items e USING(item_type,item_id)
              WHERE r.recipient_id=@me AND r.project_id IN(SELECT id FROM visible)
            )
            """;
        var source = "eligible_roles";
        if (items is not null)
        {
            args.Add(new NpgsqlParameter("bounds", JsonSerializer.Serialize(items.Select(item => new {
                itemType = item.Key.ItemType, itemId = item.Key.ItemId, item.ProjectId, item.SourceVersion, item.Roles
            }), JsonOpts.Web)));
            cte += """
                , bounded_roles AS (
                  SELECT r.item_type,r.item_id,r.project_id,b."sourceVersion" read_version,r.role,r.recipient_id
                  FROM eligible_roles r JOIN jsonb_to_recordset(CAST(@bounds AS jsonb))
                    AS b("itemType" text,"itemId" uuid,"projectId" uuid,"sourceVersion" integer,roles jsonb)
                    ON b."itemType"=r.item_type AND b."itemId"=r.item_id AND b."projectId"=r.project_id
                  CROSS JOIN LATERAL jsonb_to_recordset(b.roles) AS v(role text,"recipientId" uuid)
                  WHERE v.role=r.role AND v."recipientId"=r.recipient_id AND b."sourceVersion">=0 AND b."sourceVersion"<=r.read_version
                )
                """;
            source = "bounded_roles";
        }
        cte += $"""
            , resolved AS (
              SELECT r.*,o.token,o.source_version,o.recipient_id occurrence_recipient FROM {source} r
              LEFT JOIN LATERAL (
                SELECT a.token,a.source_version,a.recipient_id FROM {Table<WorkAssignmentOccurrence>(db)} a
                WHERE a.item_type=r.item_type AND a.item_id=r.item_id AND a.role=r.role AND a.source_version<=r.read_version
                ORDER BY a.source_version DESC LIMIT 1
              ) o ON TRUE
            )
            """;
        return new(cte, args.ToArray());
    }
    sealed class TokenRow
    {
        public string ItemType { get; set; } = "";
        public Guid ItemId { get; set; }
        public string Role { get; set; } = "";
        public Guid? Token { get; set; }
        public int? SourceVersion { get; set; }
        public Guid? RecipientId { get; set; }
    }
    internal static bool Own(Access access, OrgSettings settings, WorkSeenContext context) =>
        settings.IsFlagOn("work_screens") && access.Actor.IsActive && access.Actor.Id != Guid.Empty && context.SubjectId == access.Actor.Id;

    public static async Task<IReadOnlyDictionary<WorkSeenKey, IReadOnlyList<WorkSeenObservedToken>>> ReadCurrentAsync(
        HubDb db, Access access, OrgSettings settings, WorkSeenContext context, IReadOnlyCollection<WorkSeenReadBound> items, CancellationToken ct = default)
    {
        if (!Own(access, settings, context) || items.Count == 0) return new Dictionary<WorkSeenKey, IReadOnlyList<WorkSeenObservedToken>>();
        var query = Build(db, access, settings, context, items);
        var rows = await db.Database.SqlQueryRaw<TokenRow>(query.Cte + "\n" + """
            SELECT item_type AS "ItemType",item_id AS "ItemId",role AS "Role",token AS "Token",
              source_version AS "SourceVersion",occurrence_recipient AS "RecipientId" FROM resolved
            """, query.Parameters).ToListAsync(ct);
        return rows.GroupBy(row => new WorkSeenKey(row.ItemType, row.ItemId)).ToDictionary(group => group.Key,
            group => (IReadOnlyList<WorkSeenObservedToken>)(group.Any(row => row.Token is null || row.RecipientId != access.Actor.Id)
                ? [] : group.Select(row => new WorkSeenObservedToken(row.Token!.Value, row.Role, row.SourceVersion!.Value)).Distinct().ToArray()));
    }
    sealed class CutResult
    {
        public int Missing { get; set; }
        public int Added { get; set; }
    }
    public static async Task<int> AcknowledgeAllAsync(HubDb db, Access access, OrgSettings settings, WorkSeenContext context, Guid requestId, CancellationToken ct = default)
    {
        if (!Own(access, settings, context)) throw ApiException.NotFound();
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("MarkAll requires the caller's personal transaction.");
        var query = Build(db, access, settings, context, null);
        var args = query.Parameters.Concat([new NpgsqlParameter("at", context.Now), new NpgsqlParameter("request", requestId)]).ToArray();
        var rows = await db.Database.SqlQueryRaw<CutResult>(query.Cte + "\n" + $"""
            , missing AS (SELECT 1 FROM resolved WHERE token IS NULL OR occurrence_recipient<>@me),
            inserted AS (
              INSERT INTO {Table<WorkSeenAcknowledgement>(db)} (recipient_id,token,acknowledged_at,provenance,request_id)
              SELECT @me,token,@at,'MarkAll',@request FROM resolved WHERE NOT EXISTS(SELECT 1 FROM missing)
              ON CONFLICT(recipient_id,token) DO NOTHING RETURNING token
            )
            SELECT (SELECT COUNT(*)::int FROM missing) AS "Missing",(SELECT COUNT(*)::int FROM inserted) AS "Added"
            """, args).ToListAsync(ct);
        var result = rows.Single();
        if (result.Missing != 0) throw ApiException.Conflict("work_seen_integrity", "error.invalid");
        return result.Added;
    }
}

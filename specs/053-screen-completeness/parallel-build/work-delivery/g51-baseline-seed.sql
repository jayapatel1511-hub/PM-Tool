-- PREPARATION ONLY: root will embed this after the additive EF tables/indexes/FKs exist.
-- Apply only as the ordinary migration within a quiescent application cutover: no old/new application writers.
-- The normal migration transaction holds these source locks through the complete baseline insertion.
-- Include hidden, archived, closed and soft-deleted canonical rows; first visit baselines history without exposing it.
-- No canonical update, version increment, activity insertion, inferred assignment time or private acknowledgement.
LOCK TABLE hub.task, hub.deliverable, hub.decision, hub.meeting_action, hub.risk,
    hub.issue, hub.handoff, hub.change_notice, hub.project IN SHARE MODE;
WITH legacy_roles AS (
    SELECT 'Task'::text item_type, id item_id, project_id, 'Assignee'::text role, assignee_id recipient_id, row_version source_version FROM hub.task
    UNION ALL SELECT 'Task', id, project_id, 'Reviewer', reviewer_id, row_version FROM hub.task
    UNION ALL SELECT 'Deliverable', id, project_id, 'Owner', owner_id, row_version FROM hub.deliverable
    UNION ALL SELECT 'Deliverable', id, project_id, 'Reviewer', reviewer_id, row_version FROM hub.deliverable
    UNION ALL SELECT 'Decision', id, project_id, 'InternalOwner', owner_user_id, row_version FROM hub.decision WHERE owner_external_party_id IS NULL
    UNION ALL SELECT 'Action', id, project_id, 'InternalOwner', owner_user_id, row_version FROM hub.meeting_action WHERE owner_type = 'User'
    UNION ALL SELECT 'Risk', id, project_id, 'Owner', owner_id, row_version FROM hub.risk
    UNION ALL SELECT 'Issue', id, project_id, 'Owner', owner_id, row_version FROM hub.issue
    UNION ALL SELECT 'ChangeNotice', id, project_id, 'Owner', owner_id, row_version FROM hub.change_notice
    UNION ALL SELECT 'Handoff', id, project_id, 'SendingOwner', sending_owner_id, row_version FROM hub.handoff
    UNION ALL SELECT 'Handoff', id, project_id, 'ReceivingOwner', receiving_owner_id, row_version FROM hub.handoff
    UNION ALL SELECT 'Project', id, id, 'ProjectManager', project_manager_id, row_version FROM hub.project
)
INSERT INTO hub.work_assignment_occurrence
    (token, recipient_id, item_type, item_id, project_id, role, source_version,
     source_activity_id, source_occurred_at, provenance, recorded_at)
SELECT gen_random_uuid(), recipient_id, item_type, item_id, project_id, role, source_version,
    NULL, NULL, 'BaselineSeed', clock_timestamp()
FROM legacy_roles
WHERE recipient_id IS NOT NULL AND recipient_id <> '00000000-0000-0000-0000-000000000000'::uuid;

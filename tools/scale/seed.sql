-- planning_only isolates the small packet 034 fixture from the legacy 100,000-task stress data.
\if :{?planning_only}
\else
\set planning_only false
\endif
\if :planning_only
SET search_path = hub;
BEGIN;
CREATE TEMP TABLE planning_scope AS SELECT gen_random_uuid() id, g n FROM generate_series(0,11) g;
INSERT INTO app_user(id,email,display_name,job_title,office_id,supervisor_id,is_active,is_template_editor,weekly_capacity_hours,created_at,updated_at,row_version)
SELECT id, CASE WHEN n=0 THEN 'planning-scale-supervisor@hub.test' ELSE format('planning-scale-%s@hub.test',n) END,
format('Synthetic planning scale person %s',n), 'Synthetic fixture', (SELECT office_id FROM app_user WHERE email='sam@hub.test'),
CASE WHEN n=0 THEN NULL ELSE (SELECT id FROM planning_scope WHERE n=0) END, true,false,40,now(),now(),1 FROM planning_scope
ON CONFLICT (email) DO NOTHING;
UPDATE planning_scope x SET id=u.id FROM app_user u WHERE u.email=CASE WHEN x.n=0 THEN 'planning-scale-supervisor@hub.test' ELSE format('planning-scale-%s@hub.test',x.n) END;
INSERT INTO user_system_role(id,user_id,role,source,granted_at)
SELECT gen_random_uuid(),id,'Supervisor','Manual',now() FROM planning_scope x WHERE n=0 AND NOT EXISTS(SELECT 1 FROM user_system_role r WHERE r.user_id=x.id AND r.role='Supervisor');
INSERT INTO project_member(id,project_id,user_id,roles,added_at)
SELECT gen_random_uuid(),p.id,x.id,'{TeamMember}',now() FROM planning_scope x CROSS JOIN project p WHERE p.project_number='SYN-101'
AND NOT EXISTS(SELECT 1 FROM project_member m WHERE m.project_id=p.id AND m.user_id=x.id);
-- Idempotent fixture marker; all records and people explicitly say synthetic.
INSERT INTO planning_entry(id,person_id,hours_per_week,start_week,end_week,label,source_category,project_id,confidence,visibility,notes,last_validated_at,created_by,updated_by,created_at,updated_at,row_version)
SELECT gen_random_uuid(),x.id,0.5+(g%8)*0.5,date_trunc('week',current_date)::date+(g%12)*7,date_trunc('week',current_date)::date+(g%12)*7,
format('Synthetic planning scale entry %s',g),'MajorProject',p.id,(ARRAY['Confirmed','Expected','Possible'])[1+g%3],
CASE WHEN x.n=0 THEN 'Confirmed' ELSE 'Published' END,'Synthetic packet 034 performance fixture',now(),sup.id,sup.id,now(),now(),1
FROM generate_series(1,500) g JOIN planning_scope x ON x.n=g%12 CROSS JOIN planning_scope sup CROSS JOIN project p
WHERE sup.n=0 AND p.project_number='SYN-101' AND NOT EXISTS(SELECT 1 FROM planning_entry e WHERE e.label=format('Synthetic planning scale entry %s',g));
INSERT INTO resource_allocation(id,person_id,project_id,purpose,from_date,through_date,planned_hours,status,confirmed_by,confirmed_at,over_capacity_reason,created_by,updated_by,created_at,updated_at,row_version)
SELECT gen_random_uuid(),x.id,p.id,'Production',date_trunc('week',current_date)::date+(g%12)*7,date_trunc('week',current_date)::date+(g%12)*7+4,
5,'Confirmed',sup.id,now(),format('Synthetic planning scale allocation %s',g),sup.id,sup.id,now(),now(),1
FROM generate_series(1,50) g JOIN planning_scope x ON x.n=g%12 CROSS JOIN planning_scope sup CROSS JOIN project p
WHERE sup.n=0 AND p.project_number='SYN-101' AND NOT EXISTS(SELECT 1 FROM resource_allocation a WHERE a.over_capacity_reason=format('Synthetic planning scale allocation %s',g));
COMMIT;
ANALYZE hub.planning_entry;
SELECT (SELECT count(*) FROM planning_scope) scoped_people,
(SELECT count(*) FROM planning_entry WHERE label LIKE 'Synthetic planning scale entry %') entries,
(SELECT count(*) FROM resource_allocation WHERE over_capacity_reason LIKE 'Synthetic planning scale allocation %') allocations;
\else
-- Full-scale synthetic data for the §22 performance check (packet 011 FR-013, US5):
-- 480 more people, 600 projects (500 Active, 100 Setup), about 100,000 open tasks (one 5,000-task and one 2,000-task
-- project), 6,000 deliverables, 3,000 milestones, about 17,000 dependencies and 2,000,000 activity entries over a year.
-- Synthetic only: never load into, or copy from, a real environment (§21, FR-007).
--
-- Run against an empty database the API has migrated and seeded once (reference data and development users):
--   Use a disposable synthetic database; never an operational database.
-- Packet 034 only (12 scoped people, 500 entries, 50 allocations), on the Tuesday preview:
--   docker exec -i pm-tuesday-preview-db psql -U hub -d hub -v planning_only=true -v ON_ERROR_STOP=1 < tools/scale/seed.sql
SET search_path = hub;
SET synchronous_commit = off;
BEGIN;

CREATE TEMP TABLE one AS SELECT (SELECT id FROM office ORDER BY name LIMIT 1) AS office, (SELECT id FROM client ORDER BY name LIMIT 1) AS client,
  (SELECT id FROM deliverable_type ORDER BY name LIMIT 1) AS dtype, (SELECT id FROM app_user WHERE email = 'priya@hub.test') AS priya,
  (SELECT id FROM app_user WHERE email = 'alex@hub.test') AS alex;

-- People: 60 project managers (0..59) and 420 engineers (60..479).
INSERT INTO app_user (id, email, display_name, job_title, office_id, is_active, is_template_editor, weekly_capacity_hours, created_at, updated_at, row_version)
SELECT gen_random_uuid(), format('scale%s@hub.test', g), format('Scale Person %s', g), CASE WHEN g <= 60 THEN 'Project Manager' ELSE 'Engineer' END,
  one.office, true, false, 37.5, now(), now(), 1
FROM generate_series(1, 480) g, one;
CREATE TEMP TABLE people AS SELECT id, (regexp_replace(email, '\D', '', 'g')::int - 1) AS n FROM app_user WHERE email LIKE 'scale%';
CREATE UNIQUE INDEX ON people (n);

-- Projects; Priya manages the two large ones.
CREATE TEMP TABLE sp AS SELECT g AS n, gen_random_uuid() AS id, format('S%s', lpad(g::text, 4, '0')) AS number,
  CASE WHEN g = 1 THEN 5000 WHEN g = 2 THEN 2000 WHEN g <= 500 THEN 230 ELSE 20 END AS tasks
FROM generate_series(1, 600) g;
CREATE UNIQUE INDEX ON sp (id);
INSERT INTO project (id, project_number, name, client_id, project_manager_id, office_id, status, priority, visibility, allow_viewer_comments,
  start_date, target_completion_date, activated_at, next_task_seq, next_deliverable_seq, next_milestone_seq, next_decision_seq, next_risk_seq,
  next_issue_seq, next_action_seq, created_at, updated_at, row_version)
SELECT sp.id, sp.number, format('Scale project %s', sp.n), one.client, CASE WHEN sp.n <= 2 THEN one.priya ELSE pm.id END, one.office,
  CASE WHEN sp.n <= 500 THEN 'Active' ELSE 'Setup' END, 'Medium', 'Open', true, current_date - 120, current_date + 240,
  CASE WHEN sp.n <= 500 THEN now() END, sp.tasks + 1, 11, 6, 1, 1, 1, 1, now() - interval '120 days', now(), 1
FROM sp JOIN people pm ON pm.n = sp.n % 60, one;

-- Three disciplines each, with leads among the engineers.
CREATE TEMP TABLE disc AS SELECT id, (row_number() OVER (ORDER BY name) - 1)::int AS k FROM discipline WHERE is_active ORDER BY name LIMIT 3;
CREATE TEMP TABLE spd AS SELECT gen_random_uuid() AS id, sp.id AS project_id, sp.n, disc.id AS discipline_id, disc.k, lead.id AS lead
FROM sp CROSS JOIN disc JOIN people lead ON lead.n = 60 + (sp.n * 11 + disc.k) % 420;
CREATE INDEX ON spd (project_id, k);
INSERT INTO project_discipline (id, project_id, discipline_id, lead_user_id, sort_order, is_active)
SELECT id, project_id, discipline_id, lead, k, true FROM spd;

-- Members: the PM, the three leads and eight engineers (slots 3..10); Alex joins projects 3..22.
CREATE TEMP TABLE sm AS
SELECT p.id AS project_id, p.project_manager_id AS user_id, '{PM}'::text[] AS roles, NULL::uuid AS pd FROM project p JOIN sp ON sp.id = p.id
UNION ALL SELECT spd.project_id, spd.lead, '{TeamMember}', spd.id FROM spd
UNION ALL SELECT sp.id, u.id, '{TeamMember}', spd.id FROM sp CROSS JOIN generate_series(3, 10) slot
  JOIN people u ON u.n = 60 + (sp.n * 11 + slot) % 420 JOIN spd ON spd.project_id = sp.id AND spd.k = slot % 3
UNION ALL SELECT sp.id, one.alex, '{TeamMember}', spd.id FROM sp JOIN spd ON spd.project_id = sp.id AND spd.k = 0, one WHERE sp.n BETWEEN 3 AND 22;
INSERT INTO project_member (id, project_id, user_id, roles, primary_discipline_id, added_at)
SELECT DISTINCT ON (project_id, user_id) gen_random_uuid(), project_id, user_id, roles, pd, now() - interval '120 days' FROM sm ORDER BY project_id, user_id, roles DESC;

-- Milestones (5) and deliverables (10) per project.
INSERT INTO milestone (id, name, milestone_type, date, original_date, is_client_facing, is_complete, is_cancelled, sort_order, created_at, updated_at, row_version, project_id, key, seq)
SELECT gen_random_uuid(), format('Milestone %s', m), (ARRAY['Kickoff','Field Work','Design Submission','Permit Submission','Tender'])[m],
  current_date - 60 + m * 45, current_date - 60 + m * 45, m >= 3, m = 1, false, m, now(), now(), 1, sp.id, format('%s-M%s', sp.number, lpad(m::text, 2, '0')), m
FROM sp CROSS JOIN generate_series(1, 5) m;
INSERT INTO deliverable (id, name, project_discipline_id, deliverable_type_id, owner_id, milestone_id, start_date, due_date, original_due_date, priority, status,
  requires_review, last_activity_at, sort_order, created_at, updated_at, row_version, project_id, key, seq)
SELECT gen_random_uuid(), format('Deliverable %s', d), spd.id, one.dtype, spd.lead, ms.id, current_date - 30, current_date + d * 12, current_date + d * 12, 'Medium',
  CASE WHEN d <= 2 THEN 'In Progress' ELSE 'Not Started' END, false, now(), d, now(), now(), 1, sp.id, format('%s-D%s', sp.number, lpad(d::text, 3, '0')), d
FROM sp CROSS JOIN generate_series(1, 10) d JOIN spd ON spd.project_id = sp.id AND spd.k = d % 3
  JOIN milestone ms ON ms.project_id = sp.id AND ms.seq = 1 + (d - 1) / 2, one;

-- Tasks: a fifth complete; the rest Not Started, In Progress or Ready for Review; some unassigned, some without a due date.
CREATE TEMP TABLE st AS
SELECT gen_random_uuid() AS id, sp.id AS project_id, sp.n AS pn, sp.number, g.s AS seq, spd.id AS pd, spd.lead,
  CASE WHEN g.s % 23 = 0 THEN NULL WHEN sp.n BETWEEN 3 AND 22 AND g.s % 37 = 0 THEN one.alex ELSE u.id END AS assignee,
  CASE WHEN g.s % 5 = 0 THEN 'Complete' WHEN g.s % 10 = 4 THEN 'Ready for Review' WHEN g.s % 5 IN (1, 4) THEN 'In Progress' ELSE 'Not Started' END AS status,
  current_date - 60 + (g.s % 90) AS start_date
FROM sp CROSS JOIN LATERAL generate_series(1, sp.tasks) AS g(s)
  JOIN spd ON spd.project_id = sp.id AND spd.k = g.s % 3
  JOIN people u ON u.n = 60 + (sp.n * 11 + 3 + g.s % 8) % 420, one;
INSERT INTO task (id, name, project_discipline_id, deliverable_id, assignee_id, reviewer_id, requires_review, priority, start_date, due_date, original_start_date,
  original_due_date, status, progress_pct, estimated_hours, review_round, due_date_change_count, last_activity_at, completed_at, status_changed_at, review_requested_at,
  sort_order, created_at, updated_at, row_version, project_id, key, seq)
SELECT st.id, format('Scale task %s', st.seq), st.pd, CASE WHEN st.seq % 10 < 6 THEN dl.id END, st.assignee, CASE WHEN st.seq % 10 = 4 THEN st.lead END,
  st.seq % 10 = 4, CASE st.seq % 13 WHEN 0 THEN 'High' WHEN 1 THEN 'Critical' ELSE 'Medium' END,
  st.start_date, CASE WHEN st.seq % 29 = 0 THEN NULL ELSE st.start_date + 5 + st.seq % 20 END, st.start_date, CASE WHEN st.seq % 29 = 0 THEN NULL ELSE st.start_date + 5 + st.seq % 20 END,
  st.status, CASE st.status WHEN 'Complete' THEN 100 WHEN 'Not Started' THEN 0 ELSE 50 END, 4 + st.seq % 12, 0, 0,
  now() - make_interval(days => st.seq % 30), CASE WHEN st.status = 'Complete' THEN now() - make_interval(days => st.seq % 30) END,
  now() - make_interval(days => st.seq % 30), CASE WHEN st.status = 'Ready for Review' THEN now() - interval '1 day' END,
  st.seq, now() - interval '100 days', now(), 1, st.project_id, format('%s-T%s', st.number, lpad(st.seq::text, 4, '0')), st.seq
FROM st LEFT JOIN deliverable dl ON dl.project_id = st.project_id AND dl.seq = 1 + (st.seq / 23) % 10;

-- Dependencies: every seventh task follows the one before it.
INSERT INTO task_dependency (id, project_id, predecessor_task_id, successor_task_id, dependency_type, lag_days, created_at)
SELECT gen_random_uuid(), b.project_id, a.id, b.id, 'FinishToStart', 0, now() FROM st b JOIN st a ON a.project_id = b.project_id AND a.seq = b.seq - 1 WHERE b.seq % 7 = 0;

COMMIT;

-- Activity: 2,000,000 entries spread over the past year (in batches to keep memory flat).
DO $$
DECLARE i int;
BEGIN
  CREATE TEMP TABLE tk AS SELECT row_number() OVER () AS r, id, project_id, project_discipline_id, key, name, assignee_id FROM task;
  CREATE UNIQUE INDEX ON tk (r);
  FOR i IN 0..19 LOOP
    INSERT INTO activity_log (id, occurred_at, actor_user_id, actor_type, project_id, project_discipline_id, item_type, item_id, item_key, item_name, action, categories, changes, source)
    SELECT gen_random_uuid(), now() - make_interval(secs => (random() * 365 * 86400)::int), tk.assignee_id, 'User', tk.project_id, tk.project_discipline_id, 'Task', tk.id,
      tk.key, tk.name, 'StatusChanged', '{status}', '[{"field":"Status","old":"Not Started","new":"In Progress"}]'::jsonb, 'UI'
    FROM generate_series(1, 100000) g JOIN tk ON tk.r = 1 + ((g + i * 100000)::bigint * 7919) % (SELECT count(*) FROM tk);
  END LOOP;
END $$;

ANALYZE;
SELECT (SELECT count(*) FROM project) AS projects, (SELECT count(*) FROM task WHERE status NOT IN ('Complete', 'Cancelled')) AS open_tasks,
  (SELECT count(*) FROM task) AS tasks, (SELECT count(*) FROM activity_log) AS activity, (SELECT count(*) FROM app_user) AS people;

\endif

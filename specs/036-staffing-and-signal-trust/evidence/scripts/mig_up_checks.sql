select 'task started, started_at null', count(*) from hub.task where status not in ('Not Started','On Hold','Cancelled') and started_at is null
union all select 'task On Hold/Cancelled, started_at null (allowed only if never started)', count(*) from hub.task where status in ('On Hold','Cancelled') and started_at is null
union all select 'task On Hold/Cancelled, null, but a logged start exists', count(*) from hub.task t where status in ('On Hold','Cancelled') and started_at is null and exists (select 1 from hub.activity_log a where a.item_type='Task' and a.item_id=t.id and jsonb_path_exists(a.changes, '$[*] ? (@.field == "Status" && @.old == "Not Started" && @.new != "On Hold" && @.new != "Cancelled")'))
union all select 'task Not Started, started_at set', count(*) from hub.task where status = 'Not Started' and started_at is not null
union all select 'task started_at set', count(*) from hub.task where started_at is not null
union all select 'deliverable started, started_at null', count(*) from hub.deliverable where status not in ('Not Started','On Hold','Cancelled') and started_at is null
union all select 'deliverable On Hold/Cancelled, started_at null', count(*) from hub.deliverable where status in ('On Hold','Cancelled') and started_at is null
union all select 'deliverable Not Started, started_at set', count(*) from hub.deliverable where status = 'Not Started' and started_at is not null
union all select 'deliverable started_at set', count(*) from hub.deliverable where started_at is not null
union all select 'task links', count(*) from hub.task_dependency
union all select 'task links not FinishToStart', count(*) from hub.task_dependency where dependency_type <> 'FinishToStart'
union all select 'deliverable links', count(*) from hub.deliverable_dependency
union all select 'deliverable links not FinishToStart', count(*) from hub.deliverable_dependency where dependency_type <> 'FinishToStart';

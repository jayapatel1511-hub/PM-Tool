select table_name, (xpath('/row/c/text()', query_to_xml(format('select count(*) as c from hub.%I', table_name), false, true, '')))[1]::text::int
from information_schema.tables where table_schema='hub' and table_type='BASE TABLE' order by 1;

-- psql input supplied privately by activation: app_password never goes in argv or output.
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL log_statement = 'none';
SET LOCAL log_min_error_statement = 'panic';
DO $$
BEGIN
  IF current_database() <> 'hub_pilot' OR current_user <> 'hub_pilot' THEN
    RAISE EXCEPTION 'Wrong pilot database or migration administrator';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'hub_pilot_app') THEN
    CREATE ROLE hub_pilot_app;
  END IF;
  IF EXISTS (SELECT 1 FROM pg_auth_members WHERE member = 'hub_pilot_app'::regrole)
     OR EXISTS (SELECT 1 FROM pg_shdepend WHERE refclassid = 'pg_authid'::regclass
                AND refobjid = 'hub_pilot_app'::regrole AND deptype = 'o') THEN
    RAISE EXCEPTION 'Runtime role has memberships or owns objects; operator review required';
  END IF;
  IF EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
              WHERE n.nspname = 'hub' AND c.relowner <> 'hub_pilot'::regrole)
     OR (SELECT nspowner <> 'hub_pilot'::regrole FROM pg_namespace WHERE nspname = 'hub') THEN
    RAISE EXCEPTION 'Unexpected application object owner; preserving ownership';
  END IF;
END $$;
ALTER ROLE hub_pilot_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE
  NOREPLICATION NOBYPASSRLS NOINHERIT PASSWORD :'app_password';
DO $$
BEGIN
  EXECUTE format('REVOKE CREATE, TEMPORARY ON DATABASE %I FROM PUBLIC, hub_pilot_app', current_database());
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO hub_pilot_app', current_database());
END $$;
REVOKE ALL ON SCHEMA hub, public FROM PUBLIC, hub_pilot_app;
GRANT USAGE ON SCHEMA hub, public TO hub_pilot_app;
REVOKE ALL ON ALL TABLES IN SCHEMA hub FROM PUBLIC, hub_pilot_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA hub TO hub_pilot_app;
REVOKE ALL ON hub.activity_log, hub.__ef_migrations FROM PUBLIC, hub_pilot_app;
-- Table revocation alone does not remove pre-existing column grants.
DO $$
DECLARE t text; cols text;
BEGIN
  FOREACH t IN ARRAY ARRAY['activity_log', '__ef_migrations'] LOOP
    SELECT string_agg(quote_ident(attname), ',') INTO cols FROM pg_attribute
      WHERE attrelid = format('hub.%I', t)::regclass AND attnum > 0 AND NOT attisdropped;
    EXECUTE format('REVOKE ALL (%s) ON hub.%I FROM PUBLIC, hub_pilot_app', cols, t);
  END LOOP;
END $$;
GRANT SELECT, INSERT ON hub.activity_log TO hub_pilot_app;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA hub FROM PUBLIC, hub_pilot_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA hub TO hub_pilot_app;
-- No Hub function is directly called by runtime; immutable triggers continue to execute.
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA hub FROM PUBLIC, hub_pilot_app;
-- Clear both global and schema defaults: schema revocation cannot cancel a global grant.
ALTER DEFAULT PRIVILEGES FOR ROLE hub_pilot REVOKE ALL ON TABLES FROM PUBLIC, hub_pilot_app;
ALTER DEFAULT PRIVILEGES FOR ROLE hub_pilot IN SCHEMA hub REVOKE ALL ON TABLES FROM PUBLIC, hub_pilot_app;
-- New tables start SELECT/INSERT only. Activation adds CRUD to ordinary tables after migration,
-- so even a future recreated activity_log never receives UPDATE/DELETE through defaults.
ALTER DEFAULT PRIVILEGES FOR ROLE hub_pilot IN SCHEMA hub GRANT SELECT, INSERT ON TABLES TO hub_pilot_app;
ALTER DEFAULT PRIVILEGES FOR ROLE hub_pilot REVOKE ALL ON SEQUENCES FROM PUBLIC, hub_pilot_app;
ALTER DEFAULT PRIVILEGES FOR ROLE hub_pilot IN SCHEMA hub REVOKE ALL ON SEQUENCES FROM PUBLIC, hub_pilot_app;
ALTER DEFAULT PRIVILEGES FOR ROLE hub_pilot IN SCHEMA hub GRANT USAGE, SELECT ON SEQUENCES TO hub_pilot_app;
ALTER DEFAULT PRIVILEGES FOR ROLE hub_pilot REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC, hub_pilot_app;
ALTER DEFAULT PRIVILEGES FOR ROLE hub_pilot IN SCHEMA hub REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC, hub_pilot_app;
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_namespace WHERE has_schema_privilege('hub_pilot_app', oid, 'CREATE'))
     OR has_database_privilege('hub_pilot_app', current_database(), 'CREATE')
     OR has_database_privilege('hub_pilot_app', current_database(), 'TEMPORARY')
     OR has_table_privilege('hub_pilot_app', 'hub.activity_log', 'UPDATE,DELETE,TRUNCATE')
     OR has_any_column_privilege('hub_pilot_app', 'hub.activity_log', 'UPDATE')
     OR has_table_privilege('hub_pilot_app', 'hub.__ef_migrations', 'INSERT,UPDATE,DELETE,TRUNCATE')
     OR EXISTS (SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
                 WHERE n.nspname IN ('hub', 'public') AND p.prosecdef
                   AND has_function_privilege('hub_pilot_app', p.oid, 'EXECUTE')) THEN
    RAISE EXCEPTION 'Runtime permissions are not separated';
  END IF;
END $$;
COMMIT;
\unset app_password

-- Provisions the least-privilege runtime role of the API. Safe to repeat.
--
-- Runs on the first start of the Compose database, before migrations: the default
-- privileges then cover the tables this administrator creates later. Run it again as
-- the migration administrator after migrating wherever that did not happen or another
-- administrator created the tables: the explicit grants cover what already exists.
--
-- No password is stored here. 01-set-app-role-password.sh sets it on first start;
-- elsewhere use psql \password. Re-running preserves it.
BEGIN;

DO $provision$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'commercecore_app') THEN
        CREATE ROLE commercecore_app LOGIN
            NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT
            NOREPLICATION NOBYPASSRLS;
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_roles
        WHERE rolname = 'commercecore_app'
          AND (NOT rolcanlogin OR rolsuper OR rolcreatedb OR rolcreaterole
               OR rolinherit OR rolreplication OR rolbypassrls)
    ) OR EXISTS (
        SELECT 1 FROM pg_auth_members
        WHERE member = 'commercecore_app'::regrole
    ) THEN
        RAISE EXCEPTION 'commercecore_app must be a dedicated restricted role without memberships';
    END IF;

    EXECUTE format('GRANT CONNECT ON DATABASE %I TO commercecore_app', current_database());
END
$provision$;

CREATE SCHEMA IF NOT EXISTS catalog;
CREATE SCHEMA IF NOT EXISTS outbox;
CREATE SCHEMA IF NOT EXISTS platform;

GRANT USAGE ON SCHEMA catalog, outbox, platform TO commercecore_app;

-- What already exists, whichever administrator created it.
GRANT SELECT, INSERT, UPDATE, DELETE
    ON ALL TABLES IN SCHEMA catalog, outbox TO commercecore_app;
GRANT SELECT ON ALL TABLES IN SCHEMA platform TO commercecore_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA catalog, outbox TO commercecore_app;

-- What this administrator creates later.
ALTER DEFAULT PRIVILEGES IN SCHEMA catalog, outbox
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO commercecore_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA platform
    GRANT SELECT ON TABLES TO commercecore_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA catalog, outbox
    GRANT USAGE, SELECT ON SEQUENCES TO commercecore_app;

COMMIT;

-- Run as the migration administrator AFTER applying migrations, not as an init script.
-- No passwords are stored here. Set the password interactively with psql \password.
-- Re-running preserves the password. This script does not revoke pre-existing ACLs.
BEGIN;

DO $provision$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'commercecore_outbox') THEN
        CREATE ROLE commercecore_outbox LOGIN
            NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT
            NOREPLICATION NOBYPASSRLS;
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_roles
        WHERE rolname = 'commercecore_outbox'
          AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolinherit
               OR rolreplication OR rolbypassrls)
    ) OR EXISTS (
        SELECT 1 FROM pg_auth_members
        WHERE member = 'commercecore_outbox'::regrole
    ) THEN
        RAISE EXCEPTION 'commercecore_outbox must be a dedicated restricted role without memberships';
    END IF;

    EXECUTE format('GRANT CONNECT ON DATABASE %I TO commercecore_outbox', current_database());
END
$provision$;

GRANT USAGE ON SCHEMA platform, outbox TO commercecore_outbox;
GRANT SELECT ON TABLE platform.tenants TO commercecore_outbox;
GRANT SELECT, UPDATE ON TABLE outbox.messages TO commercecore_outbox;

-- No catalog access, platform writes, INSERT/DELETE, ownership or RLS bypass.
-- No default privileges: future tables require explicit review.
COMMIT;

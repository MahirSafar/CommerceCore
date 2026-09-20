# Outbox worker container

Run the following commands from the repository root. This is a local deployment
recipe, not a production credentials setup. Do not delete the PostgreSQL volume.

## Build and start safely

```powershell
docker compose --profile workers config --quiet
docker compose --profile workers build outbox-worker
docker compose --profile workers up -d outbox-worker
docker compose logs --tail 50 outbox-worker
```

The `workers` profile keeps the service out of a normal `docker compose up`.
The image runs as the non-root `app` user with a read-only filesystem and writable
temporary directory. No host ports are exposed. Credentials are supplied at runtime,
not copied into the image. Production mode does not load development user secrets.

**Dispatching remains hard-coded to `false` in Compose.** The topology verifier
still connects to RabbitMQ and passively verifies that the durable topic exchange exists.
The exchange is created beforehand by an administrator; the worker only verifies its
existence. A running container therefore does not mean messages are being delivered.
The database connection may remain empty for this topology-only check.

The worker uses a dedicated publisher account (`commercecore_publisher` via
`RABBITMQ_PUBLISHER_USER` and `RABBITMQ_PUBLISHER_PASSWORD`) rather than the
bootstrap/admin RabbitMQ credentials. Before production, ensure broker identity
permissions are scoped to the exchange and event routing keys, configure TLS and
supply credentials through the deployment's secret store. PostgreSQL least privilege
does not restrict RabbitMQ permissions.

## Provision the worker database role after migrations

Apply all migrations with the existing migration administrator first. The worker
must never run migrations or use administrator credentials. Then execute the
script against the intended database. These commands use the existing local
database and administrator name `commercecore`; adjust those two arguments if
your local configuration differs.

```powershell
Get-Content -Raw docker/postgres/provision-outbox-role.sql |
    docker exec -i commercecore-postgres psql -X -U commercecore -d commercecore -v ON_ERROR_STOP=1

docker exec -it commercecore-postgres psql -X -U commercecore -d commercecore -c '\password commercecore_outbox'
```

The second command prompts for a new, unique worker password without placing it
in shell history. Do not reuse an administrator/API password or paste it into chat.
The script can be re-run without resetting credentials. It rejects privileged
roles and role memberships; it does not erase pre-existing ACLs. Use this dedicated
role only for the worker. Do not grant it ownership or extra privileges.

The only application-table grants are:

- `platform.tenants`: SELECT.
- `outbox.messages`: SELECT and UPDATE, still subject to tenant RLS.

There are no catalog grants, platform writes or grants on future tables. Existing
database-wide PUBLIC privileges remain unchanged. The script is deliberately
outside `docker-entrypoint-initdb.d`: migrations must create the tables first, and
an existing database must be provisioned explicitly as well.

For local use, set `COMMERCECORE_OUTBOX_CONNECTION_STRING` in the ignored `.env`
file to a real Npgsql connection string with host `postgres`, port `5432`, database
`commercecore`, username `commercecore_outbox` and the password you just set.
Use Npgsql connection-string quoting for values containing semicolons or quotes;
wrap the entire `.env` value in single quotes to avoid Compose dollar interpolation.
A generated alphanumeric password avoids both quoting concerns. Never commit `.env`
or run `docker compose config` without `--quiet` when sharing output containing secrets.

## Activation gate

Do not change `RabbitMq__DispatcherEnabled` until all of these are ready:

1. Migrations and restricted role provisioning have succeeded on the target DB.
2. A durable consumer queue is bound to the required event routing keys.
3. The consumer deduplicates by message ID; delivery is at least once.
4. Retry/dead-letter monitoring and recovery procedures are defined.

An unbound mandatory publish is retried and can eventually dead-letter an outbox
row. Merely creating the exchange is not sufficient. Activation is a separate,
reviewed Compose change. To stop this worker without affecting the database:

```powershell
docker compose stop outbox-worker
```

References: [Compose profiles](https://docs.docker.com/compose/how-tos/profiles/)
and [PostgreSQL object privileges](https://www.postgresql.org/docs/current/sql-grant.html).

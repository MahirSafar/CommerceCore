#!/usr/bin/env bash
set -euo pipefail

: "${COMMERCECORE_APP_PASSWORD:?COMMERCECORE_APP_PASSWORD must be set}"

# 00-provision-app-role.sql has created the role without a password.
psql \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --set ON_ERROR_STOP=1 \
  --set app_password="$COMMERCECORE_APP_PASSWORD" <<'SQL'
ALTER ROLE commercecore_app PASSWORD :'app_password';
SQL

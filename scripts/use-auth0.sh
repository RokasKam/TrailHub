#!/usr/bin/env bash
# Switches backend and app from development auth to Auth0.
#   ./scripts/use-auth0.sh <domain> <native-client-id> <api-audience>
#   ./scripts/use-auth0.sh trailhub.eu.auth0.com AbC123... https://api.trailhub.app
# Writes backend/.env and mobile/.env.local (both gitignored). None of these values are secrets.
set -euo pipefail
cd "$(dirname "$0")/.."

if [[ $# -ne 3 ]]; then
  sed -n '2,5p' "$0" | sed 's/^# \{0,1\}//'
  exit 1
fi

domain=${1#https://}; domain=${domain%/}
client_id=$2
audience=$3

if ! curl -fsS "https://$domain/.well-known/openid-configuration" >/dev/null; then
  echo "Could not reach https://$domain/.well-known/openid-configuration. Check the domain." >&2
  exit 1
fi

# Keep any existing non-auth settings (e.g. Postgres password) in backend/.env.
touch backend/.env
grep -vE '^(AUTH_MODE|AUTH0_DOMAIN|AUTH0_AUDIENCE)=' backend/.env > backend/.env.tmp || true
{
  cat backend/.env.tmp
  echo "AUTH_MODE=Auth0"
  echo "AUTH0_DOMAIN=$domain"
  echo "AUTH0_AUDIENCE=$audience"
} > backend/.env
rm backend/.env.tmp
grep -q '^ASPNETCORE_ENVIRONMENT=' backend/.env || echo "ASPNETCORE_ENVIRONMENT=Development" >> backend/.env

touch mobile/.env.local
grep -vE '^EXPO_PUBLIC_(AUTH_MODE|AUTH0_DOMAIN|AUTH0_CLIENT_ID|AUTH0_AUDIENCE)=' mobile/.env.local > mobile/.env.local.tmp || true
{
  cat mobile/.env.local.tmp
  echo "EXPO_PUBLIC_AUTH_MODE=auth0"
  echo "EXPO_PUBLIC_AUTH0_DOMAIN=$domain"
  echo "EXPO_PUBLIC_AUTH0_CLIENT_ID=$client_id"
  echo "EXPO_PUBLIC_AUTH0_AUDIENCE=$audience"
} > mobile/.env.local
rm mobile/.env.local.tmp
grep -q '^EXPO_PUBLIC_API_URL=' mobile/.env.local || echo "EXPO_PUBLIC_API_URL=http://localhost:8080" >> mobile/.env.local

echo "✓ backend/.env and mobile/.env.local now use Auth0 ($domain)."
echo "Next: cd backend && docker compose up -d   (restarts services in Auth0 mode)"
echo "      cd mobile && npx expo run:ios         (rebuild: the domain is baked into the native callback config)"

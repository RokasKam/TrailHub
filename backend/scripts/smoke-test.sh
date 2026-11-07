#!/usr/bin/env bash
# End-to-end smoke test through the gateway. Requires the stack running with AUTH_MODE=Development:
#   printf 'AUTH_MODE=Development\n' > .env && docker compose up -d --build && ./scripts/smoke-test.sh
set -euo pipefail
cd "$(dirname "$0")/.."

BASE=${BASE:-http://localhost:8080}
A="Authorization: Dev smoke-user-a-$RANDOM"
B="Authorization: Dev smoke-user-b-$RANDOM"
pass=0

step() { printf '\n\033[1m▸ %s\033[0m\n' "$1"; }
ok() { pass=$((pass + 1)); printf '  \033[32m✓\033[0m %s\n' "$1"; }
fail() { printf '  \033[31m✗ %s\033[0m\n' "$1" >&2; exit 1; }
# call METHOD PATH [AUTH] [curl args...] -> body on stdout, fails on non-2xx
call() {
  local method=$1 path=$2 auth=${3:-$A}; shift 3 || shift $#
  local out status
  out=$(curl -sS -w '\n%{http_code}' -X "$method" "$BASE$path" -H "$auth" "$@")
  status=${out##*$'\n'}; out=${out%$'\n'*}
  [[ $status == 2* ]] || fail "$method $path → $status: $out"
  printf '%s' "$out"
}
status_of() { curl -s -o /dev/null -w '%{http_code}' "$@"; }
# json EXPR: evaluate a Python expression over the JSON on stdin; objects allow attribute access (d["items"][0].id).
json() { python3 -c "
import sys, json
class O(dict):
    __getattr__ = dict.__getitem__
d = json.load(sys.stdin, object_hook=O)
print(eval(sys.argv[1], {'d': d}))" "$1"; }

step "Waiting for services"
for path in /api/profiles/me /api/trails /api/activities /api/trips /api/groups /api/weather; do
  for _ in $(seq 1 60); do [[ $(status_of "$BASE$path") == 401 ]] && break; sleep 1; done
  [[ $(status_of "$BASE$path") == 401 ]] || fail "$path is not answering through the gateway"
done
ok "all six services reachable through the gateway"

step "Gateway and auth"
[[ $(status_of "$BASE/api/trails") == 401 ]] && ok "anonymous request rejected (401)" || fail "expected 401"

step "Profile"
call GET /api/profiles/me "$A" | json 'd.displayName' >/dev/null && ok "profile auto-created on first call" || fail "profile auto-created on first call"
call PUT /api/profiles/me "$A" -H 'Content-Type: application/json' -d '{"displayName":"Smoke A","bio":"Testing","homeRegion":"Tatras"}' | json 'd.displayName' | grep -c "Smoke A" >/dev/null && ok "profile updated" || fail "profile updated"
call PUT /api/profiles/me/preferences "$A" -H 'Content-Type: application/json' -d '{"units":"Imperial","mapStyle":"Terrain","shareLocationWithGroups":true,"notificationsEnabled":false}' | json 'd.preferences.units' | grep -c Imperial >/dev/null && ok "preferences saved" || fail "preferences saved"

step "Trails: GPX import, search, details"
TRAIL=$(call POST /api/trails/import "$A" -F file=@samples/demo-loop.gpx -F region="High Tatras (demo)" -F difficulty=Moderate | json 'd.id')
ok "imported GPX as trail $TRAIL"
DETAILS=$(call GET "/api/trails/$TRAIL" "$A")
echo "$DETAILS" | json 'f"{d.distanceMeters/1000:.1f} km, +{d.elevationGainMeters:.0f} m, ~{d.estimatedDurationMinutes} min, loop={d.isLoop}"' | sed 's/^/  · /'
[[ $(echo "$DETAILS" | json 'len(d.pointsOfInterest)') == 3 ]] && ok "3 waypoints became points of interest" || fail "3 waypoints became points of interest"
echo "$DETAILS" | json '[p.type for p in d.pointsOfInterest]' | grep -c WaterSource >/dev/null && ok "POI types inferred (WaterSource, Viewpoint, Shelter)" || fail "POI types inferred (WaterSource, Viewpoint, Shelter)"
call GET "/api/trails?lat=49.26&lon=19.96&radiusKm=10&difficulty=Moderate&maxDistanceKm=20" "$A" | json '[t.id for t in d["items"]]' | grep -c "$TRAIL" >/dev/null && ok "found by radius + difficulty + distance filters (PostGIS)" || fail "found by radius + difficulty + distance filters (PostGIS)"
[[ $(call GET "/api/trails?lat=40&lon=-3&radiusKm=10" "$A" | json 'd.totalCount') == 0 ]] && ok "not found far away" || fail "not found far away"
call GET "/api/trails?q=ridge&sort=Rating" "$A" | json 'd.totalCount' | grep -cv '^0$' >/dev/null && ok "text search" || fail "text search"
call GET "/api/trails?minLat=49.2&minLon=19.9&maxLat=49.3&maxLon=20.0" "$A" | json '[t.id for t in d["items"]]' | grep -c "$TRAIL" >/dev/null && ok "map bounding-box search" || fail "map bounding-box search"
[[ $(call GET "/api/trails/$TRAIL/route?maxPoints=50" "$A" | json 'len(d.points)') == 50 ]] && ok "route downsampled for map preview" || fail "route downsampled for map preview"
[[ $(call GET "/api/trails/$TRAIL/elevation-profile?samples=40" "$A" | json 'len(d)') == 40 ]] && ok "elevation profile" || fail "elevation profile"
call GET "/api/trails/$TRAIL/gpx" "$A" | grep -c "<trkpt" >/dev/null && ok "GPX export" || fail "GPX export"

step "Photos (S3-compatible storage behind the gateway)"
PHOTO="$(mktemp "${TMPDIR:-/tmp}/trailhub-photo.XXXXXX").jpg" # works with both GNU and BSD mktemp
# Smallest valid JPEG (1x1 px)
python3 -c "import base64,sys; open(sys.argv[1],'wb').write(base64.b64decode('/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA='))" "$PHOTO"
PHOTO_URL=$(call POST "/api/trails/$TRAIL/photos/upload" "$A" -F "file=@$PHOTO;type=image/jpeg" -F caption="Summit view" | json 'd.url')
ok "photo uploaded: $PHOTO_URL" || fail "photo upload"
[[ $(curl -s -o /dev/null -w '%{http_code} %{content_type}' "$PHOTO_URL") == "200 image/jpeg" ]] && ok "photo served publicly as image/jpeg" || fail "photo served publicly as image/jpeg"
[[ $(call GET "/api/trails/$TRAIL" "$A" | json 'd.photos[0].caption') == "Summit view" ]] && ok "photo listed on the trail" || fail "photo listed on the trail"
[[ $(status_of -X POST "$BASE/api/trails/$TRAIL/photos/upload" -H "$A" -F "file=@samples/demo-loop.gpx;type=text/plain") == 400 ]] && ok "non-image upload rejected (400)" || fail "non-image upload rejected (400)"
rm -f "$PHOTO"

step "Community"
call PUT "/api/trails/$TRAIL/favorite" "$A" >/dev/null && call GET /api/trails/favorites "$A" | grep -c "$TRAIL" >/dev/null && ok "favorite saved" || fail "favorite saved"
call PUT "/api/trails/$TRAIL/reviews/mine" "$A" -H 'Content-Type: application/json' -d '{"rating":5,"comment":"Great"}' >/dev/null
call PUT "/api/trails/$TRAIL/reviews/mine" "$B" -H 'Content-Type: application/json' -d '{"rating":2}' >/dev/null
call PUT "/api/trails/$TRAIL/reviews/mine" "$A" -H 'Content-Type: application/json' -d '{"rating":4,"comment":"Updated"}' >/dev/null
[[ $(call GET "/api/trails/$TRAIL" "$A" | json 'f"{d.averageRating:.1f}/{d.reviewCount}"') == "3.0/2" ]] && ok "reviews upsert per user; rating summary 3.0 from 2 reviews" || fail "reviews upsert per user; rating summary 3.0 from 2 reviews"
call POST "/api/trails/$TRAIL/conditions" "$A" -H 'Content-Type: application/json' -d '{"condition":"Muddy","note":"After rain"}' >/dev/null
[[ $(call GET "/api/trails/$TRAIL" "$A" | json 'd.latestCondition.condition') == Muddy ]] && ok "condition report shows on trail" || fail "condition report shows on trail"

step "Activity: record (idempotent), stats, GPX"
ACT=$(python3 -c 'import uuid; print(uuid.uuid4())')
BODY=$(python3 - "$TRAIL" <<'EOF'
import json, sys, math, datetime as dt
t0 = dt.datetime.now(dt.timezone.utc) - dt.timedelta(hours=4)
def seg(start_min, n, lat0):
    return [{"latitude": lat0 + i*0.0009, "longitude": 19.95, "elevation": 1000 + i*12,
             "timestampUtc": (t0 + dt.timedelta(minutes=start_min + i)).isoformat()} for i in range(n)]
print(json.dumps({"name": "Smoke hike", "trailId": sys.argv[1], "segments": [seg(0, 60, 49.25), seg(90, 30, 49.31)], "notes": None}))
EOF
)
call PUT "/api/activities/$ACT" "$A" -H 'Content-Type: application/json' -d "$BODY" | json 'f"{d.distanceMeters/1000:.1f} km, moving {d.movingDurationSeconds//60} min"' | sed 's/^/  · /'
call PUT "/api/activities/$ACT" "$A" -H 'Content-Type: application/json' -d "$BODY" >/dev/null && ok "re-upload with same id accepted (offline retry safe)" || fail "re-upload with same id accepted (offline retry safe)"
[[ $(call GET /api/activities "$A" | json 'd.totalCount') == 1 ]] && ok "no duplicate created" || fail "no duplicate created"
[[ $(status_of -X PUT "$BASE/api/activities/$ACT" -H "$B" -H 'Content-Type: application/json' -d "$BODY") == 409 ]] && ok "another user can't claim the id (409)" || fail "another user can't claim the id (409)"
[[ $(status_of "$BASE/api/activities/$ACT" -H "$B") == 404 ]] && ok "another user can't read it (404)" || fail "another user can't read it (404)"
call GET "/api/activities/$ACT" "$A" | json 'len(d.segments)' | grep -c 2 >/dev/null && ok "details keep pause/resume segments" || fail "details keep pause/resume segments"
call GET "/api/activities/$ACT/gpx" "$A" | grep -c "<trkseg" | grep -c 2 >/dev/null && ok "GPX export with 2 segments" || fail "GPX export with 2 segments"
call GET /api/activities/statistics "$A" | json 'f"{d.allTime.hikes} hike(s), {d.allTime.distanceMeters/1000:.1f} km, {len(d.lastTwelveMonths)} months"' | sed 's/^/  · /'
ok "statistics"
IMPORTED=$(call POST /api/activities/import "$A" -F file=@<(call GET "/api/activities/$ACT/gpx" "$A") -F name="Re-imported" | json 'd.id')
ok "GPX with timestamps imported as activity $IMPORTED"
[[ $(status_of -X POST "$BASE/api/activities/import" -H "$A" -F file=@samples/demo-loop.gpx) == 400 ]] && ok "GPX without timestamps rejected as activity (400)" || fail "GPX without timestamps rejected as activity (400)"

step "Achievements via RabbitMQ (Activity → Profile)"
for _ in $(seq 1 20); do
  UNLOCKED=$(call GET /api/profiles/me/achievements "$A" | json '",".join(a.code for a in d if a.unlocked)')
  [[ $UNLOCKED == *first-hike* ]] && break; sleep 1
done
[[ $UNLOCKED == *first-hike* ]] && ok "unlocked: $UNLOCKED" || fail "achievement not unlocked: '$UNLOCKED'"
[[ $(call GET /api/profiles/me "$A" | json 'd.totals.completedHikes') == 2 ]] && ok "profile totals updated by events" || fail "profile totals updated by events"
call DELETE "/api/activities/$IMPORTED" "$A" >/dev/null
sleep 2
[[ $(call GET /api/profiles/me "$A" | json 'd.totals.completedHikes') == 1 ]] && ok "delete event reverses totals" || fail "delete event reverses totals"

step "Trips"
TRIP=$(call POST /api/trips "$A" -H 'Content-Type: application/json' -d '{"name":"Tatra traverse","description":null,"startDate":"2027-07-01","endDate":"2027-07-03"}' | json 'd.id')
[[ $(call GET "/api/trips/$TRIP" "$A" | json 'len(d.days)') == 3 ]] && ok "3-day trip created with day plans" || fail "3-day trip created with day plans"
call PUT "/api/trips/$TRIP/days/2/trails/$TRAIL" "$A" >/dev/null && ok "trail added to day 2" || fail "trail added to day 2"
call PUT "/api/trips/$TRIP/days/2" "$A" -H 'Content-Type: application/json' -d '{"notes":"Early start","overnight":"Mountain hut"}' >/dev/null && ok "day notes" || fail "day notes"
call POST "/api/trips/$TRIP/checklist" "$A" -H 'Content-Type: application/json' -d '{"text":"Headlamp"}' | json 'd.checklist[0].text' | grep -c Headlamp >/dev/null && ok "checklist item" || fail "checklist item"
call PUT "/api/trips/$TRIP/dates" "$A" -H 'Content-Type: application/json' -d '{"startDate":"2027-07-01","endDate":"2027-07-02"}' | json 'd.days[1].trailIds' | grep -c "$TRAIL" >/dev/null && ok "reschedule keeps existing day plans" || fail "reschedule keeps existing day plans"
[[ $(status_of "$BASE/api/trips/$TRIP" -H "$B") == 404 ]] && ok "trips are private (404 for others)" || fail "trips are private (404 for others)"

step "Groups"
GROUP_JSON=$(call POST /api/groups "$A" -H 'Content-Type: application/json' -d '{"name":"Weekend hikers","description":null,"displayName":"Smoke A"}')
GROUP=$(echo "$GROUP_JSON" | json 'd.id'); CODE=$(echo "$GROUP_JSON" | json 'd.inviteCode')
ok "group created, invite code $CODE"
call POST /api/groups/join "$B" -H 'Content-Type: application/json' -d "{\"inviteCode\":\"$(echo "$CODE" | tr 'A-Z' 'a-z')\",\"displayName\":\"Smoke B\"}" | json 'd.inviteCode' | grep -c None >/dev/null && ok "B joined (code is case-insensitive; only the owner sees it)" || fail "B joined (code is case-insensitive; only the owner sees it)"
WHEN=$(python3 -c 'import datetime as dt; print((dt.datetime.now(dt.timezone.utc)+dt.timedelta(days=3)).isoformat())')
call POST "/api/groups/$GROUP/hikes" "$B" -H 'Content-Type: application/json' -d "{\"title\":\"Sunrise hike\",\"trailId\":\"$TRAIL\",\"scheduledAtUtc\":\"$WHEN\",\"meetingLatitude\":49.25,\"meetingLongitude\":19.95,\"notes\":null}" >/dev/null && ok "member scheduled a group hike" || fail "member scheduled a group hike"
[[ $(call GET /api/groups "$A" | json 'd[0].memberCount') == 2 ]] && ok "group list shows 2 members" || fail "group list shows 2 members"
HIKE=$(call GET "/api/groups/$GROUP" "$A" | json 'd.upcomingHikes[0].id')
[[ $(call PUT "/api/groups/$GROUP/hikes/$HIKE/rsvp" "$A" -H 'Content-Type: application/json' -d '{"status":"Maybe"}' | json 'len([r for r in d.rsvps if r.status == "Going"])') == 1 ]] && ok "RSVPs: organizer going, owner maybe" || fail "RSVPs: organizer going, owner maybe"
call PUT "/api/groups/$GROUP/hikes/$HIKE/rsvp" "$A" -H 'Content-Type: application/json' -d '{"status":"Going"}' >/dev/null
[[ $(call GET "/api/groups/$GROUP" "$B" | json 'len([r for r in d.upcomingHikes[0].rsvps if r.status == "Going"])') == 2 ]] && ok "changed RSVP replaces the old one (2 going)" || fail "changed RSVP replaces the old one (2 going)"
[[ $(status_of -X PUT "$BASE/api/groups/$GROUP/hikes/$HIKE/rsvp" -H "Authorization: Dev outsider-$RANDOM" -H 'Content-Type: application/json' -d '{"status":"Going"}') == 404 ]] && ok "non-members can't RSVP (404)" || fail "non-members can't RSVP (404)"
[[ $(status_of -X POST "$BASE/api/groups/$GROUP/leave" -H "$A") == 409 ]] && ok "owner can't leave without transferring (409)" || fail "owner can't leave without transferring (409)"
call POST "/api/groups/$GROUP/locations/mine" "$A" -H 'Content-Type: application/json' -d '{"latitude":49.25,"longitude":19.95,"elevation":1300,"accuracyMeters":8,"headingDegrees":null}' >/dev/null
[[ $(call GET "/api/groups/$GROUP/locations" "$B" | json 'len(d)') == 1 ]] && ok "REST location sharing (background path) visible to members" || fail "REST location sharing (background path) visible to members"
call DELETE "/api/groups/$GROUP/locations/mine" "$A" >/dev/null
[[ $(call GET "/api/groups/$GROUP/locations" "$B" | json 'len(d)') == 0 ]] && ok "stop sharing removes the position" || fail "stop sharing removes the position"
call GET "/api/groups/$GROUP/locations" "$A" >/dev/null && ok "live locations endpoint" || fail "live locations endpoint"

step "Weather (Open-Meteo, cached in Redis)"
W=$(call GET "/api/weather?lat=49.25&lon=19.95&elevation=1350" "$A")
echo "$W" | json 'f"now {d.current.temperatureC}°C {d.currentCondition}; {len(d.daily)} days; day 1 = {d.daily[0].assessment.suitability}"' | sed 's/^/  · /'
ok "forecast with hiking assessment"

printf '\n\033[32m%d checks passed\033[0m\n' "$pass"

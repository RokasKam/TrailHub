# TrailHub

[![CI](https://github.com/RokasKam/TrailHub/actions/workflows/ci.yml/badge.svg)](https://github.com/RokasKam/TrailHub/actions/workflows/ci.yml)

Hiking app: discover trails, record hikes with GPS (offline-first), import/export GPX, plan
multi-day trips, hike in groups with live location, and check mountain weather.

```
mobile/    Expo (React Native) app · Zustand · TanStack Query · Mapbox · Auth0
backend/   .NET 10 microservices · clean architecture · PostgreSQL/PostGIS · Redis · RabbitMQ · YARP
```

## Architecture

```
                    ┌──────────────────────────── Auth0 (login, sign-up, refresh tokens)
                    │ access token (JWT)
 ┌────────┐   HTTPS/WSS   ┌─────────────┐
 │ Mobile │ ────────────▶ │ YARP gateway│ :8080   rate limiting, CORS, WebSockets
 └────────┘               └──────┬──────┘
          ┌───────────┬──────────┼───────────┬────────────┬─────────────┐
          ▼           ▼          ▼           ▼            ▼             ▼
      Profile      Trails     Activity     Trips        Groups       Weather
     achievements  PostGIS    GPS tracks   itineraries  SignalR hub  Open-Meteo
          │           │          │           │            │   │         │
          └─ Postgres ┴─ Postgres┴─ Postgres ┴─ Postgres  ┘   └── Redis ┘
             (one database per service)                     live locations, cache, backplane
          ▲                      │
          └──── RabbitMQ ◀───────┘  ActivityCompleted / ActivityDeleted → achievements & totals
```

| Service  | Owns | Storage |
|----------|------|---------|
| Profile  | Profile, preferences, hiking totals, achievements (driven by activity events) | PostgreSQL |
| Trails   | Trails, GPX import/export, POIs, elevation profiles, favorites, reviews, condition reports, photos | PostgreSQL + **PostGIS** (GiST-indexed `geography` for radius/bounding-box search, trigram indexes for text search, `jsonb` full-resolution routes) · photos in **S3-compatible storage** (SeaweedFS locally, S3/R2 in production), served at `/photos` |
| Activity | Recorded/imported hikes with pause/resume segments, statistics, GPX export | PostgreSQL + PostGIS (`multilinestring` per hike) |
| Trips    | Multi-day trips, per-day trail plans, overnight stops, checklists | PostgreSQL |
| Groups   | Groups, invite codes, scheduled group hikes with RSVPs and meeting points, **real-time location** (SignalR/WebSockets to watch, REST to share so it works in the background) | PostgreSQL + Redis (ephemeral positions, SignalR backplane) |
| Weather  | Current conditions, 16-day forecasts downscaled to trail elevation, per-day hiking suitability | Redis cache (Open-Meteo upstream) |

**Clean architecture, per service:** `Domain` (entities, value objects, rules; no framework
references) ← `Application` (commands/queries/handlers, repository interfaces) ← `Infrastructure`
(EF Core, PostGIS, MassTransit, Redis, HTTP clients) ← `Api` (thin `[ApiController]` controllers that
call one handler each and map its `Result` to an HTTP response or RFC 9457 problem details).
Shared kernels live in `backend/src/BuildingBlocks`; services share only `TrailHub.Contracts`
(integration events). `tests/TrailHub.ArchitectureTests` fails the build if a layer or service
references something it shouldn't, or if an aggregate exposes a public setter.

No mediator library: controller actions inject (`[FromServices]`) the exact `ICommandHandler<…>`/`IQueryHandler<…>`
they call, so each action's dependencies are visible in its signature.
MassTransit is pinned to v8 (Apache-2.0); v9+ requires a commercial license.

## Run it locally

Requirements: Docker Desktop, Node 20+, Xcode and/or Android Studio. The .NET 10 SDK is optional:
`backend/build.sh` builds and tests inside the official SDK container.

### 1. Backend

```bash
cd backend
cp .env.example .env          # set AUTH_MODE=Development to try it without Auth0
docker compose up -d --build  # gateway on http://localhost:8080
./scripts/smoke-test.sh       # 42 end-to-end checks through the gateway (needs AUTH_MODE=Development)
node scripts/group-trips-test.mjs  # 21 checks for shared group trips: access, claims, live updates
```

**Swagger UI: http://localhost:8080/swagger.** It's one page for all six services; pick one under *Select a definition*.
Click **Authorize** and paste an Auth0 access token (Dashboard → Applications → APIs → TrailHub API → **Test**)
to try requests; in `AUTH_MODE=Development` paste any user id instead. Each service also serves its own UI at
`/swagger` when run directly with `dotnet run`. Swagger is on in Development; set `Swagger__Enabled=true` to
expose it elsewhere. Infra host ports are moved off the defaults
to avoid clashing with other local stacks: Postgres `55432`, Redis `56379`, RabbitMQ `55672` (UI `55673`).

Tests: `./build.sh 'dotnet test TrailHub.slnx'` (domain, GPX and architecture tests).

Photos are stored in SeaweedFS (S3 API on `localhost:58333`, dev credentials in `infra/seaweedfs/s3.json`).
MinIO no longer publishes container images, and the Trails service only speaks S3, so production can use
AWS S3, Cloudflare R2 or any S3-compatible store by setting `Photos__ServiceUrl` (omit for AWS),
`Photos__Bucket`, credentials (or an IAM role) and `Photos__PublicBaseUrl` (your CDN). When testing on a physical
phone, set `PHOTOS_PUBLIC_BASE_URL` to your computer's LAN address so photos load.

### 2. Mobile

```bash
cd mobile
cp .env.example .env.local    # API URL, auth mode, Auth0 + Mapbox values
npx expo run:ios              # or: npx expo run:android
```

Mapbox, Auth0 and background GPS are native modules, so the app runs in a **development build**
(`expo run:*` or EAS Build), not Expo Go. After the first native build, `npx expo start` is enough
for JS changes.

**After changing `app.config.ts` plugins or permissions**, regenerate the native project before building:
`npx expo prebuild -p ios` (`expo run:ios` doesn't redo this on its own once `ios/` exists). Skipping it
can ship a build without, e.g., the photo-library permission text, and iOS kills the app the moment it asks.

## Auth0 setup

1. **APIs → Create API**: identifier e.g. `https://api.trailhub.app` (this is the *audience*), signing RS256.
   Enable **Allow Offline Access** so the app gets refresh tokens.
2. **Applications → Create → Native**. Allowed Callback and Logout URLs (iOS and Android):
   ```
   trailhub.auth0://YOUR_DOMAIN/ios/app.trailhub.mobile/callback
   trailhub.auth0://YOUR_DOMAIN/android/app.trailhub.mobile/callback
   ```
   Under Advanced → Grant Types, keep *Authorization Code* and *Refresh Token*. Turn on
   **Refresh Token Rotation**.
3. **Actions → Library → Create Action** (Login / Post Login): paste
   [`backend/infra/auth0/add-profile-claims.js`](backend/infra/auth0/add-profile-claims.js), deploy it,
   and add it to the post-login trigger. It puts the user's name and email into the access token so
   new profiles get a real name.
4. Run `./scripts/use-auth0.sh <domain> <client-id> <audience>`. It writes `backend/.env`
   (`AUTH_MODE=Auth0`, `AUTH0_DOMAIN`, `AUTH0_AUDIENCE`) and `mobile/.env.local` (`EXPO_PUBLIC_AUTH_MODE=auth0`
   plus domain, client id, audience). Then restart the backend and rebuild the dev client
   (the domain is baked into the native callback config).

Registration, social logins, password reset and MFA all happen in Auth0 Universal Login. The SDK
keeps the refresh token in the Keychain/Keystore and renews access tokens transparently. Each
service validates the JWT (issuer, audience, lifetime); the user id everywhere is the `sub` claim.

## Offline behavior

- **Recording** runs fully on the device (background location task) and is persisted on every GPS
  batch, so a crash or the OS killing the app doesn't lose the track.
- Finished hikes go to an **outbox** and upload with `PUT /api/activities/{client-generated id}`.
  Retries are idempotent, uploads resume when connectivity returns, and rejected uploads are kept for the user.
- **Trail data** (details, routes, elevation, stats) is cached by TanStack Query and persisted to
  disk for 7 days. **Save offline** on a trail also downloads Mapbox tiles (zoom 10–15) around the route.

## Group location sharing

Members watch the live map over SignalR (`/hubs/groups`). Sharing your own position uses
`POST /api/groups/{id}/locations/mine` from a background location task (balanced accuracy, every 25 m or
30 s), because a WebSocket doesn't survive the app being suspended. The server broadcasts each update to
the group's live map, and positions expire after 15 minutes so a phone that goes quiet drops off.
Sharing persists across app restarts until you stop it or leave the group.

## Status

Working end to end (verified by `scripts/smoke-test.sh`, 52 checks, and SignalR client tests): profiles and
preferences, trail import/search/filters/details, GPX import/export, POIs, favorites, reviews,
condition reports, trail photos, activity recording upload (idempotent), statistics, achievements via RabbitMQ,
trips, groups with RSVPs and meeting points, live location (WebSocket viewing, REST/background sharing),
and weather with hiking assessments.

The iOS app runs on the iOS 27 simulator with Auth0 login, Mapbox maps, GPX import and GPS recording
(tested with a simulated route). Android hasn't been run yet.

Next steps that aren't built yet:
- **Photo moderation and deletion**: anyone signed in can add a trail photo; there's no report/delete flow yet.
- **Push notifications**: `GroupHikeScheduled` is already published to the bus; it needs a
  notifications service with Expo Push / APNs / FCM.
- **Transactional outbox** (MassTransit EF outbox) so events can't be lost between the DB commit and publishing.
- Activity sharing/feed and Mapbox offline pack management UI.

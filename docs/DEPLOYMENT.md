# Deployment & Operations

How the system is configured, built, migrated, and run — the platform-agnostic config plus the concrete
reference environment it is deployed to.

**Related:** [DATABASE.md](DATABASE.md) (migration chains) · [AUTHENTICATION.md](AUTHENTICATION.md) (token tunables) · [TESTING.md](TESTING.md)

## Runtime

- **Backend** — .NET 10 ASP.NET Core: `dotnet run --project Presentations/WebApi`. OpenAPI + Scalar are mapped in Development only. Kestrel listens on `8080` in the container image; TLS terminates upstream.
- **Frontend** — Angular SPA: `ng build` emits static assets; in production an nginx container serves them and reverse-proxies `/api` → the API (single origin, no CORS). Built and deployed from the **GymBroPortal** repo.
- **Datastore** — PostgreSQL: one physical database, two EF migration chains (`AppDbContext`, `IdentityDbContext`).
- **Cache / rate limits / revocation** — Redis (required in Production; a `Memory` provider exists for single-instance/dev).

## Configuration

Only `appsettings.json` + `appsettings.Development.json` are in the repo; everything sensitive comes from
environment variables or user-secrets (`UserSecretsId` on `WebApi.csproj`). Env-var form uses `__` for `:`
(e.g. `Jwt__Secret`, `ConnectionStrings__Database`).

| Key | Purpose | Required |
|---|---|---|
| `ConnectionStrings:Database` | Postgres connection | **yes** |
| `ConnectionStrings:Redis` | Redis — shared cache, auth rate limiting, SecurityStamp revocation | **yes** unless `Cache:Provider=Memory` |
| `Cache:Provider` | `Memory` opts into in-memory `IDistributedCache` (single-instance / tests only) | optional |
| `Database:AutoMigrate` | `true` applies both migration chains at startup (single-instance / dev). Default `false` = verify-only fail-fast | optional |
| `Jwt:Secret` (≥ 32 chars) | access-token signing | **yes** — startup **fails fast** if missing, short, or still the committed placeholder |
| `Jwt:Issuer` / `Jwt:Audience` | token validation | **yes** |
| `Jwt:AccessTokenMinutes` (15) · `Jwt:RefreshTokenDays` (14) · `Jwt:RefreshTokenCleanupEnabled` (true) · `Jwt:RefreshTokenCleanupIntervalHours` (24) · `Jwt:RefreshTokenRetentionDays` (1) | token lifetimes / expired-token purge (set `…CleanupEnabled=false` to drop the periodic sweep) | optional |
| `Outbox:FallbackInterval` / `BatchSize` / `MaxAttempts` / `Retention` / `CleanupEnabled` / `CleanupInterval` | outbox safety-net drain interval / batch / poison cap / retention / purge toggle / purge cadence. Dispatch is signal-driven (a committing write wakes the processor immediately); `FallbackInterval` is only the periodic catch-up so the DB stays idle between writes (lets a serverless Postgres scale to zero) | optional (`06:00:00` / `50` / `10` / `7.00:00:00` / `true` / `1.00:00:00`) |
| `Reconciliation:Enabled` / `Interval` / `InitialDelay` | cross-store (`AppUser`↔`User`) drift **detector** (read-only; never mutates). **Off by default** — opt in on multi-instance or after suspected drift | optional (`false` / `06:00:00` / `00:01:00`) |
| `NutritionMaintenance:DayCloseEnabled` / `DayCloseInterval` / `DayCloseInitialDelay` | background close of stale nutrition days (finalize adherence). Cadence is a latency-vs-idle trade-off — the close has no time-critical consumer. Set `DayCloseEnabled=false` to drop the sweep | optional (`true` / `06:00:00` / `00:01:00`) |
| `Cors:AllowedOrigins` (array) | production CORS allow-list (cross-origin SPA). Omit for a same-origin reverse-proxied deploy | optional |
| `ForwardedHeaders:Enabled` | `true` honours `X-Forwarded-Proto`/`-For` (correct `Secure` cookie + real client IP). Enable **only behind a trusted reverse proxy** | optional (default `false`) |
| `OpenTelemetry:OtlpEndpoint` (or `OTEL_EXPORTER_OTLP_ENDPOINT`) | OTLP export of traces + metrics; absent → in-process only | optional |
| `Email:SmtpHost` (+ port/ssl/credentials/from/`ResetPasswordUrl`) | password-reset mail; absent → Development logs masked recipient + subject only (token never logged), non-Development no-ops | optional |
| `Seed:AdminEmail` / `Seed:AdminPassword` | seed a platform admin outside Development | optional |

## Migrations

Run **both** chains before/at deploy:

```bash
dotnet ef database update --project BuildingBlocks/Infrastructure/BuildingBlocks.Infrastructure.Persistence --startup-project Presentations/WebApi
dotnet ef database update --project Modules/Modules.Identity --startup-project Presentations/WebApi
```

By default startup is **verify-only**: it fails fast if either chain has pending migrations, so a half-migrated DB
won't serve traffic and serving replicas never race to migrate. Options:

- **Single-instance / dev:** set `Database:AutoMigrate=true` to apply both chains at startup (the compose stacks use this).
- **Multi-instance:** run the `--migrate` entrypoint (`dotnet WebApi.dll --migrate`, applies both chains and exits) as a dedicated pre-deploy step, and keep `AutoMigrate` off on serving replicas.

The `DbSeeder` runs only in Development (or when `Seed:Admin*` is set) — seeded admin
`admin@gymbro.local` / `Admin@123456`.

## Health probes

- `GET /health` — liveness (no dependencies checked).
- `GET /health/ready` — readiness: both EF contexts reachable; Redis ping when configured; outbox dead-letter check (Degraded, not Unhealthy, when poison messages exist).

Both are anonymous so any platform can probe without a token.

## CORS & cookies

Dev allows any **loopback** origin (`localhost`/`127.0.0.1`, any port) so the Angular portal (`:4200`) and the
Flutter web dev server (an ephemeral port) both work without re-listing ports. Development also **skips HTTPS
redirection** so native/dev clients can talk plain HTTP to `http://localhost:5216`; forcing a redirect would
bounce them to the untrusted dev cert on `:7015`. Both are Development-only — production still redirects to HTTPS
and restricts origins. Production CORS is config-driven: set `Cors:AllowedOrigins` (with
credentials so the refresh cookie flows) for a cross-origin SPA; a same-origin deploy (SPA reverse-proxied to
`/api`) needs none. The `gymbro_refresh` cookie is `HttpOnly`, `SameSite=Strict`, `Path=/api/auth`, `Secure` on
HTTPS (so it also rides plain-HTTP `localhost` in dev). Behind a TLS-terminating proxy, set `ForwardedHeaders:Enabled=true` (and forward `X-Forwarded-Proto`) or
the `Secure` flag won't apply.

## Operational properties

| Concern | Status | Notes |
|---|---|---|
| Exercise-catalog cache | **Redis-backed** | `IDistributedCache` + generation counters; invalidated on admin writes |
| SecurityStamp revocation cache | **Redis-backed** | re-checked on every authenticated request; evicted on password change / logout-all |
| Rate limiting (`auth`, `auth-refresh`, `tenant-join`) | **Redis-backed** | fixed-window; `auth`/`auth-refresh` per client IP, `tenant-join` per caller |
| Domain events | **Transactional outbox** | dispatched by `OutboxProcessor` claiming rows with `FOR UPDATE SKIP LOCKED` (multi-instance-safe, at-least-once); poison messages surface via the health check |
| Cross-store consistency (`AppUser` ↔ `User`) | **Reconciliation check** | read-only drift reporter; write-time consistency is the cross-store transaction |
| No Redis + no `Cache:Provider=Memory` | **Startup fails** | no silent in-memory fallback |

- **Multi-instance ready when Redis is configured** — cache, token-revocation eviction, and rate limits are shared across replicas; domain events go through the outbox. Keep `Database:AutoMigrate` off on serving replicas.
- **Observability** — OpenTelemetry tracing + metrics (meters `GymBro.Cache`, `GymBro.Outbox`, `GymBro.Reconciliation`); OTLP export is opt-in. Structured JSON logs (request/trace scopes) outside Development; readable console in dev.

## Background jobs (hosted services)

| Service | Does |
|---|---|
| `OutboxProcessor` | dispatches persisted domain events (at-least-once) |
| `OutboxCleanupService` | purges processed outbox rows past `Outbox:Retention` |
| `RefreshTokenCleanupService` | purges expired refresh tokens (see [AUTHENTICATION.md](AUTHENTICATION.md)) |
| `CrossStoreReconciliationService` | reports `AppUser` ↔ `User` drift; never mutates |

## Secret management

- Secrets live in `dotnet user-secrets` (local) or environment variables / a secret store (prod) — never in `appsettings*.json`. `appsettings.Development.json` is gitignored; a `.example` is committed.
- Startup rejects a missing / short / placeholder `Jwt:Secret`.
- CI runs **gitleaks** (`.github/workflows/secret-scan.yml`) on push/PR.
- **Rotating the JWT signing key** invalidates every existing access token immediately (symmetric key). Refresh tokens are not signed by it, so clients with a valid `gymbro_refresh` cookie silently re-authenticate — to force a full re-login (e.g. if the key leaked) also revoke refresh tokens (`POST /api/auth/logout-all` per user, or clear the `RefreshTokens` table). **A secret committed to git is compromised: rotate it, don't just scrub history.**

## Build & images

| Image | Built from | Contents |
|---|---|---|
| `ghcr.io/<owner>/gymbro` (+ `registry.fly.io/gymbro-api`) | this repo's `Dockerfile` | .NET 10 runtime + API |
| `ghcr.io/<owner>/gymbroportal` | GymBroPortal `Dockerfile` | nginx + built Angular SPA (compose / self-host only) |

- `gymbro/docker-compose.yml` runs the full local backend stack (Postgres + Redis + API; `Database__AutoMigrate=true`): `docker compose up --build`.
- Each image is an ordinary OCI container with no orchestrator-specific assets — it runs on any container host or compose.

## CI/CD

Each repo has two GitHub Actions workflows, with no overlap:

- **`ci.yml`** — build + test on every PR (and as the CD gate). API: Postgres service container, coverage gate, vulnerable-package scan (NuGet audit is warnings-as-errors, so a new advisory on any transitive package turns CI red until the package is bumped). SPA: `npm ci`, build, API-contract drift check, headless Karma. CodeQL + Dependabot are also configured.
- **`cd.yml`** — on push to `main`/`master` (or a `v*` tag), after the CI gate passes:
  - **API:** build the image once, push it to GHCR *and* `registry.fly.io/gymbro-api:<sha>`, then `flyctl deploy --image …` against `fly.toml`. Secret: `FLY_API_TOKEN` (`flyctl tokens create deploy -a gymbro-api`).
  - **Portal:** `npm ci && npm run build`, then `wrangler pages deploy` of the build output (config in the portal's `wrangler.toml`). Secrets: `CLOUDFLARE_API_TOKEN` (Pages edit permission) + `CLOUDFLARE_ACCOUNT_ID`.

  A `concurrency` guard serialises deploys; each repo deploys only its own surface.

## Reference environment

The app is platform-agnostic; this is the one environment it is actually deployed to (since 2026-10; the previous
single-VM compose stack is kept under [Self-hosting with compose](#self-hosting-with-compose)).

```
          Cloudflare Pages (static SPA + /api proxy Function)            Fly.io (sin)
browser ───▶ gymbro-portal.pages.dev ── /api/* ──▶ gymbro-api.fly.dev ──▶ api Machine :8080 ──▶ Neon (Postgres)
mobile  ──────────────────────────────────────────▶ gymbro-api.fly.dev ──┘
```

- **API — Fly.io** (`fly.toml` in this repo): one `shared-cpu-1x` / 512 MB Machine in `sin` (next to Neon `ap-southeast-1`),
  `auto_stop_machines = "stop"` + `min_machines_running = 0`, so an idle deployment is billed only for its root disk and
  the first request after idle pays a cold start (a few seconds, including the Neon wake-up). Fly's edge terminates
  TLS and sets `X-Forwarded-Proto/-For` (`ForwardedHeaders__Enabled=true`). Single instance ⇒ `Cache__Provider=Memory`
  (no Redis) and `Database__AutoMigrate=true` (both chains applied on boot). Secrets live in Fly
  (`flyctl secrets set ConnectionStrings__Database=… Jwt__Secret=…`), never in `fly.toml`.
- **Portal — Cloudflare Pages:** the Angular build output is served as static assets, and a Pages Function
  (`functions/api/[[path]].ts`) reverse-proxies `/api/*` to the Fly app. That keeps the SPA and the API on **one
  origin**, which the `SameSite=Strict` refresh cookie requires — no CORS, exactly as nginx did on the VM. The
  upstream origin is the `API_ORIGIN` var in `wrangler.toml`. Pages serves `index.html` for unknown paths (no
  `404.html`), which is the SPA routing fallback.
- **Mobile** talks to `https://gymbro-api.fly.dev` directly (`gymbroapp/config/prod.json`).
- **Data:** Neon (Postgres) is the only stateful piece; both compute surfaces are disposable.
- **Updating the app = push to `main`** → CD redeploys that surface.

### Day-to-day operations

```bash
flyctl status -a gymbro-api              # Machine state (stopped = idle, normal)
flyctl logs -a gymbro-api                # live logs
flyctl secrets list -a gymbro-api        # which secrets are set (never the values)
flyctl ssh console -a gymbro-api         # shell into the Machine
flyctl deploy --image registry.fly.io/gymbro-api:<sha>   # redeploy a known image manually
```

Catalog reseed: `flyctl machine run registry.fly.io/gymbro-api:<sha> --app gymbro-api --region sin --rm -- --reseed-exercises`
(the image ENTRYPOINT is `dotnet WebApi.dll`, so pass only the flag; the Machine inherits the app's secrets).

### Reproducing from scratch

1. `flyctl auth login`, then in this repo: `flyctl apps create gymbro-api` (change the name in `fly.toml` if taken).
2. `flyctl secrets set -a gymbro-api ConnectionStrings__Database='…' Jwt__Secret="$(openssl rand -base64 48)"` (+ `Seed__AdminEmail`/`Seed__AdminPassword` for a fresh database).
3. First deploy from the laptop: `flyctl deploy` (remote build). Then `flyctl tokens create deploy -a gymbro-api` → repo secret `FLY_API_TOKEN`; every later deploy is CD.
4. Cloudflare: create a Pages project named as in the portal's `wrangler.toml`, an API token with *Cloudflare Pages: Edit*, and set `CLOUDFLARE_API_TOKEN` + `CLOUDFLARE_ACCOUNT_ID` in the portal repo. Merge to `main` → CD deploys.
5. Point the mobile app at the Fly hostname (`gymbroapp/config/prod.json`) and rebuild it.

### Self-hosting with compose

The deploy root (outside both repos) still carries the compose stack used before Fly — `docker-compose.yml` (pulls the
GHCR images; Caddy terminates TLS and the portal's nginx proxies `/api`), `docker-compose.build.yml` (build from
source), `docker-compose.prod.yml` (local Postgres instead of Neon). It needs an `app.env` (see `app.env.example`)
and any Linux host with Docker: `docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build`.
Migrations still run on API start (`Database__AutoMigrate=true`).

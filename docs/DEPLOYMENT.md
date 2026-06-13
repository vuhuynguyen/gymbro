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
| `Jwt:AccessTokenMinutes` (15) · `Jwt:RefreshTokenDays` (14) · `Jwt:RefreshTokenCleanupIntervalHours` (6) · `Jwt:RefreshTokenRetentionDays` (1) | token lifetimes / cleanup | optional |
| `Outbox:PollInterval` / `BatchSize` / `MaxAttempts` / `Retention` / `CleanupInterval` | outbox processor cadence / batch / poison cap / retention / purge cadence | optional (`00:00:10` / `50` / `10` / `7.00:00:00` / `01:00:00`) |
| `Reconciliation:Enabled` / `Interval` / `InitialDelay` | cross-store (`AppUser`↔`User`) drift check | optional (`true` / `06:00:00` / `00:01:00`) |
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
| `ghcr.io/<owner>/gymbro` | this repo's `Dockerfile` | .NET 10 runtime + API |
| `ghcr.io/<owner>/gymbroportal` | GymBroPortal `Dockerfile` | nginx + built Angular SPA |

- `gymbro/docker-compose.yml` runs the full local backend stack (Postgres + Redis + API; `Database__AutoMigrate=true`): `docker compose up --build`.
- Each image is an ordinary OCI container with no orchestrator-specific assets — it runs on any container host or compose.

## CI/CD

Each repo has two GitHub Actions workflows, with no overlap:

- **`ci.yml`** — build + test on every push/PR (no image). API: Postgres service container, coverage gate, vulnerable-package scan. SPA: `npm ci`, build, API-contract drift check, headless Karma. CodeQL + Dependabot are also configured.
- **`cd.yml`** — on push to `main`/`master` (or a `v*` tag): build the image, push to GHCR, then SSH to the host and `docker compose pull <svc> && up -d <svc>`. A `concurrency` guard serialises deploys; each repo deploys only its own service. GHCR auth on the host uses the workflow's ephemeral `GITHUB_TOKEN` — no long-lived token is stored.

Required repo secrets (both repos): `VM_HOST`, `VM_USER`, `DEPLOY_SSH_KEY`.

## Reference environment

The app is platform-agnostic; this is the one environment it is actually deployed to.

```
        HTTPS (Caddy / Let's Encrypt)       single VM (GCP Compute Engine)
browser ──────────────────────────▶ ┌─────────────────────────────────────────────┐
                                     │ caddy :443 ─▶ frontend nginx :80 ─/api─▶ api :8080│
                                     └────────────────────┬──────────────┬─────────────┘
                                                    Neon (Postgres)   Redis Cloud
```

- **Host:** one Linux VM (GCP Compute Engine `e2-micro`; the stack is provider-agnostic and was previously on Oracle Always Free — see the deploy-root `MIGRATION.md`). The app directory (`~/gymbro-app`) holds the compose files + an `app.env` (never committed) with the Neon connection string, the Redis Cloud connection string, `Jwt__Secret`, and the seed admin.
- **TLS:** a **Caddy** service terminates HTTPS with an auto-issued, auto-renewed **Let's Encrypt** certificate (config in the deploy-root `Caddyfile`, generated per-host by `provision-vm.sh`) and reverse-proxies to the frontend nginx.
- **One origin:** behind Caddy, the frontend's nginx serves the SPA and reverse-proxies `/api` → `api:8080`, so there is no CORS. Data is fully external (Neon + Redis Cloud), so the VM is stateless and disposable.
- **Compose files** (live at the deploy root, outside both app repos):

  | File | Role |
  |---|---|
  | `docker-compose.yml` | canonical prod — pulls the GHCR images; what CD runs |
  | `docker-compose.build.yml` | override that builds from source instead of pulling (first bootstrap / CI unreachable) |
  | `docker-compose.prod.yml` | self-contained alternative with a local Postgres container (no Neon/Redis Cloud) |

- **Migrations** run on API container start (`Database__AutoMigrate=true`, single instance) — no separate migrate job.
- **Updating the app = push to `main`** → CD rebuilds the changed image and redeploys that service. Containers are `restart: unless-stopped` and Docker is enabled at boot, so reboot/crash survival is automatic.

### Day-to-day operations

```bash
ssh <user>@<vm-host>
cd ~/gymbro-app
docker compose ps                 # status
docker compose logs -f api        # live logs
docker compose restart api        # restart a service
docker compose pull && docker compose up -d   # manual image refresh
```

### Reproducing from scratch

1. Provision a VM with a public IP; open the firewall for 80/443 (cloud security group **and** host firewall).
2. Install Docker.
3. Copy the deploy root to `~/gymbro-app` and create `~/gymbro-app/app.env` (secrets; see `app.env.example`).
4. Bootstrap the first run (no GHCR images yet): `docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build`.
5. Authorize a deploy key on the VM; set `VM_HOST` / `VM_USER` / `DEPLOY_SSH_KEY` secrets in both repos.
6. Merge to `main` → CD takes over; every later deploy is `pull` + `up -d` of the GHCR image.

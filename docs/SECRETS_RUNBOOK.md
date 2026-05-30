# Secret Rotation & Git-History Scrub Runbook

> **Purpose:** Step-by-step remediation when credentials were committed to the `gymbro/` repo (SR1).
> **When to run:** Any time a credential is found in tracked files or history.
> **Owner:** Repo maintainer / ops. **Est. time:** ~30–60 min (+ collaborator re-sync).
> **Related:** [`BACKEND.md`](BACKEND.md) · [`../../docs/SYSTEM_OVERVIEW.md`](../../docs/SYSTEM_OVERVIEW.md) (SR1).

> ⚠️ **Golden rule:** a secret committed to git is **compromised forever**. History rewriting reduces exposure,
> but **rotation is the only real fix** — assume leaked values are known and rotate them first. Code-side
> hardening: `appsettings.Development.json` is gitignored, `.example` committed, startup rejects a weak
> `Jwt:Secret`, and CI runs gitleaks (`.github/workflows/secret-scan.yml`).

## 0. Order of operations (do not reorder)

1. **Rotate** every leaked credential (treat as compromised) — fastest, real mitigation.
2. **Verify** the app runs with the new secrets.
3. **Scrub** the values from git history (only if they appear in commits).
4. **Force-push** and have all collaborators re-sync (only after a history rewrite).
5. **Prevent** recurrence (CI secret scan + secret store).

---

## 1. Inventory — what leaked (and where to rotate it)

| Secret | Former location | Rotate in |
|---|---|---|
| Postgres password | `Presentations/WebApi/appsettings.Development.json` → `ConnectionStrings:Database` (local dev file; was gitignored) | PostgreSQL + secret store |
| JWT signing key (HS256) | same file → `Jwt:Secret` | secret store (regenerate) |
| JWT placeholder | tracked `appsettings.json` → `Jwt:Secret` | ensure prod supplies a real one (placeholder is not a secret) |

Also rotate any **production** copies (env vars, Key Vault, CI variables) if the same values were reused.

**2026-05-30 status:** credentials rotated locally; secrets live in `dotnet user-secrets` only. Full-history scan
of `gymbro/` and `GymBroPortal/` found **no commits** containing the dev connection string or JWT — scrub not required.

---

## 2. Rotate first

### 2a. PostgreSQL password
```bash
# As a DB admin (psql):
ALTER ROLE <db-user> WITH PASSWORD '<new-strong-password>';
# (better long-term: create a dedicated least-privilege app role and use that)
```
Store the new value **outside** tracked files:
```bash
cd gymbro/Presentations/WebApi
dotnet user-secrets init   # if not already initialised (UserSecretsId in WebApi.csproj)
dotnet user-secrets set "ConnectionStrings:Database" "Host=127.0.0.1;Port=5432;Database=GymBroDb;Username=<user>;Password=<new-strong-password>"
```
Prod: set via env (`ConnectionStrings__Database`) or Key Vault / Secrets Manager — never in `appsettings*.json`.

### 2b. JWT signing key
```bash
openssl rand -base64 48          # generate a new key (>= 32 chars; the startup guard enforces this)
cd gymbro/Presentations/WebApi
dotnet user-secrets set "Jwt:Secret" "<generated-key>"
# prod: env var Jwt__Secret, or a Key Vault reference
```
**Impact:** the key is symmetric, so **every existing JWT becomes invalid immediately**. Tokens are 24h with no
refresh, so all users must re-login — pick a low-traffic window and communicate it.

### 2c. Confirm secrets are not in any tracked file
```bash
cd gymbro
git ls-files | xargs grep -nE "Password=[^Y][^O]|dev-secret-key" 2>/dev/null   # expect: no output
git check-ignore Presentations/WebApi/appsettings.Development.json              # expect: the path (ignored)
```

---

## 3. Verify the app with the new secrets
```bash
cd gymbro
dotnet ef database update --project BuildingBlocks/Infrastructure/BuildingBlocks.Infrastructure.Persistence --startup-project Presentations/WebApi --context AppDbContext
dotnet ef database update --project Modules/Modules.Identity --startup-project Presentations/WebApi --context IdentityDbContext
dotnet run --project Presentations/WebApi          # must start (no startup exception from the Jwt:Secret guard)
# then: POST /api/auth/login → returns a token; an authenticated + X-Tenant-Id request succeeds; DB reads work
```

---

## 4. Scrub git history (`gymbro/` repo) — only if secrets appear in commits

> ⚠️ **Destructive history rewrite.** Skip this section if step 2c and a full-history search find no leaked strings.
> **Back up first:** `git clone --mirror <url> gymbro-backup.git`.

Check first:
```bash
cd gymbro
git rev-list --all | while read c; do git grep -l '<leaked-substring>' $c 2>/dev/null; done
```

### Option A — `git filter-repo` (recommended)
```bash
pip install git-filter-repo            # or: brew install git-filter-repo
cd gymbro
cat > /tmp/secrets.txt <<'EOF'
<leaked-db-password>==>REDACTED
<leaked-jwt-secret>==>REDACTED
EOF
git filter-repo --replace-text /tmp/secrets.txt
```

Stronger alternative — purge the whole file from history (since it's now untracked anyway):
```bash
git filter-repo --path Presentations/WebApi/appsettings.Development.json --invert-paths
```

---

## 5. Push & collaborator re-sync (after history rewrite only)
```bash
git remote add origin <url>   # filter-repo removes 'origin' by design
git push --force-with-lease origin --all
git push --force-with-lease origin --tags
```
Every collaborator must re-clone or hard-reset. Hosted git caches/forks may retain old commits — **rotation (step 2) is what actually protects you.**

---

## 6. Prevent recurrence

In place:
- ✅ `appsettings.Development.json` gitignored; `appsettings.Development.json.example` committed.
- ✅ Startup guard rejects a missing/`<32`-char `Jwt:Secret`.
- ✅ `UserSecretsId` on `WebApi.csproj`; local secrets in `dotnet user-secrets`.
- ✅ CI secret scanning: `.github/workflows/secret-scan.yml` (gitleaks on push/PR).

Optional: local pre-commit hook — `gitleaks protect --staged`.

---

## 7. Completion checklist
- [x] PostgreSQL password rotated and stored in user-secrets (2026-05-30)
- [x] JWT `Jwt:Secret` regenerated (≥32 chars) and stored in user-secrets
- [ ] Prod/CI copies of old values rotated (if any were reused outside this machine)
- [x] App DB connectivity verified with new password
- [x] History scan: no leaked strings in `gymbro/` or `GymBroPortal/` commits — scrub skipped
- [x] CI secret scan workflow present
- [ ] All collaborators using user-secrets / env (not real values in `appsettings.Development.json`)

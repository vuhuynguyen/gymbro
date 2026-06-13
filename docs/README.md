# GymBro API — Documentation

Reference documentation for the GymBro API. The [root README](../README.md) is the entry point (purpose, stack,
local setup, run/test/build); these documents go deeper on one topic each. One fact lives in one document.

| Document | Owns |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Modular-monolith layout, the seven modules, boundaries & dependency rules, the request pipeline, conventions |
| [DATABASE.md](DATABASE.md) | Entity ownership, multi-tenant filters, constraints, migrations, delete/audit semantics |
| [PERMISSIONS.md](PERMISSIONS.md) | Roles, the permission matrix, enforcement layers, tenant isolation |
| [AUTHENTICATION.md](AUTHENTICATION.md) | Access/refresh token lifecycle, rotation, revocation |
| [BUSINESS_RULES.md](BUSINESS_RULES.md) | Plan / assignment / session / membership lifecycle rules and edge cases |
| [USER_FLOWS.md](USER_FLOWS.md) | End-to-end flows and the endpoints each step hits |
| [SEEDING.md](SEEDING.md) | The two global catalog seeds (exercises & foods): layered pipeline, run, data safety, licensing |
| [TESTING.md](TESTING.md) | Test strategy, inventory, how to run |
| [DEPLOYMENT.md](DEPLOYMENT.md) | Configuration, migrations, health, ops, build, CI/CD, reference environment |
| [ROADMAP.md](ROADMAP.md) | Not-yet-built design: nutrition (offline/reminders/push/analytics) + exercise master-data (model/media/import) |

The Angular client is documented in its own repository (**GymBroPortal**).

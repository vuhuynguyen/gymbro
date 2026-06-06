# GymBro API (`gymbro/`)

.NET 10 · EF Core · PostgreSQL · MediatR/CQRS — the backend for the **GymBro** multi-tenant fitness-coaching
platform. It records training **output** (weight, reps, duration, distance) and supports self-training and
trainer–trainee workflows with session logging and client-side progress analytics.

**Start with the documentation, not the code:**

- System single source of truth → [`../docs/README.md`](../docs/README.md)
- Backend build / run / migrate / conventions → [`docs/BACKEND.md`](docs/BACKEND.md)
- Repo conventions for AI agents → [`CLAUDE.md`](CLAUDE.md)

Run (two migration chains, then the API): see [`docs/BACKEND.md`](docs/BACKEND.md#run--migrate--seed).

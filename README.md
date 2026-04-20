# DevInsight – Developer Productivity Tracker

A scalable SaaS-style platform that integrates with Git providers (GitHub, GitLab) and issue tracking tools (Jira) to analyze developer productivity and provide actionable insights.

## Architecture

```
DevInsight/
├── src/
│   ├── DevInsight.Domain/           # Entities, enums, interfaces (no dependencies)
│   ├── DevInsight.Application/      # CQRS commands/queries, DTOs, service interfaces
│   ├── DevInsight.Infrastructure/   # EF Core, GitHub client, encryption, Hangfire jobs
│   └── DevInsight.API/             # ASP.NET Core Web API, controllers, auth
├── tests/
│   └── DevInsight.Tests.Unit/      # Unit tests (xUnit)
├── Dockerfile
├── docker-compose.yml              # Full stack (API + Postgres + Redis + Elasticsearch)
├── docker-compose.minimal.yml      # API + Postgres only
└── .github/workflows/ci-cd.yml    # GitHub Actions pipeline
```

**Clean Architecture** with 4 layers:
- **Domain** — Entities, enums, repository interfaces. Zero dependencies.
- **Application** — CQRS via MediatR. DTOs, command/query handlers.
- **Infrastructure** — EF Core (PostgreSQL/SQLite), GitHub API client, AES encryption, JWT, Hangfire jobs.
- **API** — Controllers, authentication, Swagger, middleware.

## Features

### Implemented
- **GitHub Integration** — Connect via personal access token, sync repos, commits, and pull requests
- **Authentication** — JWT-based register/login with BCrypt password hashing
- **Multi-tenancy** — Organization-scoped data isolation
- **Secure Token Storage** — AES-256-CBC encrypted API tokens
- **Background Jobs** — Hangfire recurring job syncs all GitHub data hourly
- **Analytics Endpoints**:
  - Per-developer activity stats
  - Team velocity metrics
  - Daily commit/PR trends
  - PR cycle time / lead time analysis
  - Bottleneck detection (stale PRs, large PRs, inactive devs)
- **Structured Logging** — Serilog with console sink
- **API Documentation** — Swagger/OpenAPI at `/swagger`
- **Hangfire Dashboard** — Job monitoring at `/hangfire`

### Planned
- GitLab integration
- Jira integration
- GitHub webhook support
- Redis caching
- Elasticsearch for search/logs
- Basic anomaly detection
- Frontend dashboard

## API Endpoints

### Auth
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Register user + org |
| POST | `/api/auth/login` | Login, returns JWT |

### Integrations
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/integrations/github` | Store GitHub PAT |
| GET | `/api/integrations` | List integrations |

### Sync
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/sync/repos` | Sync repos from GitHub |
| GET | `/api/sync/repos` | List synced repos |
| POST | `/api/sync/commits/{repoId}` | Sync commits for repo |
| POST | `/api/sync/pull-requests/{repoId}` | Sync PRs for repo |

### Analytics
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/analytics/developers/{repoId}?from=&to=` | Per-dev stats |
| GET | `/api/analytics/velocity/{repoId}?from=&to=` | Team velocity |
| GET | `/api/analytics/trends/{repoId}?from=&to=` | Daily trends |
| GET | `/api/analytics/cycle-time/{repoId}?from=&to=` | PR cycle times |
| GET | `/api/analytics/bottlenecks/{repoId}` | Bottleneck detection |

### Metrics
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/metrics/repository/{repoId}?from=&to=` | Repository metrics |

### Health
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/health` | Health check |

## Setup

### Local (Windows — no Docker)

```bash
# 1. Install .NET 8 SDK (or use standalone install script)
# 2. Restore and build
dotnet restore
dotnet build

# 3. Run (uses SQLite by default)
dotnet run --project src/DevInsight.API

# 4. Open Swagger UI
# http://localhost:5000/swagger
```

### Docker

```bash
# Full stack (Postgres + Redis + API)
docker-compose up --build

# Minimal (Postgres + API only)
docker-compose -f docker-compose.minimal.yml up --build

# With Elasticsearch
docker-compose --profile full up --build
```

### Quick Test Flow

```bash
# 1. Register
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"dev@test.com","password":"123456","displayName":"Dev","organizationName":"MyOrg"}'

# 2. Use the returned token
TOKEN="<jwt_token_from_step_1>"

# 3. Add GitHub integration
curl -X POST http://localhost:5000/api/integrations/github \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"personalAccessToken":"ghp_your_token"}'

# 4. Sync repos
curl -X POST http://localhost:5000/api/sync/repos \
  -H "Authorization: Bearer $TOKEN"

# 5. List repos (get a repo ID)
curl http://localhost:5000/api/sync/repos \
  -H "Authorization: Bearer $TOKEN"

# 6. Sync commits & PRs for a repo
curl -X POST http://localhost:5000/api/sync/commits/{repoId} \
  -H "Authorization: Bearer $TOKEN"
curl -X POST http://localhost:5000/api/sync/pull-requests/{repoId} \
  -H "Authorization: Bearer $TOKEN"

# 7. View analytics
curl "http://localhost:5000/api/analytics/developers/{repoId}?from=2024-01-01&to=2026-12-31" \
  -H "Authorization: Bearer $TOKEN"
```

## Security

- JWT authentication with configurable secret, issuer, audience
- BCrypt password hashing
- AES-256-CBC encrypted API tokens
- Organization-scoped data isolation (multi-tenant)
- **Change all secrets in production!**

## Tech Stack

- **ASP.NET Core 8** — Web API
- **Entity Framework Core 8** — PostgreSQL / SQLite
- **MediatR** — CQRS pattern
- **Hangfire** — Background job scheduling
- **Serilog** — Structured logging
- **BCrypt.Net** — Password hashing
- **xUnit** — Testing

## Future Improvements

- GitLab & Jira integrations
- GitHub webhook support for real-time updates
- Redis caching layer for analytics
- Elasticsearch for full-text search and log aggregation
- Advanced anomaly detection (unusual commit patterns)
- React/Next.js frontend dashboard
- Deployment to Render / Railway / Azure free tier

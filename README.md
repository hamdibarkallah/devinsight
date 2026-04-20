# DevInsight – Developer Productivity Tracker

A scalable SaaS-style platform that integrates with Git providers (GitHub, GitLab) to analyze developer productivity and provide actionable insights. Features a modern Angular UI for real-time analytics.

## Architecture

```
DevInsight/
├── src/
│   ├── DevInsight.Domain/           # Entities, enums, interfaces (no dependencies)
│   ├── DevInsight.Application/      # CQRS commands/queries, DTOs, service interfaces
│   ├── DevInsight.Infrastructure/   # EF Core, GitHub client, encryption, Hangfire jobs
│   └── DevInsight.API/             # ASP.NET Core Web API, controllers, auth
├── devinsight-ui/                  # Angular 18 standalone UI
│   ├── src/app/
│   │   ├── pages/                  # Dashboard, login pages
│   │   ├── services/               # API, auth services
│   │   ├── guards/                 # Auth guard
│   │   └── interceptors/           # JWT interceptor
│   └── proxy.conf.json             # Dev server proxy to API
├── tests/
│   └── DevInsight.Tests.Unit/      # Unit tests (xUnit)
├── Dockerfile
├── docker-compose.yml              # Full stack (API + Postgres + Redis + Elasticsearch)
├── docker-compose.minimal.yml      # API + Postgres only
└── .github/workflows/ci-cd.yml    # GitHub Actions pipeline (manual trigger)
```

**Clean Architecture** with 4 layers:
- **Domain** — Entities, enums, repository interfaces. Zero dependencies.
- **Application** — CQRS via MediatR. DTOs, command/query handlers.
- **Infrastructure** — EF Core (PostgreSQL/SQLite), GitHub API client, AES encryption, JWT, Hangfire jobs.
- **API** — Controllers, authentication, Swagger, middleware.
- **UI** — Angular 18 standalone components, dark theme, responsive design.

## Features

### Implemented
- **GitHub Integration** — Connect via personal access token, sync repos, commits, and pull requests
- **Angular Dashboard UI** — Modern dark-themed interface with:
  - Repository selector with date range filtering
  - Commit activity chart with tooltips and peak highlighting
  - Developer stats table with velocity metrics
  - Anomaly detection panel
  - Real-time sync buttons
- **Authentication** — JWT-based register/login with BCrypt password hashing
- **Multi-tenancy** — Organization-scoped data isolation
- **Secure Token Storage** — AES-256 encrypted API tokens
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
- GitHub webhook support for real-time updates
- Redis caching
- Elasticsearch for search/logs
- Advanced anomaly detection

## Quick Start

### Prerequisites
- .NET 8 SDK
- Node.js 18+
- npm or yarn

### 1. Start the API

```bash
cd src/DevInsight.API
dotnet run
# API runs on http://localhost:5000
```

### 2. Start the Angular UI

```bash
cd devinsight-ui
npm install
npm run start
# UI runs on http://localhost:4200
```

### 3. Use the Dashboard

1. Open http://localhost:4200
2. Register a new account
3. Click "Connect GitHub" and paste your PAT
4. Click "⟳ Sync Repos" — all repos and commits sync automatically
5. Select a repository to view analytics

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
| PUT | `/api/integrations/github` | Update GitHub PAT |
| GET | `/api/integrations` | List integrations |

### Sync
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/sync/repos/{provider}` | Sync all repos + commits + PRs |
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

### Health
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/health` | Health check |

## Docker

```bash
# Full stack (Postgres + Redis + API)
docker-compose up --build

# Minimal (Postgres + API only)
docker-compose -f docker-compose.minimal.yml up --build

# With Elasticsearch
docker-compose --profile full up --build
```

## Security

- JWT authentication with configurable secret, issuer, audience
- BCrypt password hashing
- AES-256 encrypted API tokens
- Organization-scoped data isolation (multi-tenant)
- **Change all secrets in production!** See `.env.example` for required secrets.

## Environment Variables

Create `src/DevInsight.API/appsettings.local.json`:

```json
{
  "Jwt": {
    "Secret": "your-jwt-secret-min-32-chars"
  },
  "Encryption": {
    "Key": "your-32-byte-base64-encoded-aes-key"
  }
}
```

Generate AES key:
```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { [byte](Get-Random -Max 256) }))
```

## Tech Stack

- **ASP.NET Core 8** — Web API
- **Entity Framework Core 8** — PostgreSQL / SQLite
- **Angular 18** — Standalone UI with modern components
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
- Deployment to Render / Railway / Azure free tier

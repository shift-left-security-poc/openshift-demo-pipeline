# .NET 10 Blog API on OpenShift - Design

## Scope

Add a new backend service in this repository: a .NET 10 API for blog posts, persisted in PostgreSQL running inside OpenShift, secured with a static API key.

## Architecture

- New service at `backend/BlogApi` using .NET 10 minimal API.
- API endpoints:
  - `GET /health`
  - `GET /api/posts`
  - `GET /api/posts/{id}`
  - `POST /api/posts`
  - `PUT /api/posts/{id}`
  - `DELETE /api/posts/{id}`
- API key auth via `X-API-Key` header:
  - Required for all `/api/posts*` endpoints.
  - Health endpoint remains unauthenticated.
- Persistence:
  - EF Core + Npgsql provider.
  - PostgreSQL database in OpenShift `devops` namespace.

## Data Model

`Post` entity:
- `Id` (GUID, primary key)
- `Title` (required)
- `Content` (required)
- `Author` (required)
- `IsPublished` (bool)
- `CreatedAtUtc` (UTC datetime)
- `UpdatedAtUtc` (UTC datetime)

## Deployment Design (OpenShift + Helm)

Extend existing repo deployment assets to include:

- Backend resources:
  - DeploymentConfig
  - Service
  - Route
  - Secret for API key and DB connection string
- PostgreSQL resources:
  - Deployment (or DeploymentConfig if matching existing repo style)
  - Service
  - PersistentVolumeClaim
  - Secret for DB username/password/database
- Helm values:
  - Backend image repo/tag, route host, replicas
  - API key value (or external secret reference)
  - PostgreSQL storage size and credentials

## Data Flow

1. Client calls backend endpoint.
2. Middleware validates `X-API-Key`.
3. Endpoint validates request payload.
4. EF Core executes operation against PostgreSQL service.
5. API returns explicit HTTP status and payload.

## Error Handling and Security

- `401 Unauthorized`: missing/invalid API key.
- `400 Bad Request`: invalid payload.
- `404 Not Found`: post not found.
- No secrets in code or logs; all secret values sourced from OpenShift secrets.

## Testing and Verification

- Unit/integration tests for:
  - API key middleware behavior.
  - CRUD endpoint behavior and validation.
- Build/test commands documented and runnable from repo.
- Deployment instructions for OpenShift included in docs once implementation is complete.

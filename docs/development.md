# Development guide

## Prerequisites

Development and validation are container-based. Use Docker Desktop or another Docker engine capable of building Linux containers. The repository does not require a host installation of .NET or Node.js for the documented workflow.

## Repository map

```text
AiAgileBoard.slnx                         .NET solution
Directory.Build.props                    shared strict .NET compiler settings
Dockerfile                               frontend/backend build, test, and runtime stages
src/AiAgileBoard.Api/                    ASP.NET Core application
  Api/                                   HTTP endpoint mapping and transport contracts
  Application/                           ticket use cases and validation
  Domain/                                current entities and assignee enum
  Data/                                  EF Core configuration and migrations
src/AiAgileBoard.Client/                 React/Vite application
  src/pages/                             route-level ticket screens and tests
  src/modals/                            reusable ticket creation workflow
tests/AiAgileBoard.IntegrationTests/     API and persistence integration tests
tests/AiAgileBoard.UnitTests/            backend unit-test scaffold
tests/end-to-end/                        browser-test placeholder
packages/                                future generated client placeholders
docs/                                    maintained project documentation
```

## Build and run

Build the complete production image:

```sh
docker build -t ai-agile-board .
```

Run it with loopback-only networking and durable application data:

```sh
docker run --rm -p 127.0.0.1:8080:8080 -v ai-agile-board-data:/app/data ai-agile-board
```

The application is available at `http://127.0.0.1:8080`; the health endpoint is `http://127.0.0.1:8080/api/v1/health`.

## Validation

The Dockerfile provides isolated validation targets using the same toolchains as the production build.

Run backend compilation and tests:

```sh
docker build --target backend-test -t ai-agile-board-backend-test .
```

Run frontend compilation, lint, and component tests:

```sh
docker build --target frontend-test -t ai-agile-board-frontend-test .
```

A normal final-stage build also executes the frontend production build and the backend publish, but it does not execute either test target.

## Database changes

The application uses EF Core migrations. Schema changes should include a new migration under `src/AiAgileBoard.Api/Data/Migrations`; do not edit a migration that may already have been applied to user data. On startup, the application creates the configured database directory and applies all pending migrations.

The production connection string is injected by the image and points to `/app/data/aiagileboard.db`. Mount `/app/data` to persistent storage during normal use.

## Adding application behavior

Keep the existing dependency direction:

```text
HTTP/UI concerns -> application use cases -> domain model
                                      \-> data access through the configured DbContext
```

- Put route mapping, request/response shapes, and HTTP status decisions in `Api/`.
- Put validation and use-case orchestration in `Application/`.
- Keep workflow concepts and business rules in `Domain/` and independent of ASP.NET Core.
- Put EF Core mappings, persistence configuration, and migrations in `Data/`.
- Keep route-level React components in `src/pages` and reusable modal workflows in `src/modals`.

## Branch and quality workflow

Repository guidance requires work on a new branch created from `development`; do not work directly on `main` or `development`. Before completing a change:

1. Run backend tests.
2. Run frontend lint, tests, and build.
3. Update documentation when behavior, API contracts, state names, storage, or operating instructions change.
4. Prefix any commit message with the agent name when an agent creates the commit.
5. Prefer a pull request targeting `development` when repository access permits it.

## Continuous integration

`.github/workflows/ci.yml` runs backend restore/build/test and frontend install/lint/test/build for pull requests into `development`. It also runs on pushes to `main` and `development`.

## Current constraints

- Do not introduce a dependency when the existing stack can reasonably provide the behavior.
- Do not install development software directly on the host for this project.
- The API has no authentication and should be treated as local prototype software.
- Database migrations run automatically; back up the Docker volume before testing destructive schema work.

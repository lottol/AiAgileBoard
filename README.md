# AI Agile Board

AI Agile Board is a local-first work board for collaboration between people and AI coding agents. The current prototype supports a persistent ticket list, ticket creation, and ticket editing through a React interface and a versioned ASP.NET Core API.

> The repository is an early prototype. Agent authentication, claiming and leases, review enforcement, activity history, multi-board management, generated SDKs, legacy import/export, and notifications are planned but are not implemented yet.

## What works today

- Launch the portable Windows desktop app with bundled .NET and WebView2 runtimes.
- Create and open self-contained `.aiab` projects on Windows, with board data, saved preferences, autosave, and recovery. See [project files](docs/project-files.md).
- View all tickets and summary counts for human work, agent work, and completed work.
- Create a ticket with a title, description, workflow state, human or agent assignee, story points, and an optional initial note.
- Open a ticket detail page and edit its title, description, state, assignee, and story points.
- Persist tickets and comments in SQLite across container restarts when a Docker volume is used.
- Access health and ticket create/read/update operations under `/api/v1`.
- Build and run the frontend and backend as one production container.
- Validate the prototype with backend integration tests and frontend component tests.

## Run on Windows

The primary desktop delivery is a portable Windows ZIP. Extract the complete package and double-click `AiAgileBoard.exe`. See [Windows desktop instructions](docs/windows-desktop.md) for downloads/builds, storage, updates, and validation. The following Docker workflow remains available for development.

## Run in Docker

From the repository root:

```sh
docker build -t ai-agile-board .
docker run --rm -p 127.0.0.1:8080:8080 -v ai-agile-board-data:/app/data ai-agile-board
```

Open `http://127.0.0.1:8080`. The named volume stores the SQLite database at `/app/data/aiagileboard.db` so tickets survive container replacement.

## Documentation

- [Documentation index](docs/README.md) — current scope and links to all project documentation
- [User guide](docs/user-guide/README.md) — run the application and use the ticket workflow
- [API reference](docs/agent-api/README.md) — currently implemented HTTP endpoints and payloads
- [Current architecture](docs/architecture/current-implementation.md) — runtime components, request flow, and data model
- [Development guide](docs/development.md) — repository layout, Docker validation, and contribution workflow
- [Project plan](PROJECT_PLAN.md) — product vision, target architecture, and future backlog
- [ADR 0001](docs/architecture/0001-application-topology.md) — accepted application-topology decision

## Repository layout

```text
src/AiAgileBoard.Api/       ASP.NET Core API, application service, domain model, and SQLite data layer
src/AiAgileBoard.Client/    React, TypeScript, and Vite browser client
tests/                      .NET integration/unit test projects and the end-to-end placeholder
packages/                   Placeholders for future generated TypeScript and Python clients
docs/                       User, API, architecture, and development documentation
```

The intended MVP is broader than the current prototype. See the [current status](docs/README.md#implementation-status) before relying on a capability described in the project plan.

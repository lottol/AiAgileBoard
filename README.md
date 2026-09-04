# AI Agile Board

A local-first agile board where people and coding agents collaborate through explicit, auditable tickets.

## Repository layout

- `src/AiAgileBoard.Api` — ASP.NET Core API and production host
- `src/AiAgileBoard.Client` — React, TypeScript, and Vite client
- `packages` — generated TypeScript client and Python agent SDK
- `tests` — backend unit/integration tests and browser workflows
- `docs` — architecture, agent API, and user documentation

## Run in Docker

```sh
docker build -t ai-agile-board .
docker run --rm -p 127.0.0.1:8080:8080 -v ai-agile-board-data:/app/data ai-agile-board
```

Open `http://127.0.0.1:8080`. See [PROJECT_PLAN.md](PROJECT_PLAN.md) for the product scope and architecture.

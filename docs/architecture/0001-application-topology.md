# ADR 0001: Local web application topology

- Status: Delivery model superseded by [ADR 0002](0002-windows-desktop.md); API and frontend boundaries retained.
- Date: 2026-09-03

## Context

AI Agile Board needs an independently testable browser client and API while remaining simple to operate as a local application.

## Decision

Use a React/TypeScript/Vite client and a single ASP.NET Core API project with internal API, application, domain, and data boundaries. Production client assets are copied into the API's `wwwroot`, and the API binds to loopback by default outside its container boundary.

SQLite provides local persistence through Entity Framework Core and versioned migrations. Agent integrations will communicate only through the versioned `/api/v1` surface when the agent lifecycle is implemented; the current `/api/v1` surface contains only health and generic ticket operations.

## Consequences

Frontend and backend development stay independent while production remains a single process. Domain code must not depend on ASP.NET Core or persistence concerns. A future split into class libraries can preserve the existing namespaces and boundaries.

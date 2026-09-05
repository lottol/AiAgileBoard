# Documentation

This documentation describes both the working prototype and the direction captured in the project plan. Unless a page explicitly says otherwise, the documentation under this directory describes the current implementation.

## Start here

- [Windows desktop](windows-desktop.md) explains portable executable delivery, storage, and updates.
- [Project files](project-files.md) describes self-contained `.aiab` archives, saved settings, autosave, and recovery.
- [User guide](user-guide/README.md) explains how to run the application and manage tickets.
- [API reference](agent-api/README.md) documents the HTTP operations available today.
- [Current architecture](architecture/current-implementation.md) explains how the browser, API, application service, and SQLite database fit together.
- [Development guide](development.md) covers the repository, Docker build targets, tests, and contribution rules.
- [Project plan](../PROJECT_PLAN.md) describes the intended MVP and longer-term design.

## Implementation status

Last reviewed: 2026-09-05.

| Area | Current state |
| --- | --- |
| Ticket list | Implemented. Loads every ticket and displays status, assignee, story points, and summary counts. |
| Ticket creation | Implemented. Supports required title/description, state, assignee, non-negative story points, and one optional initial comment. |
| Ticket detail/editing | Implemented. Supports reading one ticket and updating its title, description, state, assignee, and story points. Existing comments are read-only. |
| Persistence | Implemented with EF Core, SQLite, and an initial migration. The schema and workflow states are created automatically at startup. |
| Production hosting | Portable Windows ZIP with WPF, bundled WebView2, self-contained .NET, and an in-process API. Docker remains available for development. |
| Automated checks | Backend health and ticket integration tests plus frontend list/detail component tests are implemented. The backend unit project is currently only a scaffold. |
| Projects and boards | Desktop supports creating and opening one `.aiab` project at a time, containing board data and saved preferences. Multi-board management is not implemented. |
| Ticket deletion/archive, filtering, search, and ranking | Not implemented. |
| Workflow transition rules | Not implemented. Any known state may currently be selected directly. |
| Agent API lifecycle | Not implemented. There are no discovery filters, claims, leases, heartbeats, progress, release, block, or submit-for-review operations. |
| Authentication and token scopes | Not implemented. Current API endpoints are unauthenticated. |
| Audit/activity history | Not implemented. |
| Comment creation after ticket submission | Not implemented. |
| OpenAPI and generated clients | Not implemented; `packages/` contains placeholders only. |
| Events, notifications, import/export, and backups | Desktop project autosave retains the previous archive and supports crash recovery. Legacy import, events, and notifications are not implemented. Docker volume persistence remains available. |
| Desktop end-to-end tests | Windows UI Automation validates packaged startup, ticket creation/editing, navigation, persistence, runtime selection, and shutdown. |

## Current workflow states

The database seeds the following display names. API requests must use them exactly as shown.

| State | `humanNeeded` | Dashboard grouping |
| --- | ---: | --- |
| Backlog | `true` | Needs you |
| Ready for Human | `true` | Needs you |
| Human In Progress | `true` | Needs you |
| Waiting for Agent | `false` | With agents |
| Agent In Progress | `false` | With agents |
| Human Review | `true` | Needs you |
| Changes Requested | `false` | With agents |
| Blocked | `true` | Needs you |
| Done | `false` | Completed |
| Canceled | `false` | Excluded from active summary counts |

`humanNeeded` is stored on the state record and is independent of the ticket's `assignee`. The prototype does not yet enforce consistency between these values or the transition rules proposed in the project plan.

## Documentation boundaries

The [project plan](../PROJECT_PLAN.md) is the source for product intent and future architecture. The API reference and current architecture pages are the sources for implemented behavior. This distinction is important because the project plan already describes several endpoints and security controls that do not exist in the prototype.

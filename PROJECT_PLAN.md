# AI Agile Board — Project Plan

## 1. Vision

AI Agile Board is a local-first agile work-management application where humans and AI coding agents collaborate through explicit, auditable tickets. It should feel familiar to users of Jira or Trello, while treating AI agents as first-class workers that can discover, claim, update, and complete work without losing human control.

The initial product will be a locally run web application. A .NET service will host the React interface and expose a documented API that approved agents can use to connect to a board. Packaging it as a downloadable standalone application is intentionally deferred until the core workflow is proven.

## 2. Product principles

- **Human control:** humans decide what agents may access and approve work before it is considered complete when review is required.
- **Explicit ownership:** a ticket always identifies whether the next action belongs to a human or an agent.
- **Safe claiming:** only one worker can hold a ticket lease at a time; stale leases expire safely.
- **Local first:** a single user can install and use the application without creating an account or running external infrastructure.
- **Auditable work:** claims, status changes, comments, artifacts, and approvals are recorded in an immutable activity history.
- **Open integration:** agents use a versioned API and do not depend on the web UI implementation.
- **Portable data:** users can export and back up their boards in documented formats.

## 3. Target users and primary workflows

### Human project owner

1. Creates a project and board.
2. Adds and prioritizes tickets with acceptance criteria.
3. Assigns a ticket to a human or places it in the agent queue.
4. Reviews agent output, requests changes, or accepts the work.
5. Tracks progress and reads a complete activity log.

### AI agent

1. Authenticates with a scoped token.
2. Lists eligible tickets in the agent queue.
3. Atomically claims one ticket and receives a time-limited lease.
4. Posts progress, comments, links, and artifacts while renewing its lease.
5. Submits the ticket for human review, releases it, or marks it blocked with a reason.

### Human contributor

1. Selects a human-ready ticket.
2. Moves it into human work.
3. Updates the ticket and marks it done or ready for review.

## 4. Ticket workflow

The workflow uses machine-readable status keys and user-friendly labels. Ownership is derived from status and is not inferred from free-form text.

| Status key | UI label | Next actor | Meaning |
| --- | --- | --- | --- |
| `BACKLOG` | Backlog | Human | Ticket is captured but not yet ready to start. |
| `READY_FOR_HUMAN` | Ready for Human | Human | Ticket is refined and available for a person. |
| `HUMAN_IN_PROGRESS` | Human In Progress | Human | A person is actively working on the ticket. |
| `WAITING_FOR_AGENT` | Waiting for Agent | Agent | Ticket is refined and eligible for an agent to claim. |
| `AGENT_IN_PROGRESS` | Agent In Progress | Agent | An agent owns the active lease and is working. |
| `WAITING_FOR_HUMAN_REVIEW` | Human Review | Human | Agent work is complete enough for a person to inspect. |
| `CHANGES_REQUESTED` | Changes Requested | Agent or Human | Review found required changes; the reviewer explicitly chooses the next assignee type. |
| `BLOCKED` | Blocked | Human | Work cannot continue until a recorded blocker is resolved. |
| `DONE` | Done | None | Acceptance criteria have been satisfied. |
| `CANCELED` | Canceled | None | Ticket will not be completed. |

### Required transition rules

- Only a human may move work from `BACKLOG` into either ready queue.
- An agent claims a `WAITING_FOR_AGENT` ticket through an atomic claim operation. A successful claim changes it to `AGENT_IN_PROGRESS` and creates a lease.
- Only the lease owner may update agent progress or submit an `AGENT_IN_PROGRESS` ticket for review.
- Agent-completed work moves to `WAITING_FOR_HUMAN_REVIEW`, not directly to `DONE`, unless the project explicitly enables trusted-agent auto-approval.
- Only a human reviewer may accept work into `DONE` or move it to `CHANGES_REQUESTED`.
- Returning `CHANGES_REQUESTED` work to an agent creates a fresh claim opportunity in `WAITING_FOR_AGENT`; returning it to a person moves it to `READY_FOR_HUMAN`.
- Moving a ticket to `BLOCKED` requires a blocker reason and records the prior status. Unblocking requires choosing a valid destination status.
- Every transition records actor, timestamp, old status, new status, and optional reason.
- Destructive deletion is avoided: tickets are archived or canceled so their history remains available.

### Claim and lease behavior

- Claims are transactional: simultaneous claim attempts result in exactly one winner.
- A claim stores `agent_id`, `lease_id`, `claimed_at`, `expires_at`, and last heartbeat.
- The default lease lasts 15 minutes and may be renewed while work is active.
- When a lease expires, the ticket returns to `WAITING_FOR_AGENT` and the event is logged. The previous agent may no longer mutate it using the expired lease.
- Agents may voluntarily release a claim with a reason.
- A human may revoke a lease; revocation is visible in the audit trail.

## 5. Ticket data model

Every ticket should include:

- Stable ID and human-readable project key, such as `AAB-42`
- Title and Markdown description
- Status and status-derived next actor
- Priority, rank, labels, and optional milestone/sprint
- Acceptance criteria as a checklist
- Human assignee and/or requested agent capability labels
- Active agent lease, when applicable
- Dependencies and blocking relationships
- Comments and progress updates
- Attachments and external links
- Agent result summary, validation evidence, and artifact links
- Created, updated, started, submitted, reviewed, and completed timestamps
- Creator, transition actors, reviewer, and full activity history
- Optimistic concurrency version to prevent silent overwrites

Core entities are `Project`, `Board`, `Column`, `Ticket`, `User`, `Agent`, `ApiToken`, `Lease`, `Comment`, `Attachment`, `ActivityEvent`, `Sprint`, and `Label`.

## 6. Minimum viable product

### Web application experience

- Create, rename, archive, export, and import projects.
- Kanban board with configurable columns mapped to workflow statuses.
- Create, edit, rank, filter, search, and archive tickets.
- Ticket detail view with acceptance criteria, comments, dependencies, activity, and agent information.
- Clear visual distinction between human-owned, agent-waiting, agent-active, review, blocked, and terminal work.
- Human review actions: approve, request changes, reassign, revoke lease, and resolve blocker.
- Settings for local API address, startup behavior, lease duration, token management, and backups.
- Notifications for review requests, blockers, failed agent actions, and expired leases.

### Agent integration

- Versioned REST API at `/api/v1` with JSON payloads.
- WebSocket or Server-Sent Events stream for ticket and lease events.
- Scoped bearer tokens generated and revoked in the web application.
- Capability-aware ticket discovery and claim endpoint.
- Endpoints to heartbeat, post progress, add comments, attach artifact metadata, release, block, and submit for review.
- Machine-readable errors, idempotency keys for mutating requests, and optimistic concurrency.
- OpenAPI specification plus example clients for TypeScript and Python.
- Optional MCP server adapter after the core API is stable, allowing compatible agents to use board operations as tools.

### Initial API surface

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/v1/projects` | List projects visible to the token. |
| `GET` | `/api/v1/projects/{projectId}/tickets` | Query eligible tickets by status, priority, and capability. |
| `GET` | `/api/v1/tickets/{ticketId}` | Read ticket details and acceptance criteria. |
| `POST` | `/api/v1/tickets/{ticketId}/claim` | Atomically claim a waiting ticket. |
| `POST` | `/api/v1/leases/{leaseId}/heartbeat` | Extend an active lease. |
| `POST` | `/api/v1/tickets/{ticketId}/progress` | Record progress for the active lease. |
| `POST` | `/api/v1/tickets/{ticketId}/comments` | Add a ticket comment. |
| `POST` | `/api/v1/tickets/{ticketId}/block` | Record a blocker and stop active work. |
| `POST` | `/api/v1/tickets/{ticketId}/release` | Release a claim back to the queue. |
| `POST` | `/api/v1/tickets/{ticketId}/submit` | Submit results and evidence for human review. |
| `GET` | `/api/v1/events` | Subscribe to authorized board events. |

Agents must never receive filesystem, repository, or shell access merely by connecting to the board. Tickets describe work; execution permissions remain the responsibility of the agent's own runtime.

## 7. Proposed technical architecture

The initial implementation should use:

- **Frontend:** React, TypeScript, Vite, and an accessible component system.
- **Backend:** ASP.NET Core Web API on the current supported .NET LTS release.
- **Application structure:** two sibling projects: a React/Vite frontend and one ASP.NET Core backend organized into focused folders for API, application logic, domain rules, and data access.
- **Data layer:** Entity Framework Core with SQLite, versioned migrations, foreign keys, WAL mode, and transactional claim operations.
- **Local hosting:** ASP.NET Core serves the API and production frontend assets, bound to `127.0.0.1` by default.
- **Contracts:** OpenAPI-generated types shared with the UI and client SDKs.
- **Testing:** xUnit for .NET unit/integration tests, Vitest and React Testing Library for frontend logic, and Playwright for critical workflows.

The frontend and backend will be separate projects so they can be developed, tested, and built independently. The backend will initially use one `.csproj`; folder and namespace boundaries should keep domain rules independent of ASP.NET Core and Entity Framework Core. If the backend outgrows this structure, its folders can later be extracted into class-library projects without changing the domain model. Production builds will copy the compiled frontend into the backend's `wwwroot` directory so the application can still run as one deployment.

### Suggested repository layout

```text
/
├── src/
│   ├── AiAgileBoard.Api/
│   │   ├── AiAgileBoard.Api.csproj   # Single ASP.NET Core backend project
│   │   ├── Program.cs                # Application entry point and composition
│   │   ├── Api/                      # Endpoints, contracts, and HTTP concerns
│   │   ├── Application/              # Use cases and application services
│   │   ├── Domain/                   # Ticket state machine and business rules
│   │   ├── Data/                     # EF Core, SQLite, and migrations
│   │   └── wwwroot/                  # Compiled frontend assets in production
│   └── AiAgileBoard.Client/          # React, TypeScript, and Vite project
│       ├── package.json
│       ├── vite.config.ts
│       └── src/
├── packages/
│   ├── api-client-ts/        # Generated TypeScript client
│   └── agent-sdk-python/     # Small Python agent client
├── docs/
│   ├── architecture/
│   ├── agent-api/
│   └── user-guide/
├── tests/
│   ├── AiAgileBoard.UnitTests/
│   ├── AiAgileBoard.IntegrationTests/
│   └── end-to-end/
└── .github/workflows/        # CI and quality checks
```

## 8. Security and trust boundaries

- Bind the API to loopback only by default. Listening on a LAN interface requires an explicit warning and opt-in.
- Store tokens hashed at rest; display a raw token only once at creation.
- Scope tokens by project and action (`read`, `claim`, `update`, `submit`).
- Never put tokens in URLs or activity logs; redact secrets from diagnostics.
- Validate attachment size/type and store files under an application-managed data directory.
- Require user confirmation before opening agent-provided external links or files.
- Treat all ticket and agent text as untrusted content and render Markdown without executable HTML.
- Use schema validation, request-size limits, rate limits, and parameterized database queries.
- Preserve an append-only activity log for security-sensitive actions.
- Define backup, restore, database migration, and recovery tests before stable release.
- Add network authentication and TLS before supporting remote/team connections.

## 9. Quality strategy

- Unit-test every permitted and forbidden status transition.
- Property-test claim exclusivity, lease expiry, and idempotent mutations.
- Integration-test database migrations, rollback behavior, API authorization, and concurrent claims.
- End-to-end test human ticket creation, agent lifecycle, review, change request, approval, backup, and restore.
- Set accessibility goals at WCAG 2.2 AA for keyboard use, focus, contrast, labels, and announcements.
- Measure board load and filtering with at least 10,000 tickets; define performance budgets from baseline results.
- Keep a threat model and perform dependency/license scanning in CI.

## 10. MVP success criteria

The MVP is successful when:

- A new user can start the locally hosted application and create their first board.
- Humans can immediately tell who or what must act next on every visible ticket.
- An authorized agent can safely discover and claim eligible work through documented APIs.
- Two agents cannot own the same ticket concurrently.
- Agent work always reaches a visible human review gate by default.
- Every important action is attributable and auditable.
- Restart, upgrade, lease expiry, export, and restore do not lose ticket data.
- The primary human-agent workflow is covered by automated end-to-end tests.

## 11. Explicit non-goals for the first release

- Hosted SaaS or internet-exposed collaboration server
- Full Jira feature parity
- Built-in code execution or unrestricted shell access
- Autonomous approval of high-impact work by default
- Real-time multi-user editing across multiple machines
- Marketplace of third-party agent plugins
- Mobile applications
- Desktop installers, code signing, automatic updates, and app-store distribution

## 12. Key risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Agents race to claim the same work | Transactional unique active lease and concurrency tests. |
| An agent stalls indefinitely | Renewable leases with expiry, release, and human revocation. |
| Workflow becomes confusing | Fixed MVP state machine, derived next actor, and transition tests. |
| Agent output is unsafe or misleading | Mandatory human review by default, visible evidence, and untrusted-content handling. |
| Local API exposes local data | Loopback-only default, scoped hashed tokens, rate limits, and explicit LAN opt-in. |
| Schema upgrades corrupt user data | Versioned migrations, automatic pre-upgrade backup, and restore tests. |
| Scope expands toward full Jira parity | Enforce the MVP and non-goal lists; evaluate additions only after the core workflow is proven. |

## 13. First implementation backlog

1. Write an architecture decision record for the React frontend, .NET backend, and local-service topology.
2. Implement the ticket status enum, transition matrix, and next-actor derivation in the `Domain` folder.
3. Add exhaustive transition and authorization tests.
4. Design the SQLite schema, migration runner, and development seed data.
5. Scaffold the sibling ASP.NET Core API and React/Vite client projects.
6. Implement project and ticket CRUD through use cases in the `Application` folder.
7. Build the board and ticket detail views against real local storage.
8. Add append-only activity events for mutations.
9. Define `/api/v1` in OpenAPI and generate shared client types.
10. Implement token creation, hashing, scoping, and revocation.
11. Implement transactional claim, heartbeat, release, expiry, and revocation.
12. Implement progress, block, submit, review, and change-request flows.
13. Add backup/export, restore/import, and migration recovery tests.
14. Add end-to-end tests for the complete human-agent-review lifecycle.

## 14. Decisions to revisit after the prototype

- Whether events should use Server-Sent Events or WebSockets; prefer SSE unless bidirectional messaging proves necessary.
- Whether the optional MCP adapter ships in the initial release or immediately afterward.
- Whether attachments belong in the database or an application-managed content-addressed directory.
- Which desktop packaging technology, target operating systems, signing process, and update mechanism to adopt.
- Whether a future team server reuses the same .NET application core or becomes a separately deployed service.

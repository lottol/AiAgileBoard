# HTTP API reference

The prototype exposes a small JSON API under `/api/v1`. It supports health checks and human-oriented ticket create/read/update operations. It is not yet the agent lifecycle API described in the project plan.

## Important current constraints

- There is no authentication or authorization. Do not expose the prototype API to an untrusted network.
- There are no claim, lease, heartbeat, progress, release, block, review-submission, or event-stream endpoints.
- There is no OpenAPI document or generated TypeScript/Python client yet.
- Mutations do not support idempotency keys or optimistic concurrency.
- JSON property matching is case-insensitive. Enum values are represented as strings.
- State names are case-sensitive during application-level lookup and must match a seeded display name exactly.

Examples below assume the application is available at `http://127.0.0.1:8080`.

## Resource shape

Successful ticket reads and mutations return this shape:

```json
{
  "id": "95817b43-5922-4481-80f8-cd930061d2f6",
  "title": "Review agent handoff",
  "description": "Confirm the result and validation evidence.",
  "comments": [
    "Run the integration tests before approval."
  ],
  "storyPoints": 3,
  "state": "Human Review",
  "humanNeeded": true,
  "assignee": "Human"
}
```

`humanNeeded` is derived from the stored state. It does not necessarily match `assignee`, because that consistency rule is not yet enforced.

## Endpoints

| Method | Path | Success | Purpose |
| --- | --- | --- | --- |
| `GET` | `/api/v1/health` | `200 OK` | Check that the process can serve API requests. |
| `GET` | `/api/v1/tickets` | `200 OK` | Return all tickets, ordered by GUID. |
| `GET` | `/api/v1/tickets/{ticketId}` | `200 OK` | Return one ticket. |
| `POST` | `/api/v1/tickets` | `201 Created` | Create a ticket and optional initial comments. |
| `PUT` | `/api/v1/tickets/{ticketId}` | `200 OK` | Replace the editable fields of one ticket. |

### Health

```http
GET /api/v1/health
```

Response:

```json
{
  "status": "healthy"
}
```

This endpoint currently verifies request handling only; it does not execute a database health probe.

### List tickets

```http
GET /api/v1/tickets
```

Returns a JSON array of ticket resources. There are no query parameters, filters, or pagination. An empty board returns `[]`.

### Get a ticket

```http
GET /api/v1/tickets/95817b43-5922-4481-80f8-cd930061d2f6
```

Returns `404 Not Found` with no response body when the GUID is well-formed but no ticket exists. The route requires a GUID; behavior for other path shapes is outside this endpoint contract.

### Create a ticket

```http
POST /api/v1/tickets
Content-Type: application/json
```

Request:

```json
{
  "title": "Review agent handoff",
  "description": "Confirm the result and validation evidence.",
  "storyPoints": 3,
  "assignee": "Human",
  "stateId": 0,
  "state": {
    "name": "Human Review"
  },
  "comments": [
    {
      "body": "Run the integration tests before approval."
    }
  ]
}
```

Request rules:

- `title` and `description` are required, must contain non-whitespace text, and are trimmed before storage.
- The database limits `title` to 200 characters.
- `storyPoints` must be zero or greater.
- `assignee` must be `Human` or `Agent`.
- A state may be selected by a positive `stateId`, by `state.name`, or by both. Omitted/zero `stateId` with no name defaults to Backlog.
- When both state ID and name are supplied, they must refer to the same stored state.
- Blank comment bodies are discarded. Nonblank bodies are trimmed and assigned server-generated IDs.
- Client-supplied ticket, comment, and relationship IDs are replaced by the server.

On success, the API assigns the ticket ID and returns the ticket resource with a `Location` header pointing to `/api/v1/tickets/{id}`.

### Update a ticket

```http
PUT /api/v1/tickets/95817b43-5922-4481-80f8-cd930061d2f6
Content-Type: application/json
```

Request:

```json
{
  "title": "Approve agent handoff",
  "description": "The validation evidence is ready for review.",
  "storyPoints": 5,
  "state": "Done",
  "assignee": "Human"
}
```

All five properties are required as a complete editable-field representation; this is not a partial update. The same title, description, story-point, assignee, and state-name validation applies. Existing comments are retained and cannot be changed by this operation.

The API returns `404 Not Found` when the ticket does not exist.

## Validation errors

Application validation failures return `400 Bad Request` using ASP.NET Core validation-problem JSON. The response includes a `ticket` entry in `errors`, for example:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "ticket": [
      "The state 'Imaginary' does not exist. (Parameter 'ticket')"
    ]
  }
}
```

Malformed JSON or model-binding failures may use a different framework-generated problem response.

## Seeded states

Valid state names are `Backlog`, `Ready for Human`, `Human In Progress`, `Waiting for Agent`, `Agent In Progress`, `Human Review`, `Changes Requested`, `Blocked`, `Done`, and `Canceled`.

## Not-yet-implemented agent contract

The future agent contract in the [project plan](../../PROJECT_PLAN.md#agent-integration) includes scoped bearer tokens, atomic claiming, renewable leases, progress and comments, blocking/releasing work, review submission, and events. None of those planned endpoints or safety guarantees should be inferred from the current generic ticket API.

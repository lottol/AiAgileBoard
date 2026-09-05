# User guide

This guide covers the ticket workflow implemented in the current local prototype.

## Start the application

On Windows 11 x64, extract the entire portable ZIP to a writable folder and double-click `AiAgileBoard.exe`. Choose **New Project** or **Open Project** on the homepage. Your `.aiab` file contains board data and saved preferences, and changes autosave. See [project files](../project-files.md) for recovery and [Windows desktop instructions](../windows-desktop.md) for packaging and troubleshooting.

For the development Docker server, build and run from the repository root:

```sh
docker build -t ai-agile-board .
docker run --rm -p 127.0.0.1:8080:8080 -v ai-agile-board-data:/app/data ai-agile-board
```

Open `http://127.0.0.1:8080`. Keep the `ai-agile-board-data` volume to retain tickets when the container is stopped or replaced.

## Ticket dashboard

Once a project is open, the dashboard lists every ticket in its database. Each row shows the full ticket UUID, title, description, state, human or agent assignee, and story points. Select the ticket UUID to open its detail page.

The summary cards are calculated from the loaded tickets:

- **All tickets** counts every ticket.
- **Needs you** counts non-Done tickets whose state has `humanNeeded: true`.
- **With agents** counts non-Done, non-Canceled tickets whose state has `humanNeeded: false`.
- **Completed** counts tickets in Done.

On desktop, the project toolbar shows the filename and saving status. Use **Close Project** to return to the homepage before opening another project. Filters, search, sorting controls, and pagination are not implemented.

## Create a ticket

1. Select **Submit a ticket**. On an empty board, **Create first ticket** opens the same form.
2. Enter a title of at most 200 characters.
3. Enter a description.
4. Choose a workflow state and assignee.
5. Enter zero or more story points. The UI accepts values up to 100; the API enforces only that the value is not negative.
6. Optionally enter an initial note. It is stored as the ticket's first comment.
7. Select **Submit ticket**.

Changing the state in the creation form automatically suggests Human or AI agent based on the state's current `humanNeeded` grouping. You can override the assignee manually. The backend does not yet require the selected assignee to match the state.

On success, the modal closes, the new ticket appears in the list, and a short confirmation message is displayed. A submission error leaves the form open so it can be corrected and retried.

## View and edit a ticket

Open a ticket by selecting its UUID in the list. The detail page shows its editable properties and any initial comments.

You can edit:

- title;
- description;
- assignee;
- story points; and
- workflow state.

Select **Save changes** to persist all editable fields. Select **Reset changes** to return the form to the last successfully loaded or saved values. Existing comments are visible but cannot currently be added, changed, or deleted from this page.

## Workflow states

The prototype exposes all seeded states directly:

1. Backlog
2. Ready for Human
3. Human In Progress
4. Waiting for Agent
5. Agent In Progress
6. Human Review
7. Changes Requested
8. Blocked
9. Done
10. Canceled

There is not yet an enforced transition matrix or review gate. A user may move a ticket directly between any states through the detail form. The stricter rules described in the project plan are planned behavior, not current behavior.

## Errors and recovery

- If the dashboard cannot reach the API, refresh after checking that the container is still running.
- If a ticket URL contains an unknown UUID, the detail page offers a link back to all tickets.
- If saving fails, review required text, story points, state, and assignee, then retry.
- To check the backend independently, open `http://127.0.0.1:8080/api/v1/health`; a running service returns `{"status":"healthy"}`.

## Current limitations

The prototype does not yet support deleting or archiving tickets, adding later comments, managing projects or boards, authenticating users or agents, claiming work, enforcing review, recording activity history, importing/exporting data, or managing backups from the UI. See the [implementation status](../README.md#implementation-status) for the full current boundary.

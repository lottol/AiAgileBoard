## Project
AI Agent Integrated Agile Board

See PROJECT_PLAN.md for project vision and architecural guidance

## Development Environment
Do not allow any new software to be installed directly. If it absolutely needs new software, let me know.

Development will occur in Docker container. 

## Docker

Docker Desktop may be installed per-user and unavailable on the Codex execution shell's PATH.

Before concluding Docker is unavailable:

1. Try resolving `docker` normally.
2. Check these locations:
   - `$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin\docker.exe`
   - `$env:ProgramFiles\Docker\Docker\resources\bin\docker.exe`
3. If sandbox access prevents checking or executing the discovered CLI, retry with elevated sandbox permission.
4. Invoke the discovered executable directly; do not modify PATH.
5. Use the `backend-test` Dockerfile target for tests:
   `docker build --target backend-test -t ai-agile-board-backend-test .`

## GitHub CLI

- GitHub CLI (`gh`) is installed on the host, outside Docker.
- Assume the existing `gh` login may be valid.
- Never conclude that authentication is invalid from a command run with restricted network access.
- If a `gh` command fails with a socket, DNS, network, or apparent authentication error inside the sandbox, repeat the same read-only check with elevated sandbox/network permission before reporting the cause.
- Distinguish these failure types:
  1. Authentication: whether `gh auth status` succeeds with host network access.
  2. Authorization: whether the token has the scope required by the requested operation.
  3. Connectivity: whether sandbox restrictions prevented reaching GitHub.
- Report a missing token scope as a scope problem, not as expired or invalid authentication.
- Test the requested GitHub capability directly. For GitHub Projects, use:
  `gh project list --owner lottol --format json`
- Do not recommend installing another GitHub integration until the host `gh` CLI has been tested correctly.

## Allowed
- modify the dockerfile 
- modify the .gitignore
- modify any code files within project context

## Not Allowed
- working on the main branch or the development branch
- downloading anything
- messing with any PATH variables, all previews and testing must be done using Docker

## Architecture
- Prefer small, composable modules.
- Keep business logic separate from UI components.
- Avoid global mutable state.
- Follow the architecture documented in PROJECT_PLAN.md.

## Before Making Any Changes
1. Create new Git branch off of development branch.

## Before Making Major Changes
1. Read PROJECT_PLAN.md.
2. Inspect the existing architecture.
3. Prefer extending existing patterns over introducing new ones.
4. Explain significant architectural changes before implementing them.

## After Making Any Changes
1. Any commit messages will have The agent name prefixing any proper commit message created
2. If possible, please submit a code review by creating a pull request to merge it into development.
3. Tell me what branch was made regardless of whether or not a pull request was made

## Dependencies
Do not introduce a new dependency when the functionality can
reasonably be implemented using the existing stack.

When adding a dependency, explain why it is necessary.

## Quality
Before considering a task complete:
- Run type checking.
- Run linting.
- Run relevant tests.
- Fix errors introduced by the change.
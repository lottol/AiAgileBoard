## Project
AI Agent Integrated Agile Board

See PROJECT_PLAN.md for project vision and architecural guidance

## Development Environment
Do not allow any new software to be installed directly. If it absolutely needs new software, let me know.

Development will occur in Docker container. 

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
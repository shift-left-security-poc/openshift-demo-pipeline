# Jenkins Aikido CI Integration Design

Date: 2026-07-29
Target: jenkins/Jenkinsfile

## Goal
Add Aikido security gating to the frontend Jenkins pipeline before the build artifact is produced.

## Selected Approach
- Add a dedicated `Aikido security scan` stage after checkout and before build.
- Use ephemeral CLI execution with `npx -y @aikidosec/ci-api-client`.
- Use Jenkins Secret Text credentials and inject token at runtime only.
- Run `scan-release` using repository name and current commit SHA.

## Why
- Fail fast on security gate before spending build resources.
- Keep pipeline agent immutable (no global install requirement).
- Avoid storing tokens in source control.

## Security
- Token must be stored as Jenkins Secret Text credential with ID: `aikido-ci-api-key`.
- Pipeline references the secret with `withCredentials(...)` and never hardcodes it.

## Pipeline Impact
- Existing checkout, build, and binary build stages remain unchanged.
- New stage blocks pipeline when Aikido gate fails.

# PR 447 sixth review and CI repair plan

Starting head: `4487d988be36cea982ce66662924659296969051`.

## Observed failures

- CI job 108480924718 passed type checking, lint and tests, then failed in the production build. Turbopack reported 27 Roboto font import errors, including `next/font/google queries have exactly one entry`.
- Codex r4112695224: removing a vehicle group loses the deposit fallback for active reservations without a pricing snapshot.
- Codex r4112695229: step 3 omits the URL fallback for the selected exact vehicle when loading extras.
- Codex r4112695232: rental-rate priority accepts decimal steps despite an integer API contract.

## Changes and validation

1. Use the supported Next.js 16 `next build --webpack` production command, preserving fonts and the dependency lockfile. Validate the standard build command locally and inspect fresh CI after push.
2. Reject vehicle group changes while active snapshotless reservations depend on the current group. Preserve saved state on rejection and retain edits for snapshot-backed or terminal reservations. Cover statuses and group removal/replacement in unit tests and the authenticated real API path.
3. Resolve the exact vehicle from booking state or the preserved URL for both the extras request and cache key. Verify grouped/group-less URL recovery and store precedence.
4. Use integer steps for priority and decimal steps for monetary/multiplier fields. Verify browser validity behavior in component tests.
5. Run relevant backend and frontend tests, lint, production build, and whitespace checks; update the review evidence and handoff documents before committing and pushing to the existing PR.

## Scoped security review

Review deposit fallback preservation, server-side mutation guards and existing admin authorization, plus exact-vehicle identity propagation to the server-filtered extras catalogue. The build change does not alter workflow permissions, credentials or deployment triggers. No merge or deployment is included.

## Execution evidence

Implemented all four corrections. Local validation passed: 875 backend unit tests, 56 focused frontend tests, three real PostgreSQL/Redis vehicle-catalogue API tests, the standard production build with TypeScript, and lint with its existing single warning. The initial broader API/build/lint terminal results were not retrievable after the native terminal result channel stalled; no pass is claimed for that unobserved broader run. No matching validation processes remained active before the targeted API/build/lint reruns. Fresh remote CI is checked after the corrective push.

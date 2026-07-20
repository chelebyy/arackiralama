# Database Operations and Observability Implementation

**Status:** Final executable implementation plan; no implementation phase is complete; Production acceptance evidence pending

**Date:** 2026-07-19

**Depends on:** `docs/19_Database_Operations_and_Observability_Plan.md`

**Deployment model:** Greenfield Docker/Dokploy Production, PostgreSQL 18 single primary, local operational repository, and Backblaze B2 off-host recovery repository
**Software policy:** Free and open-source software only
**Validation model:** Local Docker first where feasible; mandatory complete Test VPS pass; explicitly labeled safe `PRODUCTION-ONLY` revalidation for live-environment claims

## 1. Objective

Implement the approved database operations and observability architecture without creating a custom monitoring, logging, alerting, backup, or database-maintenance engine.

The delivered system must provide:

- structured API, Worker, and Next.js server logs;
- centralized log collection, retention, and search;
- PostgreSQL, host, application, Worker, backup, and observability-pipeline metrics;
- actionable external alerts;
- encrypted off-host PostgreSQL backups with WAL/PITR and restore evidence;
- controlled all-path database-write quiescence before a B2 outage can violate the 15-minute RPO or exhaust `pg_wal`;
- a dedicated independent Vector audit relay and immutable authoritative audit sink;
- a read-only System and Operations area in the existing admin shell;
- explicit stale/untrusted behavior;
- credential, network, authorization, privacy, retention, and supply-chain controls.

No phase in this document authorizes web-triggered database maintenance.

## 2. Status Legend

- `[ ]` not started;
- `[~]` in progress or partially evidenced;
- `[x]` that individual decision or task is approved/completed with its required evidence;
- `[!]` blocked by an explicit decision, dependency, or failed gate.

A task is not complete because a file exists. It is complete only when its stated verification evidence passes. A phase is complete only when every required task and its phase exit gate pass; an `[x]` preflight decision does not imply implementation completion.

### 2.1 Environment and Evidence Legend

- `[LOCAL]`: mandatory Local Docker verification when the behavior can be reproduced without claiming a real external failure domain. Use real containers and synthetic data; fake HTTP sources are allowed only for deterministic negative paths.
- `[TEST-VPS]`: mandatory deployed verification on the existing non-trusted Staging/Dokploy VPS, which is the canonical Test VPS for this workstream. Every implemented phase must reach `TEST-VPS-PASS`.
- `[PRODUCTION-ONLY]`: safe live-environment evidence that cannot be truthfully established elsewhere. It runs only after all Local/Test VPS gates pass and never authorizes destructive Production fault injection.
- `[N/A-LOCAL]`: the Owner-Operator records why a real-provider/topology property cannot be proven locally; the corresponding Test VPS task remains mandatory.

Unless a checklist item is explicitly marked `PRODUCTION-ONLY`, it must be executed on the Test VPS and also in Local Docker wherever technically feasible. A phase exit gate requires both applicable `LOCAL-PASS` evidence and `TEST-VPS-PASS`. Test VPS evidence cannot be renamed or copied as Production evidence.

The normative environment/evidence contract is section 1.2 of `docs/19_Database_Operations_and_Observability_Plan.md`. This document operationalizes that contract. If wording diverges, execution stops, the plan is corrected first, and the cross-document environment/security-gap consistency check must pass before work resumes; duplicated phase labels never override the canonical contract.

Every evidence record contains environment, UTC timestamp, commit SHA, image digests, sanitized configuration hash, test/runbook ID, result, and evidence location. Secrets, customer records, and raw restored Production rows never enter evidence.

Evidence traceability uses stable identifiers that do not change when headings or file names are refined:

- phase exit gates use `DBOPS-P<n>-G<n>`;
- registered safe Production checks use `DBOPS-PROD-<nn>`;
- runbooks use `DBOPS-RB-<domain>-<nn>`;
- evidence runs use `DBOPS-EV-<LOCAL|TESTVPS|PROD>-<YYYYMMDDTHHMMSSZ>-<short-slug>` and reference every gate, test, and runbook they claim to satisfy.

`docs/19_Database_Operations_and_Observability_Plan.md` and this document remain the normative architecture and implementation contracts. `docs/10_Execution_Tracking.md` is the mutable execution ledger for phase status, environment-specific results, blockers, and evidence links. A checkbox in this document may change to `[x]` only when the matching ledger entry points to the required evidence; the ledger cannot weaken or override a gate.

Phase 0 creates `docs/test-evidence/database-operations/README.md` and `docs/test-evidence/database-operations/templates/validation-record.md`. Actual records live under `docs/test-evidence/database-operations/<environment>/<run-id>/`; they contain sanitized summaries and hashes/locations rather than copied secrets, customer rows, restored Production rows, or large raw logs.

### 2.2 Environment Execution Matrix

| Work type | Local Docker | Test VPS | Production |
|---|---|---|---|
| Build, unit, integration, UI, authorization, redaction | Required | Required rerun against deployed services | Safe release smoke only |
| Full observability/audit stack | Co-located functional stack | Complete deployed stack | Actual independent Operations VPS verification |
| Backup/PITR | Local encrypted repositories and synthetic restore | Real B2 test bucket/prefix and measured synthetic restore | Actual Production bucket/policy plus isolated restore from Production artifacts |
| Object Lock | Config and simulated failure only | Dedicated short-retention test buckets and negative delete | Actual 7-day backup and 3-year audit retention verification |
| WAL/B2/audit fault injection | Simulated outage, pressure, quiescence, resume | Required destructive fault matrix | No deliberate outage, disk fill, quota exhaustion, corruption, or customer-impact test |
| Network/host failure | Container/network/host simulation | Test VPS loss/isolation plus external receiver | Safe path isolation and actual failure-domain placement evidence only |
| Capacity/load | Synthetic bounded load | Measured Test VPS load and threshold tests | Actual live baseline, 30-day operational forecast, 7-day recovery-window peak, and 3-year immutable-audit forecast without forced exhaustion |

The Test VPS uses only synthetic data, test-only Keycloak realm/client/users, scoped test secrets, dedicated B2 test buckets/prefixes, and non-Production receiver routes. It holds no Production recovery authority, secrets, or customer backup data.

Destructive Test VPS execution also requires an exclusive maintenance window, recoverable infrastructure/config snapshot, stopped or relocated unrelated staging/demo workloads, bounded resource/timeout abort conditions, and evidence streamed to an off-host location before host-loss tests. A separate non-Production B2 account is preferred when provider terms/ownership permit. If an account is shared, tests must not change account-level cap/billing controls or allocate real threshold volumes; all test bytes/locked objects count in the combined 8 GB calculation.

Any `PRODUCTION-ONLY` restore of actual Production artifacts uses an encrypted ephemeral target with private ingress, default-deny egress, all notifications/payments/webhooks disabled, Owner-Operator-only access, fixed TTL, no row values in evidence, and verified cleanup plus volume destruction.

## 3. Change Boundaries

### 3.1 Approved Source Areas

Expected application changes are limited to:

- `backend/src/RentACar.API/Configuration/`;
- `backend/src/RentACar.API/Contracts/Operations/`;
- `backend/src/RentACar.API/Controllers/AdminOperationsController.cs`;
- `backend/src/RentACar.API/Middleware/RequestLoggingMiddleware.cs`;
- `backend/src/RentACar.API/Services/Operations/`;
- a single centralized database write-admission contract under the existing configuration/service conventions, with middleware as early rejection but not sole enforcement;
- one narrow infrastructure-owned write-safety watcher and atomic state schema under `ops/backup/`;
- the existing security-critical admin audit filter and its transactional-outbox seam;
- the minimum `RentACar.Infrastructure` persistence/migration files required for that transactional outbox;
- `backend/src/RentACar.Worker/`;
- corresponding backend test projects;
- `frontend/instrumentation.ts` or the Next.js 16 equivalent at project root;
- `frontend/app/(admin)/dashboard/(auth)/operations/`;
- `frontend/components/layout/sidebar/nav-main.tsx`;
- a small frontend operations client/hook following existing project conventions;
- corresponding Vitest/Testing Library tests;
- new `ops/observability/`, `ops/audit/`, and `ops/backup/` infrastructure configuration;
- `.env.example` placeholders only;
- canonical docs and test checklists.

### 3.2 Prohibited Changes

Implementation must not:

- add EvLog or `evlog-net`;
- add a second admin database;
- place metrics, raw logs, or backup manifests in `RentACarDbContext`;
- give the API or frontend PostgreSQL monitor/maintenance credentials;
- expose a shell-command endpoint;
- add maintenance, session-kill, or restore controls;
- add public Prometheus, Alertmanager, Loki, Grafana, exporter, pgAdmin, Docker API, or backup endpoints;
- commit real passwords, tokens, notification receiver secrets, encryption keys, or backup credentials;
- introduce a paid SaaS dependency or required commercial feature.

### 3.3 Technical-Debt Guardrails

- One centralized write-admission contract consumes the deployment-owned state before startup initialization and across API business writes, auth/session/account writes, internal callbacks, database mutation services, direct database-command paths, and Worker jobs; per-controller outage flags and middleware-only enforcement are prohibited.
- One local write-safety watcher is the only automatic actuator. It may atomically disable writes but has no enable, SQL, shell-command dispatch, retention, deletion, billing, or recovery capability; Alertmanager remains notification-only and manual resume is mandatory.
- One `IOperationsOverviewReader` owns overview normalization; controllers and pages do not contain provider queries, provider DTOs, status calculations, or retry logic.
- One bounded `IOperationsAuditWriter` contract fronts the dedicated Vector relay and returns only a transient transport submission outcome with no durability meaning. A separate bounded `IOperationsAuditReceiptReader` fronts the receiver-side B2 verifier and is the only application path that can produce `B2_DELIVERED`; there is no intermediate application durability state, and controllers use neither a B2 SDK nor Vector acknowledgement as delivery proof.
- Read-only overview audit is submitted to the external relay and verified through a B2-derived signed receipt without an application-database write. Security-critical business mutations replace fire-and-forget audit with a transactional outbox written atomically with the business change.
- Status/reason enums, audit schema, redaction rules, and freshness rules have one canonical definition and contract tests; stringly typed duplicates are prohibited.
- No placeholder `TODO`, `TBD`, disabled test, temporary bypass, mutable image tag, or compatibility shim may be counted as a completed phase gate.
- Every new abstraction must have at least two real consumers or isolate a named external boundary. Speculative repositories, generic command buses, and unused extension points are prohibited.

## 4. Target Repository Layout

The exact file names may change to match validated tool versions, but the ownership model must remain:

```text
ops/
├── README.md
├── runbooks/
│   ├── README.md
│   ├── monitoring-and-postgresql.md
│   ├── backup-wal-and-quiescence.md
│   ├── b2-object-lock-and-capacity.md
│   ├── restore-and-pitr.md
│   ├── audit-delivery-and-break-glass.md
│   ├── identity-credentials-and-owner-recovery.md
│   └── test-vps-fault-injection.md
├── observability/
│   ├── compose.yml
│   ├── README.md
│   ├── alloy/
│   │   └── config.alloy
│   ├── prometheus/
│   │   ├── prometheus.yml
│   │   └── rules/
│   │       ├── application.yml
│   │       ├── postgres.yml
│   │       ├── backup.yml
│   │       └── observability.yml
│   ├── alertmanager/
│   │   └── alertmanager.yml.example
│   ├── loki/
│   │   └── loki.yml
│   └── grafana/
│       ├── dashboards/
│       └── provisioning/
├── audit/
│   ├── README.md
│   ├── compose.yml
│   ├── vector.toml
│   ├── schema.json
│   ├── receipt-schema.json
│   ├── receipt-verifier/
│   │   └── README.md
│   └── retention.md
└── backup/
    ├── README.md
    ├── pgbackrest.conf.example
    ├── retention.md
    ├── write-admission.schema.json
    ├── write-admissionctl.sh
    ├── write-safety-watch.service
    ├── write-safety-watch.sh
    └── restore-verification.md

docs/test-evidence/database-operations/
├── README.md
├── templates/
│   └── validation-record.md
└── <environment>/
    └── <run-id>/
```

Production secrets stay in Dokploy or the selected secret store and are mounted/injected at runtime. Example files contain names and safe placeholders only.

`ops/README.md` is the version-controlled operator index. It owns the implementation-specific trust-boundary diagram, component/version/license register, topology and port inventory, responsibility map, source-of-truth links, and secret/configuration names only. It never contains credential values, personal contact details, provider account identifiers, private recovery locations, or live bucket/receiver coordinates.

The shared write-admission implementation has explicit ownership rather than controller-local helpers:

```text
backend/src/RentACar.Core/Operations/
└── IWriteAdmissionGate.cs
backend/src/RentACar.Infrastructure/Operations/
├── FileBackedWriteAdmissionGate.cs
└── WriteAdmissionSaveChangesInterceptor.cs
backend/src/RentACar.API/Configuration/
└── WriteAdmissionStartupExtensions.cs
backend/src/RentACar.Worker/
└── write-boundary integration in the established Worker host
```

The shared interface reads only the versioned infrastructure state. The Infrastructure interceptor is defense in depth for EF Core mutation paths; it does not replace explicit startup, direct-command, auth/application-service, callback, and Worker gate calls. Convention tests reject new mutation paths that do not declare a gate boundary.

## 5. Preflight Decisions and Release Blockers

These decisions must be recorded before infrastructure or application code is merged.

### 5.1 PostgreSQL Major Version

- [x] Record that Production is greenfield and has no existing Production database to preserve.
- [x] Approve PostgreSQL major 18, with `18.4` as the 2026-07-19 validation baseline.
- [x] Reject PostgreSQL 19 beta/RC for Production; reconsider major 19 only after general availability through a separate compatibility/upgrade decision.
- [ ] Revalidate the current supported PostgreSQL 18 minor at cutover and document any move from `18.4` without changing the approved major.
- [ ] Prove Npgsql/EF Core, pgBackRest, exporter, migration, backup, and restore compatibility with the pinned PostgreSQL 18 image.
- [ ] Make root Compose, backend Compose, CI service images, pgBackRest, exporter tests, and restore images use the same major version.
- [ ] Replace floating major-only images with validated version pins; add digests after acceptance.

**Exit gate:** Compose/config scans report one intended PostgreSQL major, and an isolated backup/restore test uses that same major.

### 5.2 Ownership and Objectives

- [x] Assign Product, Operations, Security, Incident Commander, implementer, approver, and backup-owner roles to one `Owner-Operator` for V1.
- [x] Accept the resulting bus-factor-1 and absence of independent human approval as an explicit personnel-redundancy risk; do not represent sequential self-checks as two-person review.
- [ ] Create the private, versioned Operations Contact Record containing the Owner-Operator's real name, primary/alternate contact paths, after-hours expectation, offline-secret custody location by name, and workstation-loss recovery instructions. Personal details do not enter this repository.
- [x] Approve `RPO <= 15 minutes` and `RTO <= 2 hours`.
- [x] Define RTO start as the earliest customer-impact database alert timestamp or Owner-Operator/Incident Commander recovery declaration.
- [x] Define RTO stop as PostgreSQL recovery plus external service health, controlled synthetic customer reservation write/read, authorized admin confirmation, immutable external timestamps, and the Owner-Operator's sequential checklist/sign-off in both Incident and Product-owner roles.
- [x] Approve the initial technical retention in the canonical plan: Prometheus/Loki 30 days, authoritative audit 3 years, backup/WAL 7-day PITR minimum, and restore reports 13 months; any legal change requires a new recorded decision.
- [ ] Select an independently reachable alert receiver.
- [x] Assign `SystemOperator` grant/revoke authority to the Owner-Operator only; actual subject IDs remain deployment/private records.
- [ ] Record incident escalation, after-hours expectations, and the solo-owner unavailability procedure in the private Operations Contact Record.

Every phase uses the same V1 human assignment:

| Phase | Implementer | Approver / exit-gate signer | Incident contact / backup owner |
|---|---|---|---|
| 0 - Baseline and reconciliation | Owner-Operator | Owner-Operator, separate timestamped review pass | Owner-Operator through alternate contact/recovery path |
| 1 - Structured logging | Owner-Operator | Owner-Operator, separate timestamped review pass | Owner-Operator through alternate contact/recovery path |
| 2 - Observability and alerts | Owner-Operator | Owner-Operator, separate timestamped review pass | Owner-Operator through alternate contact/recovery path |
| 3 - Backup, WAL, and restore | Owner-Operator | Owner-Operator, separate timestamped restore checklist | Owner-Operator through alternate contact/recovery path |
| 4 - Operations API and audit | Owner-Operator | Owner-Operator, separate timestamped security checklist | Owner-Operator through alternate contact/recovery path |
| 5 - Admin page | Owner-Operator | Owner-Operator, separate timestamped acceptance checklist | Owner-Operator through alternate contact/recovery path |
| 6 - Fault/security/recovery validation | Owner-Operator | Owner-Operator, immutable test/DR evidence | Owner-Operator through alternate contact/recovery path |
| 7 - Rollout and documentation | Owner-Operator | Owner-Operator, Production gate checklist | Owner-Operator through alternate contact/recovery path |

“Backup owner” in this table is an alternate path for the same person, not personnel redundancy. Owner unavailability therefore blocks non-automated operational action and remains an accepted V1 risk reviewed quarterly.

**Exit gate:** the private Operations Contact Record exists and is recoverable from the offline path; objectives and the single-operator limitation are recorded; `TBD` and fictitious second-person approval are not accepted for Production.

### 5.3 Free-Software and Infrastructure Constraint

- [ ] Confirm that all required components are self-hosted free/open-source editions.
- [ ] Reject required commercial adapters, trials, or entitlement-gated features.
- [x] Approve Backblaze B2 as a managed-infrastructure exception with a current 10 GB account allowance and an 8 GB combined operational planning ceiling; backup, WAL, audit, object versions, and locked objects all count.
- [x] Reject paid B2 capacity for V1; prohibit automatic paid-tier transition, silent retention reduction, and describing the local repository alone as off-host disaster recovery.
- [x] Designate the current non-trusted Staging/Dokploy VPS as the Test VPS while excluding it from Production backup, identity, monitoring authority, recovery secrets, and Production restore evidence.
- [ ] Verify current B2 terms, account onboarding, provider spend/data-cap behavior, and alert behavior before activation; provider caps do not replace internal byte measurement.
- [ ] Record actual compute, storage, bandwidth, DNS, certificate, SMTP/notification, and off-host backup costs separately from software licensing.
- [ ] If a separate monitoring or backup location is unavailable, mark independent observability/DR as blocked.

**Exit gate:** license inventory and infrastructure-cost/limitation record are reviewed.

### 5.4 Approved Production Recovery Topology

- [x] Approve one PostgreSQL 18 primary on the new Production infrastructure for V1; no hot standby or high-availability claim is approved.
- [x] Accept restore to a clean replacement host only while a measured drill remains within `RTO <= 2 hours`; failure requires a standby or additional trusted recovery infrastructure before Production acceptance.
- [x] Approve an encrypted, quota-bounded local pgBackRest repository for fast operational recovery; it is lost with the Production host and is not DR authority.
- [x] Approve a private Backblaze B2 S3-compatible bucket with 7-day Compliance Object Lock as the authoritative off-host repository.
- [x] Approve Keycloak as the `SystemOperator` OIDC MFA/step-up provider. It is a separate service, may co-reside on trusted Production infrastructure in V1, and must not be required to execute recovery.
- [x] Prohibit placing the Production Keycloak issuer/client secrets or authoritative Production recovery material on the Test VPS; a separate test realm/client and disposable test recovery material are required there.
- [x] Require a minimal Operations VPS in a different provider, account, and region from Production for Prometheus, Alertmanager, Grafana, Loki, an Operations heartbeat emitter, and dedicated Vector audit relay, with separate network, disk, DNS/control-plane dependency, deployment path, and credentials. Same-host/same-account placement is prohibited; a same-provider alternative requires a later documented exception and failure proof.
- [x] Require a receiver-side dead-man evaluator in a third account/control plane that depends on neither Production nor the Operations VPS. Separate Production and Operations emitters report to it; Alertmanager on the Operations VPS is not the evaluator for Operations-host loss.
- [x] Require a separate private B2 audit bucket with 3-year Compliance Object Lock as the authoritative security/admin audit sink; Loki is only the 30-day query copy.
- [ ] Record and prove the clean-host provisioning path, exact Operations placement, independent receiver, and Owner-Operator offline recovery-secret custody before Production acceptance.
- [ ] If no qualifying existing Operations host is available, keep Production acceptance blocked until the Owner-Operator supplies one or explicitly approves its infrastructure cost; do not weaken the failure-domain requirement.

**Exit gate:** target diagrams and runbooks distinguish single-primary DR from HA, local repository from off-host authority, and application strong-auth from offline recovery access.

## 6. Phase 0 - Baseline and Documentation Reconciliation

**Validation environments:** `[LOCAL][TEST-VPS]`; `PRODUCTION-ONLY` is limited to recording the real target identifiers and open live gates without changing Production.

### 6.1 Repository Baseline

- [ ] Start from a clean or explicitly scoped working tree.
- [ ] Record selected image/package versions and official documentation links.
- [ ] Create `ops/README.md` as the operator index and add an implementation-specific threat/trust-boundary diagram, component/version/license register, topology/port inventory, responsibility map, source-of-truth links, and secret names only.
- [ ] Create `ops/runbooks/README.md` with the stable runbook registry and create the database-operations evidence README/template defined in section 2.1; do not pre-fill later-phase runbooks or claim evidence before their implementation/validation phase.
- [ ] Register phase-gate, Production-check, runbook, and evidence-run IDs without embedding environment secrets or mutable provider coordinates.
- [x] Define environment names and evidence authority: Local Docker, the existing Staging/Dokploy host as Test VPS, and Production; record that Test VPS is never Production authority.

### 6.2 Reconcile Legacy Claims

Update, without falsifying historical evidence:

- [ ] `docs/02_ADR_ENTERPRISE_FULL.md` with the accepted database-operations architecture decisions and canonical links;
- [ ] `docs/03_TDD_ENTERPRISE_FULL.md` with a canonical-link and synchronization boundary now; add detailed interfaces, schemas, and runtime flows only with the phases that implement them so Phase 0 does not invent source behavior;
- [ ] `docs/04_IDD_ENTERPRISE_FULL.md` backup, restore, and monitoring sections;
- [ ] `docs/05_Runbook_ENTERPRISE_FULL.md` incident and restore procedures;
- [ ] `docs/06_Security_Compliance_ENTERPRISE_FULL.md` credential, audit, PII, and retention controls;
- [ ] `docs/09_Implementation_Plan.md` completion states;
- [ ] `docs/10_Execution_Tracking.md` with the new workstream;
- [ ] `docs/12_Phase10_PreLaunch_Gates.md` with independent monitoring and restore gates;
- [ ] `docs/14_Dokploy_Production_and_Local_Development.md` deployment topology;
- [ ] `docs/13_Local_Docker_Browser_Test_Checklist.md` with operations UI and degraded-mode validation.

Legacy `pg_dump` evidence may remain as a supplemental logical-export record. It must no longer be described as the sole disaster-recovery authority.

Use `docs/10_Execution_Tracking.md` for changing phase status and evidence pointers. Keep `docs/19` and `docs/20` as the normative decision/acceptance contracts; do not create a third master plan or duplicate live status across new phase-plan documents.

**Phase 0 exit gate (`DBOPS-P0-G1`):** canonical documents agree on source of truth, backup authority, Local/Test VPS/Production topology, environment-specific evidence labels, status, and remaining blockers; the operator/runbook/evidence indexes exist; the baseline and documentation checks are rerun on the Test VPS checkout.

## 7. Phase 1 - Structured Application Logging

**Validation environments:** `[LOCAL][TEST-VPS]` for all implementation and redaction/load tests. `[PRODUCTION-ONLY]` performs only a safe synthetic-canary log verification against the live pipeline after Test VPS acceptance.

### 7.1 Shared Event Contract

Define a small schema shared by API, Worker, and Next.js server logs:

| Field | Requirement |
|---|---|
| `timestamp` | UTC and machine parseable |
| `level` | Standard severity |
| `service` | `api`, `worker`, or `web` |
| `environment` | Explicit environment classification |
| `version` | Deployment version/commit when available |
| `event.name` / `event.id` | Stable bounded event identity |
| `correlationId` | Existing request correlation value |
| `traceId` | Current activity/trace when available |
| `route` | Route template or bounded operation name |
| `durationMs` | Numeric duration where relevant |
| `outcome` | Success, failure, rejected, stale, or unknown |

Dynamic customer, reservation, payment, and infrastructure values must not be used as metric labels. Log values must be bounded and redacted according to `docs/19`.

### 7.2 ASP.NET Core API

Target files:

- `backend/src/RentACar.API/Program.cs`;
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs`;
- `backend/src/RentACar.API/Middleware/RequestLoggingMiddleware.cs`;
- `backend/src/RentACar.API/Middleware/CorrelationIdMiddleware.cs`;
- `backend/src/RentACar.API/appsettings*.json`;
- `backend/tests/RentACar.Tests/Unit/Middleware/RequestLoggingMiddlewareTests.cs`.

Tasks:

- [ ] Configure the built-in JSON console logger exactly once; prove no duplicate console event.
- [ ] Include scopes/correlation without logging all headers.
- [ ] Replace unbounded raw paths with route templates when available; never log query strings.
- [ ] Make completion logging safe for success, handled failure, cancellation, and unexpected exception paths.
- [ ] Emit a single request-completion event with method, route, status, duration, correlation, and trace.
- [ ] Keep exception detail ownership in the centralized exception middleware to avoid duplicate stacks.
- [ ] Add logger category filters for framework noise without suppressing warnings/errors.
- [ ] Add tests for newline/control-character normalization and sensitive-data exclusion.

### 7.3 Worker

Target files:

- `backend/src/RentACar.Worker/Program.cs`;
- `backend/src/RentACar.Worker/Worker.cs`;
- `backend/src/RentACar.Worker/appsettings*.json`;
- `backend/tests/RentACar.Tests/Unit/Worker/WorkerTests.cs`.

Tasks:

- [ ] Configure the same JSON console contract.
- [ ] Add bounded event IDs for job started/completed/failed/retry-exhausted and Worker heartbeat.
- [ ] Use scopes for background job type and job ID without serializing payloads.
- [ ] Do not log backup command arguments, secrets, reservation/customer payloads, or provider responses.
- [ ] Emit Worker heartbeat as a metric in a later metrics phase, not a high-volume log loop.

### 7.4 Next.js Server

Target files:

- `frontend/instrumentation.ts`;
- server-only logging helpers only if required to enforce the shared schema;
- existing admin/public error boundaries for safe user-facing behavior.

Tasks:

- [ ] Use official Next.js 16 `instrumentation.ts` and `onRequestError` for Node.js server errors.
- [ ] Emit sanitized structured JSON to stdout.
- [ ] Do not log full request headers, request bodies, cookies, tokens, customer PII, or raw backend responses.
- [ ] Keep `why`/internal diagnostics server-only; do not introduce a new structured-error response contract in this workstream.
- [ ] Do not add browser-wide transport or a public log ingestion endpoint.
- [ ] Replace production `console.error(error)` calls only where they expose unsafe raw objects or create unstructured server logs; do not mechanically rewrite harmless client development diagnostics without a defined destination.

### 7.5 Phase 1 Verification

- [ ] Unit tests cover success, handled failure, thrown exception, cancellation, correlation, and redaction.
- [ ] Local Docker output is valid one-event-per-line JSON for API, Worker, and Next.js server events.
- [ ] Canary token, cookie, email, phone, payment, and identity values do not appear in captured logs.
- [ ] A representative request produces correlated API and web events without duplicate request logs.
- [ ] Log volume is measured under the existing k6 smoke profile.

**Phase 1 exit gate (`DBOPS-P1-G1`):** applicable unit/integration/load/redaction checks have `LOCAL-PASS`, the deployed Test VPS log stream has `TEST-VPS-PASS`, and structured logs are safe and machine parseable before Loki is introduced. The later Production canary cannot compensate for a missing Test VPS pass.

## 8. Phase 2 - Metrics, Log Pipeline, Dashboards, and Alerts

**Validation environments:** `[LOCAL][TEST-VPS]` for the full co-located/deployed stack and fault tests. `[PRODUCTION-ONLY]` proves the real independent Operations VPS, firewall/ingress, DNS/TLS, receiver, credentials, and failure-domain placement.

### 8.1 Infrastructure Files

- [ ] Create `ops/observability/compose.yml` as a separately deployable stack.
- [ ] `[TEST-VPS]` Deploy the complete observability/audit stack on the Test VPS with test-scoped credentials; co-location is allowed only for functional and destructive test evidence.
- [ ] `[PRODUCTION-ONLY]` Deploy and verify the live observability/audit stack on the approved independent Operations VPS, not on the Production application host or Test VPS.
- [ ] Deploy the dedicated Vector audit relay from `ops/audit/` on the same independent Operations failure domain with its own bounded encrypted disk queue and credentials; do not build a custom relay service.
- [ ] Deploy the minimal audit-receipt verifier in the receiver-side third control plane, not on Production or the Operations VPS. It is a read-only B2 evidence adapter/status service, not a relay, queue, mutable audit authority, dashboard, or general object browser.
- [ ] Pin Prometheus, Alertmanager, Grafana OSS, Loki, Alloy, Vector, and `postgres_exporter` images by validated version.
- [ ] Add health checks, restart policy, persistent volumes, resource limits, and bounded retention.
- [ ] Keep all management and ingest ports on private/internal networks.
- [ ] Add safe `.example` configuration; inject real secrets at deployment.

Do not merge a direct `/var/run/docker.sock` mount into Alloy. First detect the host Docker logging driver:

- preferred: host-level Alloy or read-only file tailing of the actual container log files;
- alternative: a restricted Docker socket proxy exposing only required read endpoints;
- prohibited: publicly reachable Docker API or an unrestricted raw Docker socket inside the collector container.

### 8.2 PostgreSQL Exporter

- [ ] Create a dedicated login such as `rentacar_monitor` through an infrastructure-owned initialization step.
- [ ] Grant `CONNECT` only to required databases and `pg_monitor` or a narrower validated set.
- [ ] Set a small connection limit.
- [ ] Set connection, statement, and lock timeout defaults for the monitoring identity.
- [ ] Store the password as a mounted secret/file, not in Compose, environment examples, URLs, or logs.
- [ ] Expose exporter metrics only to the private Prometheus network.
- [ ] Disable unnecessary high-cardinality collectors.
- [ ] Enable long-transaction, lock, WAL, relation, and wraparound collectors only after load measurement.

**Negative gate:** the exporter role cannot create/alter/drop objects, write business data, terminate sessions, change settings, or execute maintenance.

### 8.3 Host Metrics through Alloy

- [ ] Use Alloy's built-in `prometheus.exporter.unix`; do not deploy a separate host exporter without a new measured requirement.
- [ ] Bind only the required host filesystem, procfs, and sysfs paths read-only.
- [ ] Disable collectors that require privileged mode, write access, or unapproved Linux capabilities.
- [ ] Collect bounded CPU, memory, disk, filesystem, load, and network availability signals.
- [ ] Exclude pseudo filesystems, container overlay paths, volatile mount points, and high-cardinality device noise.
- [ ] Verify the Alloy container is not privileged and cannot write to the mounted host paths.

### 8.4 Application and Worker Metrics

- [ ] Preserve `ILogger`; do not introduce Serilog solely for metrics.
- [ ] Use official .NET `Meter`/`ActivitySource` and OpenTelemetry packages where instrumentation is required.
- [ ] Send OTLP to Alloy over a private endpoint; Alloy forwards metrics into the Prometheus-compatible pipeline.
- [ ] Record API request duration/count by bounded route template, method, and status class.
- [ ] Record Worker heartbeat, queue depth, processed/failed jobs, and retry exhaustion.
- [ ] Never use customer, reservation, vehicle, payment, correlation, exception message, raw URL, or job payload as a metric label.
- [ ] Add cardinality tests or configuration checks for every custom metric.

### 8.5 Log Pipeline

- [ ] Configure Alloy to read one-event-per-line container/host logs.
- [ ] Attach bounded labels: environment, service, host, container, and severity only.
- [ ] Do not promote correlation IDs, user IDs, reservation IDs, paths with IDs, exception messages, or arbitrary fields to Loki labels.
- [ ] Forward to Loki over authenticated TLS when crossing a host boundary.
- [ ] Configure 30-day retention and a storage quota.
- [ ] Alert on ingestion errors, rejected entries, queue/backoff growth, Loki storage pressure, and source silence.
- [ ] Preserve raw JSON fields as searchable content after redaction.

### 8.6 Prometheus and Rules

- [ ] Configure 30-day retention and storage-pressure alerts.
- [ ] Scrape only private targets.
- [ ] Add recording rules only when they reduce query cost or normalize stable indicators.
- [ ] Add rule tests for PostgreSQL, API, Worker, backup, and observability-pipeline alerts.
- [ ] Add statistics-reset awareness to index and relation advisory queries.
- [ ] Require a 14-day baseline before tuning non-safety thresholds.

### 8.7 Alertmanager

- [ ] Configure grouping, deduplication, inhibition, cooldown, and recovery notifications.
- [ ] Route critical alerts to an independently reachable receiver.
- [ ] Keep receiver secrets outside source control.
- [ ] Configure separate synthetic heartbeat emitters on Production and the Operations VPS, each with a distinct canary ID, bounded interval, and no business data.
- [ ] Configure missed-check evaluation at the external receiver/evaluator, not in Alertmanager on the Operations VPS. It independently pages on missing Production emissions and missing Operations emissions and remains reachable when either or both managed VPS workloads are unavailable.
- [ ] Keep evaluator/receiver credentials and deployment control outside both VPS credential sets; a shared notification destination is allowed only when its missed-check evaluation remains a separate control plane.
- [ ] Test at least one warning, one critical, one recovery, one deduplicated storm, and one receiver failure.

### 8.8 Grafana

- [ ] Provision Prometheus and Loki read-only data sources.
- [ ] Provision dashboards from source-controlled JSON or declarative configuration.
- [ ] Apply Viewer/Editor/Admin separation and private access.
- [ ] Disable anonymous access.
- [ ] Add dashboard links to runbooks rather than operational buttons.
- [ ] Verify dashboards do not reveal secrets, query parameters, PII, raw SQL, or bind values.

### 8.9 Phase 2 Verification

- [ ] Public-network checks cannot reach management/exporter endpoints.
- [ ] Alloy host-metric mounts are read-only, the container is not privileged, and disabled collectors do not create false critical alerts.
- [ ] PostgreSQL saturation and exporter timeout do not amplify database load.
- [ ] Alloy/Loki outage does not block API, Worker, or Next.js responses.
- [ ] Prometheus/Alertmanager outage does not block business traffic.
- [ ] End-to-end synthetic alerts and separate Production/Operations heartbeat canaries reach the external evaluator/receiver and recover.
- [ ] Simulated loss of the Production workload triggers the missing-Production-canary alert while the Operations pipeline remains reachable; simulated loss of the entire Operations monitoring workload triggers the missing-Operations-canary alert from the external evaluator even though Alertmanager/dashboards/relay are unavailable.
- [ ] Grafana can correlate a safe request event with API metrics by time/correlation without high-cardinality labels.
- [ ] Retention and quota behavior are demonstrated in Local Docker where feasible and mandatorily on the Test VPS.

**Phase 2 exit gate (`DBOPS-P2-G1`):** Local Docker configuration/fault checks pass and the full deployed Test VPS pipeline proves external receiver delivery, source failure, recovery, and no business-traffic coupling. Actual cross-provider/account/region independence remains a labeled `PRODUCTION-ONLY` gate.

## 9. Phase 3 - pgBackRest, WAL/PITR, and Restore Evidence

**Validation environments:** `[LOCAL]` uses encrypted local repositories and synthetic data; `[TEST-VPS]` uses dedicated real B2 test buckets/prefixes and performs the complete destructive outage/quiescence/restore matrix; `[PRODUCTION-ONLY]` verifies actual bucket policies, scoped secrets, live capacity/WAL freshness, and an isolated restore from Production artifacts without modifying Production.

### 9.1 Repository and Secrets

- [x] Select Backblaze B2 through its S3-compatible endpoint as the off-host repository target.
- [x] Select separate quota-bounded local-repository paths for Local Docker, Test VPS, and Production; the Test VPS repository contains only synthetic/test backup data and is never Production authority.
- [ ] Enable repository encryption.
- [ ] `[LOCAL][TEST-VPS]` Inject environment-scoped pgBackRest cipher/B2 test credentials through local/test secret stores; never place them in source, images, logs, API/frontend configuration, or Production secret locations.
- [ ] `[PRODUCTION-ONLY]` Inject the actual cipher passphrase and bucket/prefix-scoped B2 runtime key through the Production secret store and verify the sanitized mount/reference inventory.
- [ ] `[TEST-VPS]` Drill loss/recovery with a separate test KeePassXC record and disposable test secrets; do not copy Production recovery material to the Test VPS.
- [ ] `[PRODUCTION-ONLY]` Store the authoritative Production recovery copy in the encrypted Owner-Operator KeePassXC database plus one offline removable copy and verify host-loss access without exposing the secret.
- [ ] Keep B2 account/master credentials and Object Lock administration capability off all runtime hosts; require provider MFA and separate test/Production scoped application keys.
- [ ] Maintain a signed, versioned, non-secret accepted-version recovery manifest outside runtime hosts. For every accepted `repo2` chain, map repository/stanza, backup label, repository generation, retained-chain interval, and every required pgBackRest data/metadata/WAL object name to its B2 file/version ID, digest, and lock expiry. Record only the scoped application-key ID and cipher generation, never secret values.
- [ ] Rotate B2 runtime credentials by overlapping old/new keys only until archive/check and an isolated restore pass with the new key; then revoke the old upload key. Never treat B2-key rotation as repository-cipher rotation.
- [ ] Preserve each retired repository decryption secret offline until every dependent locked backup chain has expired under policy and an oldest-retained-chain restore proves it is no longer required. A cipher change requires a separately verified repository migration/new repository; blind in-place passphrase replacement is prohibited.
- [ ] `[TEST-VPS]` Create dedicated private backup/audit test buckets or prefixes with the shortest approved test retention, synthetic data, bounded quota, and clearly prefixed test object names.
- [ ] `[PRODUCTION-ONLY]` Create/verify the actual private backup bucket/prefix with 7-day Compliance Object Lock before accepted Production backup data is written.
- [ ] `[TEST-VPS]` Prove protected test versions cannot be deleted, deliberately create same-name upload and hide-marker faults in a disposable test prefix, and prove the version-aware monitor detects both. Do not claim Object Lock prevents a newer version or hide marker from shadowing a protected historical version.
- [ ] `[PRODUCTION-ONLY]` Safely prove an actual locked Production test-marker version cannot be deleted with the runtime credential, inspect version-aware controls by read-only evidence, and confirm expiry never precedes the 7-day lock; do not create a same-name/hide fault against accepted recovery objects.
- [ ] `[TEST-VPS]` Exercise provider alerts plus infrastructure-owned stored-byte metrics and simulated 6/7.5/8/9 GB/quota states without allocating those real volumes.
- [ ] `[PRODUCTION-ONLY]` Enable/verify actual provider alerts and reconcile total/per-bucket backup, WAL, audit, version, and locked-object bytes against the 10 GB account allowance and no-paid-B2 policy.
- [ ] Estimate audit/lock overhead from Local/Test VPS evidence, then `[PRODUCTION-ONLY]` replace estimates with measured Production data. Produce three separate conservative forecasts: 30-day operational growth, peak backup/WAL/version usage across the rolling 7-day PITR window and chain overlap, and cumulative audit/locked/version usage through the full 3-year retention horizon. Their combined peak must stay below 8 GB; a green 30-day forecast alone cannot pass.

### 9.2 PostgreSQL and pgBackRest

- [ ] Create a pgBackRest stanza for the selected PostgreSQL major.
- [ ] Pin repository identities in configuration and evidence: `repo1` is the quota-bounded local repository and `repo2` is the B2 S3-compatible repository. Configuration validation fails on identity/order drift because restore priority and repository-specific schedules depend on these stable IDs.
- [ ] Configure `archive_mode`, pgBackRest WAL `archive_command`, `archive-async=y`, and a durable pgBackRest spool/status path.
- [ ] Set `archive_timeout=60s` as the initial low-write baseline; allow a later increase only when the low-write acceptance test still proves `RPO <= 15 minutes`.
- [ ] Keep `archive-push-queue-max` unset and add a configuration test that fails if it is assigned any finite value; silently dropped/acknowledged WAL is prohibited.
- [ ] Validate that `archive-push` reaches every configured repository and record repository-specific WAL freshness; this fan-out never counts as proof that either repository contains a current base-backup chain.
- [ ] Schedule weekly full plus daily differential/incremental `backup` commands separately with explicit `--repo=1` and `--repo=2`. Record the command, selected repository, backup label, start/stop time, and outcome; an unqualified single backup command cannot satisfy either repository gate.
- [ ] Run and retain repository-specific `check`, `info`, retention/`expire` dry-run or equivalent safe evaluation, and post-expire evidence for `--repo=1` and `--repo=2`. Automatic expire after one repository's backup never substitutes for evidence from the other repository.
- [ ] Keep at least two complete recoverable chains in the local repository within its dedicated quota.
- [ ] In `repo2`, retain every full/differential/incremental backup and WAL segment needed to restore every timestamp in the rolling 7-day PITR window. With weekly full backups, keep at least two overlapping full chains and their dependent WAL, plus transition overlap until an explicit `--repo=2` oldest-in-window restore succeeds; the repository-specific expire evaluation must prove no required target is removed.
- [ ] Restore a known synthetic watermark explicitly from `--repo=1` and independently from `--repo=2` into clean targets. Never allow pgBackRest's default repository-priority selection to satisfy the B2 restore gate accidentally from local artifacts.
- [ ] After each accepted `repo2` backup/check/expire cycle, generate and sign the accepted-version manifest from a separate list/read identity. Reject acceptance when any required object lacks a file/version ID, digest, valid lock horizon, or unambiguous mapping; alert on a hide marker, same-name version, missing current version, unexpected version count, file-ID/digest drift, or manifest/signature staleness.
- [ ] Provide a private, offline recovery tool that reads the signed manifest, downloads each pinned historical object by B2 file ID, verifies every digest and repository-generation binding, and materializes a new isolated repository/prefix. It must never overwrite, unhide, delete, or otherwise repair the authoritative B2 bucket. Point pgBackRest at the reconstructed repository only after complete verification; pgBackRest is not expected to select historical B2 versions itself.
- [ ] Expose only sanitized backup age, WAL freshness, verification, and restore-test metrics to Prometheus.
- [ ] Do not run pgBackRest or shell commands from the API.

### 9.3 WAL-Archive Degradation and Write Quiescence

- [ ] Emit repository-specific last-success age, archive backlog bytes/count, oldest queued WAL age, `pg_wal` free bytes, and estimated time-to-full without exposing paths or credentials.
- [ ] Implement the state machine from conservative `oldest_unprotected_wal_age`, not time since confirmed failure: `NORMAL`; immediate `DEGRADED` on confirmed B2 archive failure; `WARNING` at 5 minutes; `CRITICAL` at 10 minutes; `WRITE_QUIESCED` at 13 minutes, when the verified remaining RPO margin is one minute or less, or earlier when estimated `pg_wal` time-to-full is 15 minutes or less.
- [ ] Bound the uncertainty budget to at most two minutes across `archive_timeout=60s`, archive-command timeout/retry, and watcher polling/detection. Emit the budget components as sanitized metrics; missing, unknown, stale, or regressed age/budget evidence quiesces immediately.
- [ ] Add one atomic, short-TTL, infrastructure-owned `DatabaseWritesEnabled` state with schema version, reason/mode, incident-or-change ID, observed/activation/expiry timestamps, source evidence age, and monotonic revision. Mount it read-only into API and Worker; a business-only state name or scope is prohibited.
- [ ] Implement one least-privilege local write-safety watcher under `ops/backup/`. It polls at most every 15 seconds and refreshes the state from local pgBackRest/archive, `pg_wal`, and filesystem-capacity evidence; it may change `true` to `false` automatically but can never change `false` to `true`.
- [ ] Treat a missing, malformed, expired, or regressed-revision state as write-quiesced in Production. Local/Development may use an explicit safe test fixture; Production bypass is prohibited by startup/config tests.
- [ ] Add one mandatory `IWriteAdmissionGate` (or equivalently named bounded service) that is consumed before every database mutation boundary. HTTP middleware may reject mutating requests early, but startup initialization, application/auth services, EF Core mutation paths, approved direct SQL paths, internal callbacks, and Worker jobs must invoke the same gate independently; no mutation path may rely only on route classification.
- [ ] Read and validate the state before `InitializeApiAsync` calls `ApplyDatabaseMigrationsAsync`, `ApplyLocalAdminSeedAsync`, or `ApplyConcurrentBookingInventorySeedAsync`. While quiesced, these operations are refused/skipped without opening a write transaction. Start read/recovery-only service only when a no-write schema-compatibility preflight passes; otherwise fail startup closed without changing the database.
- [ ] Classify admin/customer login, refresh, logout, password/account changes, session creation/rotation/revocation, failed-attempt counters, last-seen updates, and similar identity side effects as database writes. Deny them while quiesced. Recovery access must use a pre-provisioned private-host/offline or already-valid non-rotating credential that can be verified without a database write; it cannot create, refresh, revoke, seed, or update a session.
- [ ] Define one audited `INITIAL_SCHEMA_BOOTSTRAP` mode for the greenfield database. It is authorized manually through the same gate only after pgBackRest/WAL reaches both repositories and capacity evidence is healthy, while public ingress and Worker are disabled. Bind it to a change ID and short expiry, run only migrations/required seeders, capture evidence, and disable the gate again before remaining cutover checks. Configuration/tests prohibit this mode during degraded/stale/unknown archive state and after normal Production cutover.
- [ ] Keep health, monitoring, explicitly allowlisted safe reads, and the no-write recovery-auth path available. Any route or service whose no-write behavior is not proven is denied while quiesced.
- [ ] Pause Worker jobs before their next mutation boundary while quiesced; in-flight work must either commit atomically before the boundary or roll back without blind retry.
- [ ] Do not expose the quiescence switch in the admin UI/API and do not implement controller-specific flags.
- [ ] Resume writes only after the entire archive backlog is accepted by B2, pgBackRest repository/archive checks prove no WAL gap, archive age is within policy, combined B2 usage/projection is below 8 GB, and the Owner-Operator completes the timestamped resume checklist.
- [ ] Provide one private-host-only `write-admissionctl.sh` for schema-validated atomic manual resume or the bounded initial bootstrap window after its checklist. It requires an incident/change ID and current evidence, emits the durable audit event before enabling writes, restricts allowed mode transitions, and has no remote/web listener.
- [ ] Prohibit automatic WAL deletion, archive disablement, retention shrink, Object-Lock bypass, paid-capacity enablement, or automatic write resume.
- [ ] Run the watcher without database superuser, B2 account-admin, Dokploy-admin, application-signing, or shell-dispatch credentials; grant only the minimum local read evidence and atomic state-file write path.

**Quiescence verification:** contract tests enumerate startup migrations/seeders, admin/customer auth and session effects, all mutating API endpoints, direct database-command paths, Worker write jobs, and internal callbacks. A quiesced process-restart test proves zero PostgreSQL writes while schema-compatible reads/no-write recovery access remain available, and a schema-incompatible restart fails closed without migration. Bootstrap tests prove the bounded mode requires healthy all-repository WAL/capacity evidence, disabled ingress/Worker, a change ID and expiry, and cannot be invoked after cutover or during degraded evidence. Fault tests prove writes are refused before RPO/disk exhaustion and resume only after gap-free evidence.

### 9.4 Restore Verification

The restore verifier must be infrastructure-owned and isolated from Production application data paths.

- [ ] Restore into a fresh PostgreSQL instance on the same major version.
- [ ] Verify schema/migration presence.
- [ ] Run bounded data-integrity queries that do not export customer records.
- [ ] Run application-level smoke checks against the restored database where safe.
- [ ] Start the RTO clock at the earliest qualifying alert/declaration timestamp, not at restore-command execution.
- [ ] Before each destructive drill, commit one side-effect-disabled synthetic recovery probe with a stable non-PII ID and database UTC timestamp, then record its acknowledgement at the independent external receiver. This receipt is the authoritative pre-impact RPO watermark; no customer data is copied into evidence.
- [ ] After restore, run external database/API/Worker/public HTTPS health checks and a controlled non-payment synthetic reservation availability/write/read/admin-confirmation flow with notifications disabled.
- [ ] Use an unmistakable recovery-test marker and synthetic contact data, suppress payment/SMS/email side effects, retain auditable evidence, and remove the test reservation through the approved cleanup runbook.
- [ ] Compare the restored probe ID/database timestamp with the external receipt and label RPO `PASS` only when the target is within 15 minutes. For a real incident, accept only an independently timestamped pre-existing business acknowledgement; otherwise record `RPO=UNVERIFIED` and keep the recovery gate open.
- [ ] Stop RTO only after the Owner-Operator completes the sequential checklist and records immutable external timestamps while acting in both Incident and Product-owner roles; record explicitly that no independent human approval occurred.
- [ ] Record backup set, target time, replay target, RTO start/stop evidence, measured RPO/RTO, integrity result, Owner-Operator, checklist version, and evidence hashes/locations.
- [ ] Destroy the isolated restore environment through the approved cleanup procedure after evidence is retained.
- [ ] Schedule monthly restore verification and quarterly DR exercises.

### 9.5 Worker Backup Cutover

- [ ] Keep `BackgroundJobs:DailyBackup:Enabled=false` during rollout.
- [ ] Do not mark cutover complete until two independently evidenced scheduled cycles have run for both `--repo=1` and `--repo=2`, WAL freshness is proven for every configured repository, repository-specific check/info/expire evidence passes, and clean restores explicitly selected from each repository succeed.
- [ ] After cutover, remove or formally deprecate the Worker backup command path and update its tests/docs.
- [ ] Preserve logical `pg_dump` only as an optional portability export with separate status; it is not PITR/DR authority.

### 9.6 Phase 3 Failure Tests

- [ ] Backup target temporarily unavailable.
- [ ] B2 archive is unavailable across warning/critical/quiescence boundaries; a commit immediately after the previous WAL switch plus worst-case archive timeout, archive-command timeout/retry, and watcher polling proves quiescence before the oldest unprotected commit reaches 15 minutes and without a WAL gap.
- [ ] `pg_wal` time-to-full becomes shorter than 15 minutes; early quiescence takes precedence over the elapsed-time schedule.
- [ ] Low-write workload proves the 60-second `archive_timeout` baseline and end-to-end archive evidence satisfy the 15-minute RPO.
- [ ] Database host restarts during backup.
- [ ] An unqualified/single-repository backup schedule is rejected by configuration tests; `repo1` and `repo2` inventories prove each has its own current base-backup chain rather than WAL-only content.
- [ ] Repository credential is invalid or expired.
- [ ] Backup is present but verification fails.
- [ ] Restore exceeds RTO.
- [ ] `[TEST-VPS]` Simulate loss of the source application host and local repository. Power off or make the source host plus its storage/network unreachable, then restore from `repo2` into a clean target on a separate recovery host/control plane that does not share the source hypervisor, disk, Dokploy control plane, or required network path. The target uses offline test secrets and does not depend on source Test VPS services or Keycloak. If no such target is available, record only functional restore evidence and keep the host-loss gate open.
- [ ] B2 stored bytes cross 6 GB, 7.5 GB, 8 GB, and 9 GB thresholds and route the required severity.
- [ ] B2 quota rejects a WAL/backup write; status becomes critical and no old healthy evidence remains green.
- [ ] Combined account usage includes the audit bucket, versions, and locked objects; crossing 8 GB blocks acceptance and crossing 9 GB pages critically without enabling payment.
- [ ] Runtime B2 credential cannot delete a protected version. In a disposable test prefix, same-name upload and hide-marker faults are detected immediately; a direct name-based/current-version restore fails or is rejected safely, while a clean repository reconstructed from the signed manifest's historical file IDs restores successfully without mutating the original bucket.
- [ ] Immediately after a new weekly full and after each repository's expire evaluation, restore the oldest timestamp still inside the applicable retention window explicitly from `--repo=1` and `--repo=2`; prove the selected repository's preceding overlapping full chain plus required WAL remain available.
- [ ] Rotate the B2 runtime key and restore the oldest retained chain with the versioned recovery manifest; separately prove no repository-cipher secret is retired while a locked dependent chain remains.
- [ ] Latest recovery point is intentionally removed/corrupted in an isolated test repository.
- [ ] Alert and admin status distinguish `FAILED`, `STALE`, `UNVERIFIED`, and `UNKNOWN`.

**Phase 3 exit gate (`DBOPS-P3-G1`):** applicable Local Docker backup/PITR checks pass; on the Test VPS, two scheduled cycles per explicit repository, all-repository WAL freshness, separate check/info/expire evidence, real test-B2 interruption/quiescence tests, proof that `archive-push-queue-max` is unset, version-drift detection, direct plus manifest-reconstructed restores, and isolated synthetic restores explicitly from both `repo1` and `repo2` pass. The host-loss sub-gate additionally requires a separate recovery host/control plane while the source is unavailable. Backup/WAL-file existence, a default-priority restore, or a co-located target cannot pass, and Production restore authority remains open until the `PRODUCTION-ONLY --repo=2` isolated restore succeeds.

## 10. Phase 4 - Read-Only Operations Overview Module

**Validation environments:** `[LOCAL][TEST-VPS]` for application code, Keycloak test realm, fake/real monitoring adapters, Vector, test B2 audit sink, outbox, fail-closed, and break-glass behavior. `[PRODUCTION-ONLY]` verifies the actual issuer/client/claim mapping, scoped audit credential/bucket, allowlisted source URLs, and safe authorized-view audit.

### 10.1 Module Shape

Target folder:

```text
backend/src/RentACar.API/
├── Contracts/Operations/
├── Controllers/AdminOperationsController.cs
└── Services/Operations/
    ├── IOperationsOverviewReader.cs
    ├── OperationsOverviewReader.cs
    ├── IOperationsAuditWriter.cs
    ├── VectorOperationsAuditAdapter.cs
    ├── IOperationsAuditReceiptReader.cs
    ├── B2AuditReceiptAdapter.cs
    ├── PrometheusOperationsAdapter.cs
    ├── AlertmanagerOperationsAdapter.cs
    ├── BackupStatusAdapter.cs
    └── internal ports/options/normalizers
```

The external interface remains one aggregate read:

```csharp
Task<OperationsOverview> GetOverviewAsync(CancellationToken cancellationToken);
```

No controller, page, or test outside the module learns PromQL, Alertmanager JSON, backup command output, provider URLs, or provider exceptions.

### 10.2 Stable DTO

The DTO should contain only bounded, sanitized fields:

```text
OperationsOverview
├── overallStatus
├── generatedAtUtc
├── sources[]
│   ├── name
│   ├── status
│   ├── sourceTimestampUtc
│   ├── ageSeconds
│   └── safeReasonCode
├── services
│   ├── api
│   ├── worker
│   ├── database
│   ├── metricsPipeline
│   ├── logPipeline
│   └── backup
├── alerts
│   ├── criticalCount
│   └── warningCount
├── capacity
└── links[]
```

The response must not include raw provider response text, internal base URLs, credentials, SQL, log content, customer data, or stack traces.

### 10.3 Source Access

- [ ] Configure source URLs only from validated deployment configuration.
- [ ] Reject non-HTTPS cross-host URLs in Production unless a private mutually authenticated transport is explicitly approved.
- [ ] Reject loopback, link-local, metadata, or arbitrary redirect targets unless they are the exact configured private endpoint.
- [ ] Use separate typed HTTP clients, strict connect/request timeouts, no untrusted redirects, and response-size limits.
- [ ] Read sources in parallel with independent cancellation and circuit breakers.
- [ ] Cache normalized results in memory for approximately 30 seconds; do not write snapshots to the application database.
- [ ] Preserve per-source timestamps and classify stale/untrusted centrally.
- [ ] Return partial results when one source fails.

### 10.4 Configuration and Startup Validation

Proposed configuration surface:

```text
Operations.Enabled
Operations.MaxSourceAgeSeconds
Operations.CacheSeconds
Operations.RequestTimeoutMilliseconds
Operations.Prometheus.BaseUrl
Operations.Alertmanager.BaseUrl
Operations.BackupStatus.BaseUrl or approved status source
Operations.AuditRelay.BaseUrl
Operations.AuditReceipt.BaseUrl
Operations.Grafana.OperatorUrl
Operations.RunbookBaseUrl
Operations.StrongAuthRequired
```

Secrets use secret-file or environment-backed options and are never represented in example values.

- [ ] Production startup fails when Operations is enabled without validated source allowlists (including the audit-receipt verifier), strong-auth requirement, private/authenticated source access, signed-receipt verification material, or required secret references.
- [ ] Development bypass configuration fails startup when `Environment=Production`.
- [ ] Invalid URI schemes, hosts, redirects, timeouts, or unbounded cache/source-age values fail validation.

### 10.5 Authorization

Target files include:

- `backend/src/RentACar.API/Configuration/AuthPolicyNames.cs`;
- authorization registration/handler files under `Configuration/` or the established auth folder;
- `backend/tests/RentACar.Tests/Unit/Services/AuthConventionsTests.cs`;
- `backend/tests/RentACar.Tests/Unit/Services/AuthEndpointSecurityConventionsTests.cs`;
- `backend/tests/RentACar.Tests/Unit/Controllers/RbacPolicyMatrixTests.cs`.

Tasks:

- [ ] Add `SystemOperatorOnly` as a distinct policy.
- [ ] Do not make `SuperAdminOnly` automatically satisfy it unless an explicit separate claim is present.
- [ ] Resolve membership from deployment-controlled subject IDs or an approved external identity group.
- [ ] Integrate Keycloak OIDC for the operations client and resolve `SystemOperator` membership from its deployment-controlled group/claim mapping.
- [ ] Require MFA plus the approved Keycloak `acr`/LoA step-up level and authentication age no greater than 10 minutes before enabling in Production.
- [ ] Validate the required token level/age in the API; default client login level or hidden navigation is not sufficient evidence.
- [ ] Keep customer and ordinary business-admin authentication independent from this Keycloak operations client.
- [ ] Document offline recovery access that remains usable when Keycloak or the Production host is unavailable.
- [ ] Prevent application admin screens from granting/revoking the claim.
- [ ] Emit durable off-host audit evidence for infrastructure grant/revoke changes.

### 10.6 Audit Relay and Authoritative Sink

- [ ] Define one versioned, bounded audit schema containing event ID, opaque event partition `SHA-256(event ID)`, occurred/received UTC timestamps, stable non-PII actor identifier or subject hash, action, outcome, safe reason code, correlation/incident ID, source service, and integrity metadata only. Canonically hash the delivery-relevant fields and carry that digest with the stable event ID.
- [ ] `[LOCAL][TEST-VPS]` Configure the pinned Vector relay with authenticated private TLS ingress, encrypted quota-bounded blocking disk buffer, bounded payload/rate limits, retry/backoff, health/delivery-age metrics, and no public listener. Treat its HTTP/source response only as transient submission outcome. Vector provides no V1 fsync-backed application acknowledgement or intermediate application durability state; source acknowledgement, disk-buffer behavior, sink finalization, and S3 request success never directly create `B2_DELIVERED`.
- [ ] `[PRODUCTION-ONLY]` Verify the same Vector configuration on the independent Operations VPS with actual private routes, scoped secrets, persistent volume, and external receiver path.
- [ ] Configure Vector's `aws_s3` sink with the Backblaze S3-compatible endpoint and acknowledgements enabled for pipeline durability/retry only; configure Loki as the non-authoritative 30-day query path. Document and test that neither Vector nor Loki exposes the application-consumable per-event remote receipt required by the overview gate.
- [ ] Configure a validated Vector transform plus S3 `key_prefix` template so each event is written under `audit/v1/event={{ opaque_event_partition }}/`; set `batch.max_events=1`, keep a unique never-reused object suffix, and pin byte/time limits so a healthy path flushes within the overview request budget. Include one-object-per-event and object/version overhead in the 3-year capacity forecast.
- [ ] Implement a minimal receipt verifier in the receiver-side control plane outside Production and the Operations VPS. It validates the event-ID format, derives the opaque partition itself, lists only the exact event prefix with strict page/object/version/time bounds, and uses a separate read-only B2 list/get-version credential. It consumes no caller-supplied bucket/key/version coordinates, parses the bounded object, and matches the stable event ID plus canonical digest. Zero candidates, multiple current candidates, unexpected versions, pagination, or bound overflow fail closed and alert.
- [ ] On a successful match, issue a short-lived signed receipt containing schema version, event ID, object key, B2 version/file ID, object/event digest, observed-at UTC, expiry, and verifier key ID. The B2 object version is authoritative; the receipt cache is reconstructible and cannot issue a receipt without a fresh/within-policy B2 observation.
- [ ] Expose only an authenticated, rate/size-bounded `GET receipt-status/{eventId}` (or equivalent) over the private allowlisted route. Reject caller-selected destinations, redirects, stale/expired receipts, unknown signing keys, digest/event mismatches, replay across event IDs, and responses missing the object version/file ID.
- [ ] `IOperationsAuditWriter` returns only success/failure of transport submission and exposes no durable audit enum. `IOperationsAuditReceiptReader` validates the signed verifier response and is the sole application component allowed to map an event to `B2_DELIVERED`; controllers and the Vector adapter cannot set that state.
- [ ] `[TEST-VPS]` Create a separate private test audit bucket/prefix with short bounded test retention, unique never-reused object names, and three non-Production identities: upload-only relay; live read/list/get-version receipt verifier; and offline read-only governance verifier. Exclude delete, retention/legal-hold mutation, governance bypass, and bucket/account administration as applicable. Execute Object-Lock, hide-marker, version-list, credential-confusion, backlog, disk-full, retry, duplicate, and break-glass tests using synthetic events.
- [ ] `[PRODUCTION-ONLY]` Create/verify the actual separate private B2 audit bucket with 3-year Compliance Object Lock before accepted Production events are sent. Provision mutually exclusive bucket/prefix-scoped relay-upload and receipt-verifier-read identities; keep account/master, delete-version, retention-administration, and offline governance-verification credentials off both runtime hosts.
- [ ] Because B2 upload capability can still create name-hiding versions, use the third offline/read-only governance identity to inventory object versions/file IDs, detect hide markers or missing current versions, reconcile signed receipts against B2, and prove lifecycle rules cannot hide or delete accepted audit evidence before retention expiry. Normal evidence queries and live receipts must be version-aware rather than name-only.
- [ ] Mirror sanitized audit events to Loki for 30-day query only; Loki success never substitutes for immutable B2 delivery.
- [ ] For normal overview access, submit the event, then poll the receipt reader by stable event ID within the bounded deadline. Disclose data only after a valid B2-derived receipt maps it to `B2_DELIVERED`. Submission success, Vector/disk-buffer/S3 pipeline outcome, or receipt-cache presence without current receipt validation marks `AUDIT_DEGRADED`, alerts critically, and denies new normal overview reads.
- [ ] If the relay, B2, or receipt verifier cannot return the required proof within the bounded request deadline, deny normal overview access while preserving the stable queued event for retry. Public and ordinary business traffic remain unaffected.
- [ ] Implement break-glass overview access only for the Owner-Operator with valid MFA/recent authentication, a non-empty incident ID, immutable external timestamp evidence, a distinct critical alert, and export disabled. Reconcile the audit event when the relay recovers.
- [ ] Keep export out of V1. Any future overview export requires a separate data-minimization, authorization, rate-limit, and audit decision.
- [ ] Require confirmed off-host audit acknowledgement before `SystemOperator` grant/revoke or any future operations control-plane mutation; these actions have no fail-open mode.
- [ ] Replace fire-and-forget handling for existing security-critical business mutations with a transactional outbox written atomically with the business change and delivered through Vector. Use optimistic claim/lease, stable event IDs, bounded retry, poison-event handling, and cleanup only after `B2_DELIVERED`; local-buffer acknowledgement alone never authorizes cleanup.
- [ ] Do not use the transactional outbox for overview reads and do not treat the application database as the authoritative audit sink.
- [ ] Treat audit delivery as at least once. Retries reuse the same event ID; immutable B2 objects may contain duplicates, and evidence/query tooling deduplicates by event ID. Do not claim exactly-once delivery.
- [ ] Prohibit tokens, headers, response bodies, raw SQL/query values, customer/identity/payment data, credentials, stack traces, or provider error text in relay, Loki, B2, alerts, and poison-event records.
- [ ] `[PRODUCTION-ONLY]` Send one bounded synthetic authorized-view audit through the live path and verify transient submission plus receipt-derived `B2_DELIVERED`, deterministic event prefix, one-event object, signed receipt fields, event-ID/digest match, immutable B2 object version/file ID, Loki query copy, version-aware visibility, backlog recovery state, and capacity metric without forcing a live sink outage or invoking break-glass.

### 10.7 Controller

- [ ] Add one read-only endpoint, proposed `GET /api/admin/operations/overview`.
- [ ] Require `SystemOperatorOnly`.
- [ ] Apply the existing API envelope only if it does not obscure partial/stale source semantics.
- [ ] Add strict rate limiting and `Cache-Control: no-store`.
- [ ] Execute the pre-disclosure durable audit acknowledgement and fail-closed/break-glass contract from section 10.6 without logging response content.
- [ ] Return safe reason codes, not provider messages.
- [ ] Do not add POST, PUT, PATCH, DELETE, command, target, SQL, relation, session, or restore endpoints.

### 10.8 Phase 4 Tests

- [ ] Normal all-source success.
- [ ] One-source partial failure.
- [ ] All-source failure.
- [ ] Stale source.
- [ ] Inconsistent/untrusted source.
- [ ] Timeout, oversized response, redirect, malformed JSON, invalid timestamp, and cancellation.
- [ ] Internal-cache freshness and stale transition.
- [ ] No sensitive fields in serialized DTO.
- [ ] Unauthorized Admin, unauthorized SuperAdmin, missing claim, expired recent-auth, and valid SystemOperator matrices.
- [ ] Production startup failure for unsafe configuration.
- [ ] No application-database write during overview reads.
- [ ] Submission success with delayed/failed B2 delivery raises `AUDIT_DEGRADED` and denies new normal overview reads; only a valid receipt derived from an observed B2 object version maps the same event ID/digest to `B2_DELIVERED`.
- [ ] Vector source response, disk-buffer persistence/restart, sink/S3 request success without a receipt, receipt-verifier timeout/outage, stale/forged/replayed/mismatched receipt, prefix pagination/overflow, multiple candidate objects, and B2 failure each deny normal overview access without affecting public/business traffic; a retry reuses the stable event ID and access returns only after authoritative remote-object proof.
- [ ] Break-glass requires Owner-Operator MFA/recent-auth plus incident ID, emits a critical alert, exposes no export, and reconciliation reuses the stable event ID.
- [ ] Duplicate, restart, timeout, quota, poison-event, disk-full, hide-marker, version-list, receipt-signing-key rotation, verifier-cache loss, and B2 recovery tests prove at-least-once delivery with reconstructible B2-derived receipts, duplicate-tolerant/version-aware evidence queries, and no silent drop or name-only false absence.
- [ ] Security-critical mutation plus transactional audit outbox commits or rolls back atomically; process termination cannot silently lose the event, and retries preserve its event ID.

**Phase 4 exit gate (`DBOPS-P4-G1`):** Local Docker automated/contract tests pass and the deployed Test VPS proves the module, test Keycloak, real test relay/sinks, fail-closed/break-glass, outbox, and degraded behavior end to end. Provider details stay internal and monitoring failure cannot affect business endpoints; actual Production identity/audit configuration remains `PRODUCTION-ONLY`.

## 11. Phase 5 - Admin System and Operations Page

**Validation environments:** `[LOCAL][TEST-VPS]` for all frontend states, authorization, accessibility, responsive/browser, stale/untrusted, and safe-link tests. `[PRODUCTION-ONLY]` is a bounded authorized UI smoke against live sanitized sources after backend Production gates pass.

### 11.1 Route and Navigation

Target files:

- `frontend/components/layout/sidebar/nav-main.tsx`;
- `frontend/app/(admin)/dashboard/(auth)/operations/page.tsx`;
- `frontend/app/(admin)/dashboard/(auth)/operations/OperationsPage.tsx`;
- a focused operations API client/hook following existing conventions;
- corresponding tests.

Tasks:

- [ ] Add a top-level `System and Operations` item under the existing System group, separate from Settings.
- [ ] Hide the item when the authorized session lacks the operations claim, while preserving backend enforcement as authority.
- [ ] Add overall status, source freshness, API, Worker, database, pipelines, backup, alerts, and capacity cards.
- [ ] Add last backup and last restore-verification timestamps.
- [ ] Add external links to Grafana/Dokploy/runbooks with `noopener`/safe target handling.
- [ ] Use clear `HEALTHY`, `WARNING`, `CRITICAL`, `STALE`, `UNTRUSTED`, and `UNKNOWN` visual/text states.
- [ ] Include source timestamp and evidence age on every section.
- [ ] Make stale/untrusted states more prominent than the last known metric value.

### 11.2 Explicit UI Exclusions

- [ ] No raw log browser.
- [ ] No SQL/query display.
- [ ] No maintenance button.
- [ ] No session kill/cancel button.
- [ ] No backup or restore button.
- [ ] No credential, internal URL, or infrastructure configuration display.
- [ ] No generic “all systems operational” fallback when fetch fails.

### 11.3 Frontend Tests

- [ ] Healthy, warning, critical, stale, untrusted, unknown, and partial responses.
- [ ] Loading, timeout, authorization failure, and generic safe error state.
- [ ] Evidence age and source labels.
- [ ] External link safety.
- [ ] No maintenance controls in rendered output.
- [ ] Sidebar visibility by role/claim.
- [ ] Responsive desktop/tablet/mobile layout.
- [ ] Accessibility: semantic status text, keyboard navigation, focus, contrast, and non-color-only status.

**Phase 5 exit gate (`DBOPS-P5-G1`):** Local component/build/browser tests and Test VPS authorized browser validation pass across healthy/degraded/stale/unauthorized states. The page is a truthful read-only projection and never pretends to be an independent control plane.

## 12. Phase 6 - Fault Injection, Security, and Recovery Validation

**Validation environments:** `[LOCAL][TEST-VPS]` for the complete destructive fault/security/recovery/load matrix. Production runs only the separately listed safe `PRODUCTION-ONLY` revalidation and must not repeat destructive capacity, corruption, host-loss, disk-fill, or prolonged archive-outage tests.

### 12.1 Degraded-Mode Matrix

| Scenario | Required behavior |
|---|---|
| PostgreSQL unavailable | External alert fires; admin is unavailable or shows database critical/stale; no green fallback |
| PostgreSQL saturated | Exporter/overview backs off; monitoring does not amplify connections; business traffic remains prioritized |
| Prometheus unavailable | Admin shows metrics stale/untrusted; reservation/payment/public endpoints remain unaffected |
| Alertmanager unavailable | Pipeline-health alert is observable through an independent path; admin marks alert state untrusted |
| Loki unavailable | Applications continue; Alloy buffers only within safe limits; dropped-log telemetry/alert is visible |
| Alloy unavailable | Source-silence alert fires; application responses remain unaffected |
| B2 archive temporarily unavailable | Backup/WAL freshness and conservative `oldest_unprotected_wal_age` fail; startup/API/auth/Worker/internal/direct database writes quiesce at 13 minutes, when verified remaining RPO margin is one minute or less, or earlier when `pg_wal` time-to-full is 15 minutes or less; unknown evidence fails safe and only proven-safe reads/no-write recovery access remain available |
| B2 free-capacity gate reached | Combined usage at 8 GB or a retention-horizon forecast above 8 GB blocks acceptance/growth; use only retention-compliant expiry or a separately approved and restore-tested off-host authority; no paid transition or silent retention reduction; quiesce writes before WAL/audit durability is lost |
| Audit relay/B2 audit delivery unavailable | Submission without receipt-derived `B2_DELIVERED` marks `AUDIT_DEGRADED` and denies new normal overview reads; no Vector intermediate state is trusted; break-glass is bounded; public/business traffic continues until bounded mutation-audit storage is threatened |
| Mutation-audit outbox approaches exhaustion | Critical alert fires; each security-critical mutation fails atomically closed if its outbox event cannot commit; general filesystem safety thresholds may quiesce every database mutation path; no silent event deletion |
| Production-equivalent source host lost | Power off or isolate the source Test VPS and its storage/network; restore from the B2 test repository with offline test secrets on a separate recovery host/control plane that shares no source hypervisor, disk, Dokploy control plane, or required network path. A target on the source Test VPS is functional-only evidence |
| Keycloak unavailable | Operations login and overview fail closed; customer/business traffic continues; recovery uses offline infrastructure access |
| Test VPS unavailable or compromised | No Production recovery or strong-auth authority is lost; no Test VPS evidence is accepted as Production proof |
| API unavailable | Grafana/Alertmanager/Dokploy/private runbooks remain directly usable |
| Monitoring host unavailable | External heartbeat/dead-man notification fires through a separately observable path |
| Unauthorized operator | Backend returns forbidden regardless of hidden navigation |
| Owner-Operator unavailable | Automated monitoring/alerts continue; no other person is implied to have authority; non-automated operations wait for the documented owner recovery path and the event records the accepted bus-factor-1 impact |

### 12.2 Security Tests

- [ ] Public port/exposure validation for every operations service.
- [ ] PostgreSQL monitor-role negative privilege matrix.
- [ ] Secret inventory proving no operational secret in source, image layer, log, response, or frontend bundle.
- [ ] SSRF/config validation for monitoring source clients.
- [ ] PII/token canary log test across API, Worker, Next.js, Alloy, and Loki.
- [ ] Authorization and strong-auth matrix.
- [ ] Rate-limit behavior for the overview endpoint.
- [ ] Supply-chain/SBOM/vulnerability checks for new packages and images.
- [ ] Image-tag policy rejects `latest` and records validated digests.
- [ ] Grafana/Prometheus/Loki/Alertmanager anonymous and default credentials disabled.
- [ ] Docker socket or host-log access reviewed and limited.
- [ ] Test VPS inventory proves no Production secret, Keycloak issuer/client credential, recovery material, customer row, authoritative backup, or Production receiver route is present.
- [ ] Destructive Test VPS maintenance-window, snapshot, off-host evidence, resource/timeout abort, unrelated-workload isolation, and recovery procedure are exercised.
- [ ] B2 test-account/bucket credentials are distinct; if an account is shared, tests prove no account-level cap/billing mutation or real threshold-volume allocation and reconcile all test/locked bytes into the 8 GB calculation.

### 12.3 Recovery Tests

- [ ] Point-in-time restore to a controlled timestamp.
- [ ] Immediately after a new weekly full and after retention/expire evaluation, restore the oldest timestamp still inside the rolling 7-day window.
- [ ] Restore after simulated accidental table/data removal in an isolated environment.
- [ ] Restore from the previous recovery point when the latest is unusable.
- [ ] Measured RPO uses the independently timestamped recovery-probe/business-acknowledgement watermark and RTO uses the approved alert/declaration clock; either satisfies the target or creates an explicit blocker. Missing pre-impact watermark evidence yields `RPO=UNVERIFIED`, never `PASS`.
- [ ] Application smoke checks include the controlled synthetic customer reservation flow, the Owner-Operator's sequential self-verification, immutable timestamps, and an explicit record that independent human approval was unavailable.
- [ ] `[TEST-VPS]` Restore from the B2 test repository on a separate recovery host/control plane while the source Test VPS, its local repository/storage/network, and test Keycloak are powered off or unreachable. Prove the target shares no source hypervisor, disk, Dokploy control plane, or required network path. If that topology is unavailable, do not issue `TEST-VPS-PASS` for host loss. `[PRODUCTION-ONLY]` later restores actual Production artifacts without stopping Production.
- [ ] Test the Production-restore target template on the Test VPS: encrypted volume, private ingress, default-deny egress, side effects disabled, Owner-Operator-only access, fixed TTL, aggregate-only evidence, cleanup, and destroyed-volume verification.
- [ ] Quarterly DR checklist includes operator handoff and evidence retention.

### 12.4 Load and Cardinality

- [ ] Run existing k6 request profiles with observability enabled.
- [ ] Compare latency/error/resource baseline before and after instrumentation.
- [ ] Confirm monitoring/database connections remain bounded.
- [ ] Confirm Prometheus series and Loki stream cardinality remain within an approved budget.
- [ ] Confirm log volume and retention capacity projection.
- [ ] Record `pg_database_size`, daily WAL bytes, compressed backup sizes, B2 stored bytes, log bytes/day, Prometheus active series, and metrics bytes/day.
- [ ] Treat the 30-day B2 projection as an early-warning signal only. Prove the combined conservative peak across the rolling 7-day recovery window (including overlapping chains, WAL, and object versions) and the full 3-year immutable-audit horizon remains below 8 GB; also prove PostgreSQL/WAL/local-repository filesystems remain below the 70% warning projection.

**Phase 6 exit gate (`DBOPS-P6-G1`):** all applicable Local Docker checks pass and every material destructive failure path has `TEST-VPS-PASS` evidence. Host-loss evidence passes only from the independent recovery-host topology defined above; a same-host/container-only restore leaves that gate open. No report describes the system as fully secure or Production-safe solely because these tests pass; live-environment claims remain open until their safe `PRODUCTION-ONLY` checks pass.

## 13. Phase 7 - Rollout and Documentation Closure

**Validation environments:** complete Local Docker and Test VPS closure first; then execute only the registered `PRODUCTION-ONLY` checks and record them separately.

### 13.1 Rollout Order

1. Complete every applicable `[LOCAL]` build, test, Compose/config, UI, synthetic backup/PITR, and fault gate; archive `LOCAL-PASS` evidence.
2. Deploy the full stack to the Test VPS with synthetic data, test-only Keycloak/receiver/secrets, and dedicated B2 test buckets/prefixes.
3. Run Phases 1-5 end to end on the Test VPS, including real deployed log/metric/alert/audit/backup/overview/admin paths.
4. Run the complete Phase 6 destructive fault matrix only on Local Docker/Test VPS: B2 block/quota and same-name/hide faults, WAL pressure, all-path database-write quiescence/resume, quiesced restart/auth/startup, host/container/network loss, Vector disk-full, receipt-verifier failure/replay/cache-loss/prefix-overflow, audit backlog, break-glass, and restore faults.
5. Complete two scheduled pgBackRest cycles for each explicit repository, repository-specific check/info/expire evidence, clean-target direct and manifest-reconstructed restores explicitly from both Test VPS repositories, an independent-recovery-host loss drill, measured test RPO/RTO, external receiver delivery, authorization matrix, and browser acceptance.
6. Record `TEST-VPS-PASS` for every phase and observe the Test VPS for at least 14 days before non-safety threshold tuning. A failed or missing Test VPS gate blocks Production work.
7. Provision Production PostgreSQL, pgBackRest, watcher/state, and application images with public ingress, Worker jobs, and all database writes disabled; deploy only the exact revision/digests accepted on the Test VPS.
8. After both repository WAL paths and capacity evidence are healthy, open the short-lived `INITIAL_SCHEMA_BOOTSTRAP` gate with a change ID while ingress/Worker remain disabled; run migrations/required seeds, record evidence, and disable the gate again.
9. Execute the safe `PRODUCTION-ONLY` register in section 13.2 and record evidence separately.
10. Only after every Production gate passes does the Owner-Operator timestamp the gate record and enable normal database writes/public ingress/Worker jobs.
11. Accumulate at least 30 days of Production evidence before any V2 discussion.

Before the greenfield Production cutover, the following behavioral gates must already have `TEST-VPS-PASS`:

1. the dead-man producer/receiver, audit relay, and receiver-side receipt verifier survive the applicable simulated Test VPS application/host/path loss and fail closed when their own path is unavailable;
2. test backup and audit B2 buckets prove Object Lock/API compatibility and least-privilege credentials;
3. pgBackRest all-repository archive evidence, explicit `repo1`/`repo2` check/info/expire, the low-write `archive_timeout` test, two scheduled backup cycles per repository, and combined-account capacity gates pass;
4. clean isolated restores explicitly from local `repo1` and B2 `repo2`, including manifest-pinned reconstruction after a same-name/hide fault and an independent-recovery-host loss drill, meet the applicable RPO/RTO and complete the Owner-Operator checklist;
5. overview relay plus receipt-derived audit proof, verifier-failure/replay, fail-closed/break-glass, and mutation transactional-outbox tests pass;
6. centralized startup/API/auth/direct-command/Worker write-quiescence, quiesced restart, no-write recovery auth, and gap-free resume tests pass;
7. every fault result records that it came from the Test VPS and is not a Production topology claim.

The Test VPS validates the complete implementation and destructive failure behavior. It never stores authoritative Production backup material, Keycloak recovery authority, Production recovery secrets, customer data, or evidence presented as a successful Production restore.

### 13.2 `PRODUCTION-ONLY` Safe Validation Register

Run these items only after `TEST-VPS-PASS`; each produces `PRODUCTION-PASS` or blocks cutover:

- [ ] `DBOPS-PROD-01` - Verify exact live commit/image digests, private networks, DNS/TLS, firewall/ingress, persistent volumes, runtime mounts, sanitized configuration hashes, and public management-port exposure.
- [ ] `DBOPS-PROD-02` - Prove the Operations VPS is actually in the approved different provider/account/region failure domain and the receiver-side dead-man evaluator plus B2 receipt verifier are outside both Production and Operations control planes. Isolate only dedicated Production and Operations canary routes to prove each missed-check alert; do not disconnect, stop, or kill a live host/path.
- [ ] `DBOPS-PROD-03` - Inspect actual PostgreSQL monitor grants/limits/timeouts, archive settings, `archive-push-queue-max` absence, write-safety watcher/state permissions, shared startup/API/auth/Worker/direct-command gate wiring, no-write recovery-auth configuration, and fail-closed Production configuration.
- [ ] `DBOPS-PROD-04` - Verify actual Keycloak issuer/client, MFA/step-up/recent-auth claims, `SystemOperator` mapping, and offline recovery independence using a synthetic operator check.
- [ ] `DBOPS-PROD-05` - Verify actual backup/audit B2 bucket policies, 7-day/3-year Compliance Object Lock, scoped runtime credentials, audit hide-marker/version-aware controls, provider alerts, no-paid-capacity controls, combined stored bytes, 30-day operational forecast, 7-day recovery-window peak, and 3-year immutable-audit forecast using safe test markers only.
- [ ] `DBOPS-PROD-06` - Verify live pgBackRest all-repository WAL freshness plus repository-specific check/info/expire and two scheduled cycles for both `repo1` and `repo2`, watcher heartbeat, and sanitized capacity metrics without blocking B2 or filling `pg_wal`.
- [ ] `DBOPS-PROD-07` - Restore an actual Production `repo2` backup with explicit repository selection into an encrypted ephemeral clean target with private ingress, default-deny egress, Owner-Operator-only access, fixed TTL, and all side effects disabled. Read-only verify the signed accepted-version manifest, historical-file-ID reconstruction procedure, and version-drift monitor; run bounded aggregate/synthetic checks without exporting row values, then verify cleanup and volume destruction.
- [ ] `DBOPS-PROD-08` - Verify one synthetic live overview audit reaches Vector, Loki, and immutable B2, then derive its opaque event prefix from the event ID and validate the bounded one-event lookup plus signed receipt's event ID, digest, object key, version/file ID, timestamp/expiry, and verifier key ID. Validate fail-closed configuration by inspection/safe test seam rather than disabling the live sink.
- [ ] `DBOPS-PROD-09` - Run the authorized read-only Operations UI/API smoke and verify source ages, `no-store`, safe links, and no raw provider/customer/secret data.
- [ ] `DBOPS-PROD-10` - Confirm rollback/runbooks, private Operations Contact Record, evidence locations, and open accepted risks before enabling traffic.

The following are explicitly prohibited in Production validation: deliberate B2/quota exhaustion, `pg_wal` or filesystem fill, 15-minute archive outage, destructive table/data removal, live host kill, real receiver blackhole, Vector disk exhaustion, forced outbox exhaustion, retention reduction, Object-Lock bypass, customer notification/payment side effects, and any restore over the live primary. Their accepted behavioral evidence comes from Local Docker/Test VPS fault tests.

### 13.3 Production Rollback

Rollback order must preserve observability and backups:

- operations UI can be disabled with a fail-closed feature/config switch;
- the overview endpoint can be disabled without removing external Grafana/alerts;
- application metrics instrumentation can be disabled independently if it causes measurable regression;
- JSON stdout should remain even if Alloy/Loki is rolled back;
- exporter can be stopped/revoked without changing application credentials;
- pgBackRest/WAL rollback is prohibited after cutover unless another verified PITR-capable authority is active;
- backup data is never deleted as part of application rollback.

### 13.4 Documentation Sync

- [ ] Update canonical ADR/IDD/TDD/implementation/execution/security/runbook/gate docs.
- [ ] Record exact versions, topology, ports, owners, RPO/RTO, retention, and secret locations by name only.
- [ ] Keep live status and evidence pointers in `docs/10_Execution_Tracking.md`; keep normative decisions and gate wording in `docs/19` and `docs/20` without creating per-phase master-plan duplicates.
- [ ] Add operator runbooks for alert triage, PostgreSQL saturation, low disk, stale backup, WAL failure, log-pipeline failure, and restore.
- [ ] Add runbooks for B2 outage stages, centralized write quiescence/resume, combined 8 GB capacity remediation, audit relay/B2 backlog, audit break-glass reconciliation, mutation-outbox pressure, independent-monitoring loss, and Owner-Operator unavailability.
- [ ] Add screenshots for the admin page after implementation.
- [ ] Add separate Local Docker, Test VPS, and Production evidence timestamps/labels without copying secrets or customer data.

**Phase 7 exit gate (`DBOPS-P7-G1`):** every phase has applicable `LOCAL-PASS` plus mandatory `TEST-VPS-PASS`; each registered live gate has separate `PRODUCTION-PASS`; implementation, Test VPS acceptance, and Production readiness are reported separately and truthfully.

## 14. Automated Validation Matrix

| Area | Local Docker | Test VPS - mandatory | `PRODUCTION-ONLY` safe validation |
|---|---|---|---|
| Backend logging | Targeted middleware/unit tests; full backend build/test; canary redaction | Rerun against deployed API/Worker and captured test Loki records | One synthetic live canary; no customer data |
| Worker logging/metrics | Unit, cancellation, failure, label/cardinality tests | Real scheduled/test jobs, heartbeat, retry/failure telemetry | Safe heartbeat/job-status smoke only |
| Authorization | Policy convention/controller/integration plus Production-startup configuration tests | Test Keycloak MFA/step-up/recent-auth and outage matrix | Actual issuer/client/claim mapping and synthetic operator check |
| Operations module | Adapter/normalization/cache/degraded unit tests; fake HTTP integration | Real test Prometheus/Alertmanager/backup sources and fault states | Read-only live source smoke and safe audit |
| Write admission | Startup/migration/seed, auth/session, endpoint, direct-command, internal-callback and Worker inventories; stale-state; zero-write restart; simulated quiescence/resume | Destructive B2/WAL pressure, quiesced restart/auth negatives, hard-bound quiescence, gap-free manual resume | Inspect live startup/auth/gate configuration and state/permissions; no forced quiescence |
| Audit durability | Schema/digest, relay and receipt adapters, signed-receipt validation, outbox atomicity, and deterministic B2-verifier fixtures | Vector ACK/blocking buffer, remote object/version receipt, verifier outage/replay/cache-loss, B2 test lock, restart/quota/disk-full/duplicate/break-glass | Actual relay/verifier bucket credentials plus one synthetic end-to-end event with B2-derived signed receipt |
| Frontend page | Vitest/Testing Library, lint, build, role/state/accessibility/browser coverage | Authorized/unauthorized responsive browser matrix against deployed API | Bounded read-only live browser smoke |
| Compose/config | Syntax, pins/digests, health, mounts, no-public-port checks | Deployed service health, persistence, restart, resource limits, public exposure scan | Actual live topology/mount/config hash and exposure scan |
| Prometheus | Config/rule tests and synthetic series | Real scrapes, rule firing/recovery, storage/cardinality pressure | Live target/rule health and retention baseline |
| Alertmanager | Config validation and local receiver fixture | Real non-Production receiver, dedup/storm/failure/recovery | Actual independent receiver and safe path-isolation proof |
| Alloy/Loki | Config, ingestion, redaction, buffer/cardinality | Real container/host ingestion, outage/recovery, quota behavior | Live source/retention/credential/path smoke |
| PostgreSQL monitor role | Grant fixture and negative mutation/maintenance tests | Actual Test VPS role/grants/timeouts/load | Inspect actual live role/grants/limits and bounded connection behavior |
| pgBackRest | Fixed `repo1`/`repo2` validation, explicit per-repository backup/check/info/expire fixtures, encrypted repositories, and explicit-repo synthetic PITR/restore | Real B2 test bucket, separate local/B2 scheduled chains, all-repo WAL evidence, Object Lock compatibility, outage/quiescence, and clean restores explicitly from each repository | Actual bucket/policy/WAL freshness, repository-specific scheduled-cycle evidence, plus isolated `--repo=2` restore from Production artifacts |
| Security | Credential/PII canary values, SSRF/auth negatives, dependency/image scans | Public exposure, scoped non-Production identity material, Keycloak/relay/sink faults | Live credential-reference/exposure/identity checks without value disclosure |
| End to end | Full co-located Docker degraded-mode and restore smoke | Full deployed destructive matrix, external alert, admin stale state, measured RPO/RTO | Registered safe Production checklist only |

Repository-wide commands remain:

```text
dotnet restore backend/RentACar.sln --configfile backend/NuGet.Config
dotnet build backend/RentACar.sln --no-restore
dotnet test backend/RentACar.sln --no-build
corepack pnpm -C frontend lint
corepack pnpm -C frontend test
corepack pnpm -C frontend build
```

Tool-specific validation commands must be taken from the pinned component versions and recorded in `ops/*/README.md`; this plan does not hard-code commands that may drift before implementation.

## 15. Operational Runbook Deliverables

Before Production acceptance, provide tested runbooks for:

- monitoring stack unreachable;
- no logs arriving;
- Loki/Prometheus disk pressure;
- PostgreSQL connection saturation;
- long/idle transaction investigation;
- autovacuum or XID warning;
- stale/failed backup;
- WAL archive backlog;
- B2 outage stages and centralized database-write quiescence/resume, including quiesced restart, startup migration/seed suppression, and no-write recovery authentication;
- B2 same-name/hide-marker detection, accepted-version-manifest verification, clean historical-file-ID repository reconstruction, and independent recovery-host restore;
- combined B2 6/7.5/8/9 GB capacity response and restore-tested repository migration;
- audit relay/B2 backlog, receipt-verifier outage/cache rebuild/signing-key rotation, normal-access fail-closed, break-glass reconciliation, and mutation-outbox pressure;
- isolated restore and PITR;
- compromised monitoring/exporter credential;
- revoking `SystemOperator` access;
- rotating B2/runtime/identity credentials with overlap, revocation, the non-secret chain-to-key manifest, retained decrypt-material custody, and an oldest-retained-chain restore after rotation;
- disabling the admin operations feature;
- escalating from admin overview to direct Dokploy/Grafana/`psql` evidence.
- Owner-Operator unavailability and recovery through the private Operations Contact Record.
- exclusive Test VPS destructive-test maintenance, snapshot/abort/resource bounds, off-host evidence, unrelated-workload isolation, and recovery;
- B2 test-resource/account isolation and shared-account no-cap-mutation/no-real-fill restrictions;
- encrypted egress-denied Production-artifact restore, side-effect suppression, TTL, sanitized evidence, cleanup, and destroyed-volume verification.

Each runbook includes trigger, owner, safe first checks, prohibited actions, rollback/recovery, evidence capture, and escalation.

Runbooks are grouped physically so incidents that cross components still have one obvious entry point:

| Runbook file | Required scenario groups |
|---|---|
| `ops/runbooks/monitoring-and-postgresql.md` | monitoring/log pipeline loss, Loki/Prometheus disk pressure, PostgreSQL saturation, long/idle transactions, autovacuum/XID |
| `ops/runbooks/backup-wal-and-quiescence.md` | stale/failed backup, WAL backlog, B2 outage stages, write quiescence/restart/no-write recovery authentication/manual resume |
| `ops/runbooks/b2-object-lock-and-capacity.md` | same-name/hide-marker and manifest checks, 6/7.5/8/9 GB response, retention-compliant repository migration, test-resource isolation |
| `ops/runbooks/restore-and-pitr.md` | isolated restore/PITR, historical-file-ID reconstruction, independent recovery-host drill, Production-artifact isolation/cleanup/destruction |
| `ops/runbooks/audit-delivery-and-break-glass.md` | relay/B2 backlog, receipt-verifier outage/cache/key rotation, fail-closed reads, break-glass reconciliation, outbox pressure |
| `ops/runbooks/identity-credentials-and-owner-recovery.md` | compromised monitoring credential, `SystemOperator` revoke, credential rotation/retained decrypt material, feature disable, escalation, Owner-Operator unavailability |
| `ops/runbooks/test-vps-fault-injection.md` | exclusive maintenance, snapshot, abort/resource bounds, off-host evidence, unrelated-workload isolation, cleanup/recovery |

`ops/runbooks/README.md` assigns each scenario a stable `DBOPS-RB-*` ID, owning file, environment applicability, required gate IDs, lifecycle status (`DRAFT`, `LOCAL-TESTED`, `TEST-VPS-TESTED`, or `PRODUCTION-VALIDATED`), last tested revision/time, and evidence link. `PRODUCTION-VALIDATED` is allowed only for the registered safe checks in section 13.2; destructive behavior remains evidenced by Local/Test VPS runs.

## 16. Security Plan-Gap Verification Mapping

| Plan gap ID | Implementation phases that close it | Required evidence |
|---|---|---|
| `ops-monitor-credential-isolation` | 2, 4, 6 | grants, secrets/config inventory, private network, negative privilege tests |
| `ops-admin-authz` | 4, 5, 6 | policy matrix, MFA/recent-auth, fail-closed Production startup |
| `ops-sensitive-log-data` | 1, 2, 6 | canary tests across final Loki records |
| `ops-audit-durability` | 2, 4, 6, 7 | No intermediate Vector durability state; one event per SHA-256-derived prefix/object; bounded prefix lookup; receiver-side read-only B2 observation; signed event-ID/digest/object-version receipt as the sole `B2_DELIVERED` proof; fail-closed overview; verifier outage/replay/cache-loss/prefix-overflow tests; internal disk-buffer restart behavior; hide-marker detection; 3-year Object Lock; duplicate handling; break-glass; and outbox atomicity/restart tests |
| `ops-backup-recoverability` | 3, 6 | Fixed `repo1`/`repo2` configuration, repository-specific backup/check/info/expire schedules and evidence, all-repository WAL freshness, signed object-to-file-ID/digest manifests, same-name/hide/version-drift detection, direct plus clean manifest-reconstructed restores, independent recovery-host loss drill, and measured RPO/RTO |
| `ops-quiescence-write-coverage` | 3, 6, 7 | Startup migration/seed, auth/session/account, API, direct-command, internal-callback and Worker mutation inventories; quiesced restart; no-write recovery auth; schema-compatible read-only startup; schema-incompatible fail-closed startup; and zero-write/gap-free resume evidence |
| `ops-free-tier-capacity` | 3, 6, 7 | combined-account reconciliation, 6/7.5/8/9 GB alerts, 30-day operational/7-day recovery-window/3-year audit forecasts, quota failure, no-paid-B2 proof, retention-compliant migration, controlled write quiescence, and oldest-in-window restore |
| `ops-recovery-secret-custody` | 3, 6, 7 | scoped-key capability inspection, offline KeePassXC custody record, non-secret chain-to-key manifest, retired decrypt-material retention, staged rotation, runtime-secret-loss recovery, and oldest-retained-chain restore after rotation |
| `ops-strong-auth-colocation` | 4, 6, 7 | Local/Test VPS Keycloak/source-host outage tests plus `PRODUCTION-ONLY` safe live configuration proof, fail-closed operations login, unaffected business auth, and offline recovery evidence |
| `ops-public-management-surface` | 2, 6 | firewall/private-access proof and public exposure test |
| `ops-outbound-source-control` | 4, 6 | configuration validator and malicious URI/redirect tests |
| `ops-pipeline-blindness` | 2, 6 | dead-man alert and source/sink outage tests |
| `ops-single-operator` | all phases, 6, 7 | private contact record, sequential timestamped checks, offline recovery path, quarterly accepted-risk review, and explicit no-independent-review evidence |
| `ops-independent-heartbeat` | 2, 6, 7 | Separate Production/Operations emitters, receiver-side evaluator outside both control planes, Local/Test VPS missed-canary/path-loss simulation, and `PRODUCTION-ONLY` three-failure-domain placement plus safe canary isolation proof |
| `ops-test-environment-isolation` | all phases, 6, 7 | Test VPS secret/data/authority inventory, exclusive maintenance window, snapshot/abort/resource bounds, off-host evidence, unrelated-workload isolation, B2 test-resource separation, shared-account restrictions, and recovery proof |
| `ops-production-restore-data-handling` | 3, 6, 7 | Test VPS validation of the hardened restore-target template and independent recovery-host topology plus `PRODUCTION-ONLY` encrypted/private/default-deny-egress restore, signed-manifest/historical-version procedure inspection, side-effect suppression, aggregate-only evidence, TTL cleanup, and destroyed-volume proof |
| `ops-supply-chain` | all infrastructure phases | pinned versions/digests, SBOM and vulnerability checks |

## 17. Definition of Done

### 17.1 Implementation Complete

- [ ] Phases 0-5 are implemented and every applicable automated/configuration/Compose/browser test has `LOCAL-PASS`.
- [ ] No EvLog or unapproved paid dependency is present.
- [ ] No paid B2 path, finite `archive-push-queue-max`, controller-specific/middleware-only write flag, unqualified single-repository backup schedule, direct controller B2 integration, Vector-as-`B2_DELIVERED` shortcut, or fire-and-forget security audit remains.
- [ ] No V1 database-maintenance execution surface exists.
- [ ] Source/config/security documentation is synchronized; `ops/README.md`, the runbook registry, the database-operations evidence index/template, and `docs/10` ledger entries exist with stable IDs and no private-record leakage.

### 17.2 Test VPS Acceptance Complete

- [ ] Every phase has `TEST-VPS-PASS` at the accepted commit/image digests; no Local-only result closes Test VPS acceptance.
- [ ] Phase 6 destructive fault, security, cardinality, load, and recovery matrix passes on the Test VPS with synthetic data and test-scoped credentials.
- [ ] External non-Production alert delivery, deployed admin stale/untrusted behavior, and public exposure scans are proven.
- [ ] Real test-B2 outage/capacity/same-name/hide-marker faults, all-path database-write quiescence/resume, quiesced restart/auth negatives, explicit local/B2 repository cycles, direct and manifest-reconstructed restores, Vector relay plus event-addressable B2 receipt verifier, overview fail-closed/break-glass, transactional outbox, Object Lock, and gap-free PITR tests pass.
- [ ] The monthly Test VPS recovery procedure has one successful measured clean-target run on the independent recovery host/control plane while the source host is unavailable; a same-host functional restore cannot close this gate.
- [ ] Test VPS evidence is labeled and stored separately and contains no Production secret, customer data, recovery authority, or false Production claim.

### 17.3 Production Ready

- [ ] All Local and Test VPS gates are complete at the exact revision/digests proposed for Production.
- [ ] `[PRODUCTION-ONLY]` Every item in section 13.2 has `PRODUCTION-PASS`; live topology, private network, exposure, failure-domain placement, and receiver are verified.
- [ ] `[PRODUCTION-ONLY]` The private Owner-Operator Contact Record is recoverable; single-person ownership, RPO/RTO, retention, on-call expectation, and bus-factor-1 limitation are signed off.
- [ ] `[PRODUCTION-ONLY]` Production secrets, Keycloak MFA/step-up/recent-auth, `SystemOperator` mapping, and offline recovery independence are configured and safely verified.
- [ ] `[PRODUCTION-ONLY]` B2 backup/WAL and receiver-verified `B2_DELIVERED` audit delivery are active; the relay and read-only verifier use separate scoped identities; accepted-version manifests, version-drift/hide-marker controls, and bounded event-addressable receipt lookup are verified; current bytes plus the 7-day recovery-window and 3-year audit forecasts remain below 8 GB; separate Production/Operations canaries reach the external dead-man evaluator.
- [ ] `[PRODUCTION-ONLY]` Actual Production schedules have produced independently evidenced chains in local `repo1` and B2 `repo2`; a `--repo=2` backup restores to an isolated clean target, the signed manifest and historical-file-ID reconstruction path are safely verified, the oldest B2 in-window target restores after repository-specific retention evaluation, and the authoritative external recovery watermark satisfies the sanitized RPO/RTO/smoke contract without modifying live Production.
- [ ] `[PRODUCTION-ONLY]` Rollback and incident runbooks are validated and no unaccepted/unmitigated high/critical security or recoverability gap remains; the accepted single-operator limitation is not misrepresented as independent review.

These milestones must not be collapsed into one “done” statement.

## 18. Deferred V2 Maintenance Work

This implementation document does not contain V2 tasks because V2 is not approved.

After at least 30 days of evidence, a new proposal may evaluate a separate internal operations runner. That proposal must not reuse the main API's process or credentials and must include typed allowlisted commands, target allowlists, idempotency, lease/heartbeat, advisory locking, uncertain-outcome reconciliation, external audit, step-up authorization, partial-failure recovery, and a fresh security/architecture gate.

Until that proposal is approved, maintenance remains PostgreSQL autovacuum plus private `psql`/pgAdmin/Dokploy runbooks.

## 19. Coverage Note

- **Reviewed:** current .NET logging and middleware, Worker backup configuration, health endpoint, admin policy/navigation, background jobs/audit path, Docker/PostgreSQL version mismatch, existing canonical docs, approved free/open-source component architecture, implementation seams, authorization, credentials, logging privacy, SSRF, retention, failure behavior, recoverability, supply chain, testing, rollout, and rollback.
- **Not reviewed or executed:** current Local Docker runtime, live Test VPS/Dokploy topology, Test/Production firewall, exact independent Operations-host/receiver placement, an actual B2 account or test/Production backup/audit bucket, actual Keycloak deployment, current backup artifacts, real secrets, the private Operations Contact Record, and measured Test VPS/Production volume/performance.
- **Assumptions:** Production is greenfield; the current non-trusted Staging/Dokploy VPS is the Test VPS and is excluded from Production authority; the existing Clean Architecture placement conventions remain; Dokploy/Docker remains; PostgreSQL 18 is the approved major; paid B2 capacity is not approved; one Owner-Operator holds all human roles; V1 operations remains read-only except infrastructure-controlled write admission and durable audit delivery.
- **Tools run:** repository text inspection, current Context7 verification of Vector acknowledgement/disk-buffer/key-prefix behavior, pgBackRest multi-repository behavior, and Backblaze same-name/hide-marker/file-ID version behavior; official PostgreSQL/Keycloak documentation carried from the architecture review; scoped diff consistency analysis; and Codex Sentinel Security Plan Gap. No code implementation, package install, external scanner, container deployment, B2 mutation, runtime test, or Production validation was performed.

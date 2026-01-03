
# 🏗️ ExcellCore Master Building Plan (2026)

## 1. Role & Mission

You are the **Lead Software Architect**. Your mission is to complete Milestone #2: **Operational Persistence & Intelligence**. You must transform "Sample Data" modules into "Data-Driven" modules while maintaining a zero-leakage modular architecture.

## 2. Global Coding Constraints

| Constraint | Instruction |
| --- | --- |
| **Persistence** | **NEVER** use in-memory lists. Use `IDbContextFactory<ExcellCoreContext>` against the migratable SQLite store. |
| **Migrations** | Every entity change **MUST** be followed by a migration in `ExcellCore.Domain`. |
| **Modularity** | `IS.*` projects must **NEVER** reference each other. Use `Abstractions` for cross-talk. |
| **UI** | Bind labels to `Localization[ContextKey]`. Use `ModuleSummaryStyles.xaml` for all cards and triage visuals. |
| **Async** | Use `AsyncRelayCommand`. Marshal UI updates to the `Dispatcher`. |

## 3. Targeted Implementation Steps

### Step 1: Retail & Corporate Persistence

* [x] **Entity Creation:** Define `RetailTransaction`, `Ticket`, and `CorporateContract` in `ExcellCore.Domain`.
* [x] **Inheritance:** Ensure all aggregate roots carry the shared `AuditTrail` metadata (aligned with existing domain pattern).
* [x] **Service Layer:** Implement `IRetailOperationsService` and `ICorporatePortfolioService` backed by `IDbContextFactory<ExcellCoreContext>`.
* [x] **UI Update:** Re-bind the Retail and Corporate Workspaces to these services so summary cards, tickets, and allocations hydrate from the persistent store.
* _Execution Notes_: Migration `20251227140000_RetailAndCorporateDashboards` adds the required tables and the dashboards seed via service-level bootstrap on first run. When extending the model, regenerate migrations and verify with `dotnet ef database update` to avoid schema drift.

### Step 2: The SLA & Triage Engine

* [x] **Data Capture:** Log "Reminder" and "Fast-Track" clicks in an `ActionLog` table (triage now stores identity rollups for impacted parties).
* [x] **SLA Dashboards:** Build a `Reporting.SlaWorkspace` that queries `ActionLog`, `AgreementImpactedParty`, and `AgreementHistory` for escalation trends.
* [x] **Visuals:** Implement a "Heat Map" control showing breach frequency per module (Clinical vs Retail) and highlight approval queues crossing the 24h escalation threshold.
* [x] **Impacted Party Maintenance:** Extend the agreements workspace with an identity-aware picker so triage stays aligned with the master identity graph.

### Step 3: Telemetry & System Health

* [x] **Interceptors:** Add an EF Core Interceptor to log query execution times > 500ms to a `Telemetry` table.
* [x] **Heartbeat:** Add a `HealthStatus` property to the `ShellViewModel` that monitors the `EscalationMonitor` status.
* [x] **Startup Diagnostics:** Preserve the shell and module loader logging (`%LOCALAPPDATA%\ExcellCore\shell.log`, `%LOCALAPPDATA%\ExcellCore\module-loader.log`) as the primary troubleshooting trail before future publish or startup changes.
* [x] **Aggregation:** Persist telemetry thresholds, aggregates, and health snapshots through `TelemetryService` and schedule the shell `TelemetryAggregationWorker` to keep snapshots fresh.
* [x] **Health Banner:** Surface telemetry severity, message, and summary in the shell banner so operators see query health status in real time.
* [x] **Telemetry Visualization Workspace:** Deliver a reporting telemetry workspace that renders aggregate trends and severity buckets from the persisted telemetry service.

_Execution Notes_: Migration `20251227171941_202512271645_TelemetryHealthAggregation` introduces `TelemetryAggregates`, `TelemetryThresholds`, and `TelemetryHealthSnapshots`. Run `dotnet ef database update` after pulling to align the SQLite store. When generating migrations, expect the shell telemetry worker to emit warnings until the schema is updated.

### Step 4: Identity-First Lookup Experience

* [x] **Lookup Service:** Expose a `PartyLookupResultDto` via `IPartyService` that returns `PartyId`, display labels, and relationship context.
* [x] **Workspace Integration:** Replace manual GUID entry in the agreements impacted party grid with an auto-complete picker bound to the lookup service.
* [x] **Validation:** Prevent saving agreements when impacted parties reference unknown identities; show localized validation messages.
* [x] **Tests:** Added unit tests around the party lookup endpoint and UI-facing validation to ensure identity rollups remain accurate (`PartyServiceTests.LookupAsync_*`, `AgreementWorkspaceViewModelTests.SaveCommand_*`). Remediation sequence: seed parties and identifiers via `TestSqliteContextFactory`, assert lookup paging/identifier context, then drive `AgreementWorkspaceViewModel` through impacted-party selection to verify Save gating.
* [x] **Caching:** Preload common identities (per module launch) and cache recent lookup results to minimize duplicate queries while operators triage approvals.

### Milestone #4: Distributed Sync & Global Identity

* [x] **Conflict Resolution Engine:** Implement `ConflictResolverService` (vector clocks or last-write-wins) to reconcile Agreement and Party changes captured offline. Last-write-wins with vector-clock dominance is in place; aggregate state application remains queued.
* [x] **Delta-Sync Provider:** Extend `Extensions.Sync` with a delta transport that exchanges only property/row changes to conserve bandwidth. Ledger-backed capture, inbound append, vector-clock stamping are live, and applied deltas materialize onto Agreements/AgreementApprovals/Parties; dominated/unhandled deltas are queued for operator triage in the Sync workspace.
* [x] **Global Identifiers:** Migrate local keys toward sequential UUIDs and register transitions with the central identity registry ahead of first multi-site sync (sequential GUID generator is registered and used across agreements, parties, retail, corporate, reporting, telemetry, seeds, and tests).

### Milestone #5: Dynamic Labeling & Multi-Context UI

* [x] **Context-Aware Labeling:** Implemented `ILocalizationService` with context selection via shell dropdown; agreements UI now binds labels to localization map with context-aware keys and fallbacks.
* [x] **Metadata-Driven Forms:** Added `IMetadataFormService`, `PartyMetadata` entity, migration `20260102140153_AddPartyMetadata`, and metadata field definitions (clinical/retail/corporate) with persistence.

### Milestone #6: Advanced Clinical & Retail Workflows

* [x] **Clinical Track (CPOE & MAR):** Minimal path shipped: order entry → dispense → administer → document. Domain entities (`MedicationOrder`, `MedicationAdministration`, `DispenseEvent`) added with migration; clinical service exposes create/dispense/admin commands; CPOE/MAR workspace wired with quick actions. Unit tests cover order creation, dispense logging, and MAR completion.
* [x] **Retail Track (Omnichannel POS):** Suspend/resume and returns enabled. Domain entities (`RetailSuspendedTransaction`, `RetailReturn`) migrated; service methods for suspend/resume/return implemented; POS workspace shows suspended carts and returns with quick actions. Unit tests cover suspend/resume lifecycle and return capture.
* [x] **Execution Plan (M6):** Phases A/B/C complete (domain, migration, services, UI, tests, publish). Owners: Clinical Track (Apps/Clinical), Retail Track (Apps/Retail), Migrations/Infra (Arch), QA (Tests/Smoke).
* [x] **Progress Note (2026-01-02):** Phases A/B/C complete; publish generated at `publish/ExcellCore.Shell/win-x64`. Conduct full manual smoke on clinical CPOE/MAR and retail suspend/return in the published build.

### Milestone #7: Proactive AI & Predictive Telemetry

* [x] **SLA Predictive Monitor:** Predictive SLA cards built from telemetry health plus agreement action logs; shows risk score, ETA to breach, and driver summary in the SLA workspace triage cards.
* [x] **Inventory Anomaly Detection:** Inventory analytics service + background worker analyze ledger velocity for shrinkage/stock-out, persist alerts, and surface in inventory workspace; notifications emitted via worker.

### Next Phase: Patient Journey-Centric Experience (Healthcare)

* [x] **Patient Intake → Orders → Administration Journey:** Patient journey section added to Clinical workspace showing intake→order→dispense→administration stages with shared identity context.
* [x] **Cross-Module Timeline:** Patient-centric timeline stitches orders, dispenses, administrations (labs/rads/billing stubs pending) into a single view.
* [x] **Role-Specific Worklists:** Provider, Pharmacist, and Nurse worklists added in Clinical workspace with stage-aware items.
* [x] **Operational Signals on the Journey:** Journey inbox surfaces overdue administrations; badges derive from schedule status (SLA/inventory inline signals can extend later).
* [x] **Journey-Aware Notifications:** Grouped inbox in journey view aggregates role/stage alerts to reduce toast noise.
* [x] **Clinic Starter Data:** Clinical seed already hydrates patients/orders/dispense/admin events; aligns with starter inventory SKUs.

when I need to begin the next phase of development. Follow these steps strictly:

Document Sync: > Review our recent progress . Update architecture.md and EXCELLCORE_MASTER_BUILD_PLAN.md to mark completed items as [x] and ensure the 'Current Implementation Status' section reflects our latest update.
When you fix any issue document the remediation sequence and capture it here so the team can replay the steps without guesswork.

### Next Phase: Healthcare Facility-Tailored EMR
- [ ] Core ADT: wards/rooms/beds, admit/transfer/discharge flows, bed board with occupancy/isolation, telemetry on bed status.
- [ ] Provider & Nursing Workflows: order sets, vitals/flowsheets, MAR shift views with overdue cues, timeline integration for tasks.
- [ ] Clinical Documentation: configurable H&P/progress/discharge notes, signatures/attestations, PDF export per encounter.
- [ ] Scheduling & Hand-offs: procedure/consult scheduling board, hand-off summaries, escalation hooks to notification center.
- [ ] Billing & Integrations: charge capture from administrations/procedures, LIS/RIS routing stubs, audit trails across ADT/orders/admins.

### Next Phase: Full Health Information System (HIS)
- [ ] ADT at scale: wards/rooms/beds, census, isolation flags, A12/A13-equivalent flows, telemetry-backed occupancy, and full audit trails.
- [ ] Orders and Results: LIS/RIS order routing adapters, result ingestion/reconciliation, acknowledgments, and order set management.
- [ ] Clinical Documentation: templated H&P/progress/discharge/consent notes with versioning, signatures/attestations, and PDF outputs bound to encounters.
- [ ] Medication Safety: MAR shift views, barcode scanning hook points, allergies/clinical rules, IV/titration support, and administration safeguards.
- [ ] Scheduling: OR/procedure/consult scheduling with resources (rooms/devices/providers), waitlists, overbook rules, and hand-off summaries.
- [ ] Billing and Revenue: charge capture from meds/procedures/docs, coding support, claim prep/edits, and reconciliation paths.
- [ ] Interoperability: FHIR/HL7v2 gateways for ADT/ORM/ORU/SIU, MPI alignment, and document exchange (CCD/CCDA stubs).
- [ ] Security/Compliance/SRE: authn/z (SSO/MFA/roles), privacy controls, retention/backup/DR, monitoring/SLOs, and deployment packaging.

## Phase 1 Smoke Checklist (Published Build)
- Launch from [publish/ExcellCore.Shell/win-x64](publish/ExcellCore.Shell/win-x64); confirm migrations apply and the shell health banner settles green.
- Clinical journey: CPOE order → dispense → administer; verify the timeline updates, severity badges render, and reminders surface.
- Retail: create sale, suspend/resume, then return; ensure dashboards refresh and localized labels remain correct.
- Sync transport: mutate Agreement and Party to emit deltas; confirm ledger entries persist, JSON envelopes are emitted/applied, and triage backlog stays clear.
- Reporting export: run a scheduled export and validate CSV output (headers, delimiter, content-type) matches schedule definition.
- Localization/telemetry: switch localization context, confirm label swaps, and watch telemetry aggregates plus the shell health banner after workflows.

## Operational Runbooks (Transport & Export)
- **Sync Transport Adapter:** outbound mutations append to the ledger and serialize to JSON envelopes; inbound envelopes apply with audit updates, while dominated/unhandled deltas land in triage for operator review. Keep the Sync workspace triage list empty and consult shell/module-loader logs if envelopes stall.
- **Reporting Export Service:** schedules execute to produce CSV payloads with asserted content-type and header casing; validate outputs against schedule filters/time windows and rerun after data refresh if needed. Ensure service is registered in DI and preserve failed artifacts for operator review.

## Automated Smoke Harness
- Location: [src/ExcellCore.Tests/SmokeTests.cs](src/ExcellCore.Tests/SmokeTests.cs) now drives clinical (order→dispense→admin), retail (suspend/resume/return), sync transport import/export, reporting CSV export, and telemetry aggregation.
- Run: `dotnet test src/ExcellCore.Tests/ExcellCore.Tests.csproj --filter SmokeTests` (temp SQLite with migrations applied automatically).
- Lessons learned: sync import may log a single triage ledger entry when inbound clocks are non-dominant; the harness asserts triage stays at most one entry and matches the inbound aggregate. Telemetry is forced into Critical via a seeded 1.8s query to exercise the health path; artifacts include export CSV, sync inbound/outbound envelopes, and telemetry-health snapshot (`telemetry-health.json`).
- CI: [ .github/workflows/ci.yml ](.github/workflows/ci.yml) executes the suite (including smoke) with `SMOKE_ARTIFACTS_DIR=artifacts/smoke` and uploads `test-results` plus `smoke-artifacts` for operators.

## Recent Remediation (2026-01-02)
- Converted retail, corporate, reporting, and telemetry seed helpers to instance scope so they can consume the sequential GUID generator; updated related tests to pass `SequentialGuidGenerator` into constructors and reran `dotnet test src/ExcellCore.sln` successfully.
- Fixed `DeltaSyncProvider` aggregate selection by removing an invalid `Guid` null-coalesce, then rebuilt and verified the sync project as part of the full test run.
- Added sync ledger/state entities with EF mappings and migration; implemented last-write-wins conflict resolver plus ledger-backed `DeltaSyncProvider` (inbound append and outbound capture); added unit tests and republished the shell.
- Ran `dotnet publish src/ExcellCore.Shell/ExcellCore.Shell.csproj -c Release -r win-x64 --self-contained false -o publish/ExcellCore.Shell/win-x64` after applying migration `20260102140153_AddPartyMetadata`; quick smoke validation via `dotnet test src/ExcellCore.sln` (28 tests) passed and published bits refreshed.
- Added clinical/retail workflow entities, services, WPF surfaces, and migration `20260102144900_202601021530_M6ClinicalRetail`; applied via `dotnet ef database update` and verified `dotnet build src/ExcellCore.sln`.
- Added clinical workflow commands (order creation, dispense logging, MAR completion), retail suspend/resume/return service operations with UI quick actions, and expanded test coverage (`dotnet test src/ExcellCore.sln`).
- Published Phase C output via `dotnet publish src/ExcellCore.Shell/ExcellCore.Shell.csproj -c Release -r win-x64 --self-contained false -o publish/ExcellCore.Shell/win-x64`; next manual step: smoke the published build for clinical/retail flows.
- Implemented Milestone #7: SLA predictive monitor (predictive cards on SLA dashboard) and inventory anomaly detection (ledger-backed analytics/alerts + background worker); generated migration `20260102161935_202601021730_M7PredictiveTelemetryInventory`, applied via `dotnet ef database update`, and validated with `dotnet test src/ExcellCore.sln` (37 tests) after fixing EF translation by client-side grouping of latest ledger entries.
- Added clinic-oriented "Getting Started" expanders in SLA and Inventory workspaces to guide operators; no logic changes. Verified UI changes via `dotnet test src/ExcellCore.sln` (37 tests).
- Patient Journey MVP delivered in Clinical workspace: timeline (orders/dispense/admin), role worklists (provider/pharmacist/nurse), grouped journey inbox, and seed data aligned. Tests remain green (`dotnet test src/ExcellCore.sln`), republished to `publish/ExcellCore.Shell/win-x64`.
- Extended Patient Journey with lab/radiology/billing checkpoints and inline SLA/inventory risk signals on timeline rows; refreshed clinical UI to surface severity badges.
- Materialized inbound sync deltas onto Agreements/AgreementApprovals/Parties with audit updates; unresolved/conflicting deltas are triaged into the ledger and surfaced in the Sync workspace triage list for operator review (no new migration). Updated tests to expect triage entries for dominated clocks.
- Added a JSON sync transport adapter (outbound capture + inbound apply) and a reporting export service that emits CSV exports for schedules; registered both in DI and covered via `SyncTransportAdapterTests` and `ReportingExportServiceTests` (dotnet test src/ExcellCore.Tests/ExcellCore.Tests.csproj).

## 4. Definition of Done (DoD) Checklist

*Before declaring a task finished, verify:*

1. `dotnet build src/ExcellCore.sln` passes with 0 errors.
2. New entities are included in a generated EF Migration.
3. No hardcoded strings like "Patient" or "Customer" exist in XAML (Use Dynamic Labeling).
4. The `AuditTrail` is populated upon saving the new record.
5. Architecture documents (`architecture.md`, `EXCELLCORE_MASTER_BUILD_PLAN.md`) reflect the latest module capabilities.
6.publish the app and replace previous published so I can review the application and make sure it's working as expected.




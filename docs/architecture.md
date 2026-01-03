# ExcellCore Modular ERP - Architecture Blueprint

## 1. Vision and Scope
ExcellCore is a modular, identity-first ERP platform that ships as a Windows desktop application. A single core runtime delivers foundational capabilities (identity, agreements, billing, inventory) while industry-specific features are plugged in through snap-in modules that can be enabled or disabled per installation. A dynamic labeling layer allows the system to remap terminology ("Patient" vs "Client") without touching feature code.

## 2. Guiding Principles
- **Identity First:** Every transaction begins with identifying the involved parties and the relationship context (direct, insurance, agreement).
- **Modularity:** Features are packaged as independent assemblies that register capabilities with the core container.
- **Offline First:** All critical flows work without network connectivity; synchronization providers are optional extensions.
- **Configurability:** UI and business rules adapt through metadata, not code forks.
- **Security and Audit:** Strong authentication, authorization, and immutable audit trails are core, not optional extras.

## 3. High-Level Architecture
```
+--------------------------------------------------------------+
|                   ExcellCore Desktop Shell                   |
|  (WPF/WinUI Shell, Navigation, Theme, Dynamic Labels)        |
+----------------------+--------------------+------------------+
|   Identity Services  | Agreement Engine   | Module Host      |
|  (Registry, Search)  | (Pricing, Rules)   | (MEF/DI Loader)  |
+----------+-----------+---------+----------+---------+--------+
|  Data Access Layer   |  Event Bus & Jobs  |  Sync Providers  |
+----------------------+--------------------+------------------+
|                 Local Data Store (SQLite/SQL CE)             |
|                 Optional Remote Services API                 |
+--------------------------------------------------------------+
```

### Layers
1. **Desktop Shell:** Provides navigation, window management, styling, and dynamic labeling. Hosts module views in regions.
2. **Core Services:** Shared services for identity registry, agreement engine, inventory kernel, financial engine, and security.
3. **Module Host:** Discovers and loads industry-specific modules, applying license and configuration filters.
4. **Integration Layer:** Background services for synchronization, reporting exports, and notifications.
5. **Persistence:** Local relational store (SQLite by default, pluggable provider abstraction) plus optional remote sync endpoints.

## 4. Module Map
| Module ID | Purpose | Key Components |
|-----------|---------|----------------|
| Core.Identity | Master Identity Registry | Party repository, dedupe service, unified profile UI |
| Core.Agreements | Agreement Engine | Pricing rules, payer hierarchy, rider management |
| Core.Inventory | Inventory Kernel | Stock ledger, reservation service, location management |
| Core.Financials | Accounting & Payments | Chart of accounts, invoicing, payment capture |
| IS.Clinical | Healthcare workflows | HIS admission, LIS orders, PIS dispensing |
| IS.Retail | POS and storefront | Fast billing screen, receipt engine, loyalty |
| IS.Corporate | B2B operations | Contract billing, project allocations |
| Extensions.Sync | Optional syncing | Push/pull adapters, conflict resolution |
| Extensions.Reporting | Analytics & BI | Prebuilt dashboards, export jobs |

### Current Implementation Status (January 2026)
- **Shell and Infrastructure:** WPF shell boots all core and industry assemblies, merges shared summary-card styles, hosts MVVM view models through DI, and continues to provide the event bus, notification center, background escalation monitor with surfaced status banner, slow-query telemetry capture via an EF Core interceptor, persisted telemetry aggregates and thresholds with a background worker feeding the health banner, startup diagnostics that keep the published build instrumented, and a shell-level localization context selector feeding dynamic labels. “Getting Started (Clinic)” overlays were added to SLA and Inventory workspaces for quicker onboarding.
- **Identity Module:** Dashboard backed by `IPartyService`, supporting identifier management, detail editing, refreshed search workflows against the EF repository, and graph rollups that feed downstream workspaces.
- **Agreements Module:** End-to-end pricing workspace with rate authoring, coverage metadata, approval tracking, renewal scheduling, centralized pricing calculator, workflow validation diagnostics, escalation notifications, proactive triage cards with reminder/fast-track actions, identity graph rollups with cached party lookups, action logging persisted to the shared store, dashboard metrics, persistent EF-backed repository, pricing history timeline sourced from `AgreementService`, SLA triage analytics with heat-map buckets and reminder/fast-track summaries, unit coverage enforcing impacted-party identity selection before saves, and context-aware localization bindings driven by `ILocalizationService` and metadata-backed form definitions persisted via `PartyMetadata`.
- **Identity & Action Logging:** Reminder and fast-track actions write to the persistent action log with identity relationships resolved from the master registry, enabling triage reporting without duplicate queries.
- **Inventory Module:** Persisted stock ledger with location/search filters, summary metrics, and anomaly analytics (shrinkage/stock-out) driven by a background worker; alerts surface in the workspace with action hints and last-analyzed timestamp.
- **Financial Module:** Invoice list, status filters, cashflow snapshots, and toggleable summary cards aligned to shared styles.
- **Clinical Module:** Admissions dashboard plus CPOE/MAR surfaces backed by clinical workflow service; orders, dispenses, and administrations hydrate from persisted `MedicationOrder`, `DispenseEvent`, and `MedicationAdministration` entities seeded at first run.
- **Retail Module:** Persists transactions and tickets through `IRetailOperationsService`; dashboards hydrate from the SQLite store with service-level bootstrap seeding for first-run demo data, now including suspended carts and returns panels bound to `RetailSuspendedTransaction` and `RetailReturn` entities.
- **Corporate Module:** Contract backlog and allocation alerts persisted via `ICorporatePortfolioService` to the shared context.
- **Reporting Module:** Analytics dashboards, export schedules, an SLA workspace with breach heat maps, escalation queue, and predictive SLA cards (risk score, ETA, driver, action hint) plus a telemetry workspace tab using shared summary visuals, all backed by persisted metrics hydrated through domain services.
- **Sync Foundations:** Delta sync provider now runs from the sync change ledger, emitting vector-clocked `SyncDelta` payloads; inbound deltas are conflict-checked, materialized onto Agreements/AgreementApprovals/Parties when applicable with audit updates, and triaged into the ledger when dominated/unhandled, with a Sync workspace triage list for operator review.
- **Sync Transport:** JSON sync transport adapter packages outbound deltas into envelopes and applies inbound results; registered in DI with coverage via Sync transport tests.
- **Identifiers:** Sequential GUID generator is registered and used across agreements, parties, retail, corporate, reporting, and telemetry services (including seeds and tests) to prepare for multi-site sync and reduce index fragmentation.
- **Shared Styling:** Module summary cards, label tones, and typography centralized in `Styles/ModuleSummaryStyles.xaml` and applied across core and industry workspaces.
- **Database Migrations:** Core data layer managed through EF Core migrations with runtime application via context factory; latest migration `20260103170000_202601031715_M8AdtFoundation` adds wards/rooms/beds tables and indexes for ADT flows (previous: `20260102161935_202601021730_M7PredictiveTelemetryInventory`).
- **ADT Foundation:** Wards/rooms/beds entities, ADT service (admit/transfer/discharge), initial bed-board seeding, and occupancy telemetry capture landed with Sprint 1.
- **Operational Diagnostics:** Desktop runtime persists startup traces to `%LOCALAPPDATA%\ExcellCore\shell.log` and module loading traces to `%LOCALAPPDATA%\ExcellCore\module-loader.log`; keep these logs when debugging publish issues or health-banner regressions.
- **Build Health:** `dotnet build src/ExcellCore.sln` and `dotnet test src/ExcellCore.sln` pass (37 tests including clinical/retail workflows and inventory analytics); modules load via manifests for manual testing.

Modules implement `IModuleDescriptor` and register views, commands, and services through dependency injection.

## 5. Data Model Overview
- **Party** (`PartyId`, `Type`, `Demographics`, `Identifiers[]`)
- **Agreement** (`AgreementId`, `Payer`, `CoverageRules`, `Rates[]`, `Validity`)
- **Encounter/Transaction** (`TxnId`, `PartyId`, `Module`, `LineItems[]`, `AgreementContext`)
- **InventoryItem** (`ItemId`, `Sku`, `Batch`, `Locations[]`, `QuantityOnHand`)
- **FinancialDocument** (`DocId`, `Type`, `Amount`, `Status`, `LinkedTxn`)

The `ExcellCoreContext` EF Core DbContext coordinates every aggregate and applies a shared `AuditTrail` value object rather than a classical `BaseEntity`. AuditTrail captures `CreatedOnUtc`, `CreatedBy`, `ModifiedOnUtc`, `ModifiedBy`, and `SourceModule` for immutable history across modules.

All entities share audit metadata (created, modified, source module). Dynamic labeling is stored in a `Localization` table referencing `ContextKey` values consumed by the UI.

## 6. Technology Stack (Initial Proposal)
- **Language and Runtime:** .NET 8, C#.
- **UI Framework:** WPF with MVVM pattern; optional migration path to WinUI if needed.
- **Dependency Injection:** Microsoft.Extensions.DependencyInjection with module discovery via MEF-compatible exporters.
- **ORM/Data Access:** Entity Framework Core with provider abstraction (SQLite default, SQL Server optional).
- **Configuration:** JSON profiles per deployment, merged with module manifests.
- **Background Jobs:** Hosted services using `IHostedService` inside the desktop process.

## 7. Solution Structure (Planned)
```
src/
  ExcellCore.sln
  ExcellCore.Shell/                 // WPF shell hosting navigation and resources
    Styles/ModuleSummaryStyles.xaml // shared summary card brushes and styles
  ExcellCore.Infrastructure/
  ExcellCore.Domain/
  ExcellCore.Module.Abstractions/
  ExcellCore.Module.Core.Identity/
  ExcellCore.Module.Core.Agreements/
  ExcellCore.Module.Core.Inventory/
    Commands/                       // AsyncRelayCommand helpers
    ViewModels/InventoryWorkspaceViewModel.cs
  ExcellCore.Module.Core.Financials/
    Commands/
    ViewModels/FinancialsWorkspaceViewModel.cs
  ExcellCore.Module.IS.Clinical/
    Commands/
    ViewModels/ClinicalWorkspaceViewModel.cs
  ExcellCore.Module.IS.Retail/
  ExcellCore.Module.IS.Corporate/
  ExcellCore.Module.Extensions.Sync/
  ExcellCore.Module.Extensions.Reporting/
  ExcellCore.Tests/
```

## 8. Next Steps
1. Sprint 2 – Orders & Results: define LIS/RIS order routing interfaces and JSON/HL7 stub adapters; add result ingest with ACK/ERR logging; introduce order-set definitions and a migration to persist orders/results.
2. Provider & nursing workflows: order sets, vitals/flowsheets, MAR batching with shift views, and overdue task cues in the journey timeline.
3. Clinical documentation: configurable note templates (H&P, progress, discharge), signature/attestation, and PDF export tied to encounters.
4. Scheduling & hand-offs: inpatient schedule board (procedures/consults), hand-off summaries, and escalation hooks into notifications.
5. Integration & billing: charge capture from administrations/procedures, LIS/RIS routing stubs, and audit-ready trails across ADT/orders/admins.

## 11. Phase 1 Smoke Checklist (Published Build)
- Launch from [publish/ExcellCore.Shell/win-x64](publish/ExcellCore.Shell/win-x64); allow migrations to apply, confirm shell health banner shows green on first refresh.
- Clinical journey: create CPOE order → dispense → administer, verify timeline updates and severity badges render; confirm reminders/alerts still surface.
- Retail: create sale, suspend/resume, then execute a return; ensure dashboards refresh and receipts display localized labels.
- Sync transport: edit an Agreement and Party to emit a delta; verify ledger entry persists and JSON envelope is produced/applied without triage backlog growth.
- Reporting export: trigger a scheduled export and confirm CSV output matches schedule columns and content-type expectations.
- Localization/telemetry: switch localization context, validate label swaps; monitor telemetry aggregates and shell banner severity after a few workflows.

## 12. Operational Runbooks (Transport & Export)
- **Sync Transport Adapter:**
  - Outbound: Agreement/Party mutations append to the ledger and serialize into JSON envelopes; confirm envelopes logged by the sync worker and that vector clocks increment.
  - Inbound: Apply received envelopes, materialize entities with audit updates, and check triage ledger for dominated/unhandled deltas; resolve triage items before requeue.
  - Health: Use the Sync workspace triage list to ensure backlog stays empty after apply; review shell and module-loader logs if envelopes stall.
- **Reporting Export Service:**
  - Execute or schedule exports from Reporting; expect CSV payloads with correct delimiter/header casing and content-type asserted by tests.
  - Validate outputs against schedule definition (filters/time window) and confirm the service is registered in DI.
  - Capture failures with schedule metadata and rerun after verifying data freshness; persist artifacts alongside the operator review package.

## 13. Automated Smoke Harness
- Location: [src/ExcellCore.Tests/SmokeTests.cs](src/ExcellCore.Tests/SmokeTests.cs); scenarios cover clinical journey (order→dispense→admin), retail suspend/resume/return, sync transport import/export, reporting CSV export, and telemetry aggregation.
- Run: `dotnet test src/ExcellCore.Tests/ExcellCore.Tests.csproj --filter SmokeTests` from repo root; uses per-test temp SQLite via `TestSqliteContextFactory` with migrations applied automatically.
- Lessons learned: sync import can log a single triage ledger entry when inbound clocks lack dominance; smoke asserts the triage list stays at most one entry and pins it to the inbound aggregate to avoid backlog regressions.
- Artifacts: export CSV, sync inbound/outbound envelopes, and telemetry-health snapshot (`telemetry-health.json`); telemetry critical path seeded via a single 1.8s query event to prove banner severity wiring.
- CI: [ .github/workflows/ci.yml ](.github/workflows/ci.yml) runs the full suite (including smoke) with `SMOKE_ARTIFACTS_DIR=artifacts/smoke`, uploading `test-results` and `smoke-artifacts` for inspection.

## 14. Next Phase: Healthcare Facility EMR Scope
- Core ADT: wards/rooms/beds, bed board, admit/transfer/discharge flows, occupancy and isolation flags feeding telemetry.
- Clinical workflows: order sets, vitals/flowsheets, MAR shift views, overdue task cues, and journey timeline integration.
- Documentation: configurable notes (H&P, progress, discharge) with signatures, attestations, and PDF export per encounter.
- Scheduling & hand-offs: procedure/consult scheduling board, hand-off summaries, escalation routing via notification center.
- Billing & integrations: charge capture from meds/procedures, LIS/RIS routing stubs, audit-ready trails across ADT/orders/admins.

## 15. Next Phase: Full Health Information System (HIS)
- ADT at scale: wards/rooms/beds with census, isolation, and telemetry-backed occupancy; A12/A13-equivalent flows and audit trails.
- Orders and results: LIS/RIS order routing adapters, result ingestion/reconciliation, acknowledgments, and order set management.
- Clinical documentation: templated H&P/progress/discharge/consent notes with versioning, signatures/attestations, PDF outputs bound to encounters.
- Medication safety: MAR shift views, barcode-scanning hook points, allergies/clinical rules, IV/titration support, and admin safeguards.
- Scheduling: OR/procedure/consult scheduling with resources (rooms/devices/providers), waitlists, overbook rules, and hand-off summaries.
- Billing and revenue: charge capture from meds/procedures/docs, coding support, claim prep/edits, and reconciliation paths.
- Interoperability: FHIR/HL7v2 gateways for ADT/ORM/ORU/SIU, MPI alignment, and document exchange (CCD/CCDA stubs).
- Security/Compliance/SRE: authn/z (SSO/MFA/roles), privacy controls, retention/backup/DR, monitoring/SLOs, and deployment packaging.

## 16. HIS Delivery Sprints (Plan)
- Sprint 1 – ADT Foundation (Owner: Arch/Clinical): wards/rooms/beds entities + migration; ADT service (admit/transfer/discharge) with audit trails; bed board VM with occupancy/isolation telemetry. AC: A12/A13-equivalent events persisted; bed board reflects census; telemetry emits occupancy metrics.
- Sprint 2 – Orders & Results (Owner: Clinical Integration): LIS/RIS order routing interfaces and stubs; result ingestion with ACK/ERR; order set definitions. AC: place order → serialized envelope; ingest ORU maps to order, marks status, records acknowledgment.
- Sprint 3 – Clinical Documentation (Owner: Clinical Apps): note templates (H&P/progress/discharge/consent) with versioning, signatures/attestations, PDF export bound to encounter. AC: create/attest note with version increment; PDF stored with audit.
- Sprint 4 – MAR/Medication Safety (Owner: Pharmacy/Clinical): MAR shift views, overdue cues, barcode scanning hook points, allergy/clinical rules checks, IV/titration support. AC: administration logged only after rule check passes; overdue queue populates; barcode hook callable.
- Sprint 5 – Scheduling & Hand-offs (Owner: Clinical Ops): procedure/consult scheduling with resources (room/device/provider), waitlist/overbook rules, hand-off summaries. AC: schedule prevents resource collisions; hand-off includes recent vitals/orders.
- Sprint 6 – Billing/Revenue (Owner: Revenue): charge capture from administrations/procedures/docs, coding support, claim prep/edits, reconciliation. AC: admin/procedure triggers charge entry; claim export stub produced; edits audited.
- Sprint 7 – Interoperability (Owner: Integration): FHIR/HL7v2 gateways for ADT/ORM/ORU/SIU, MPI alignment, CCD/CCDA stub export. AC: ADT in/out processed; identity reconciled; CCD generated per encounter.
- Sprint 8 – Security/Compliance/SRE (Owner: Platform): authn/z (SSO/MFA/roles), privacy controls, retention/backup/DR, monitoring/SLOs, deployment packaging. AC: role-based access enforced; backups/restores validated; health endpoints monitored.

### Sprint 1 Detail – ADT Foundation
- Deliverables: `Ward`, `Room`, `Bed` entities + migration; ADT service (`Admit`, `Transfer`, `Discharge`) with audit; bed board VM showing occupancy/isolation; telemetry worker emitting occupancy metrics.
- Tests: service-level ADT commands persist audit; bed board query reflects occupancy; telemetry snapshot includes occupancy metric with expected counts.

### Sprint 2 Detail – Orders & Results
- Deliverables: LIS/RIS order routing interfaces and JSON/HL7 stub adapter; order set definitions; result ingestion pipeline with ACK/ERR logging.
- Tests: order placement serializes envelope; ORU ingest updates order status and records acknowledgment; order set expands into discrete orders.

### Sprint 3 Detail – Clinical Documentation
- Deliverables: note template model (H&P/progress/discharge/consent), versioning, signature/attestation, PDF export bound to encounter.
- Tests: create/attest increments version and audit; PDF export produced and linked to encounter.

### Sprint 4 Detail – MAR/Medication Safety
- Deliverables: MAR shift views, overdue cues, barcode hook points, allergy/clinical rule checks, IV/titration support.
- Tests: administration only after rule check passes; overdue queue populated; barcode hook callable in tests.

### Sprint 5 Detail – Scheduling & Hand-offs
- Deliverables: procedure/consult scheduling board with resource availability, waitlist/overbook rules, hand-off summary template.
- Tests: scheduling prevents resource collisions; hand-off summary includes recent vitals/orders snapshot.

### Sprint 6 Detail – Billing/Revenue
- Deliverables: charge capture pipeline from administrations/procedures/docs, coding support stubs, claim export stub, reconciliation logging.
- Tests: admin/procedure triggers charge entry; claim export generated; edits/audits recorded.

## 9. Milestones & Remaining Work
- **Completed – Milestone #1: Production-ready Core Data Layer.** EF-backed repositories now replace seeded data, migrations are applied automatically on startup, and identity plus agreements workflows run entirely against the persistent store.
- **Completed – Agreement Workflow Enhancements:** Approvals, renewals, validation diagnostics, automated escalation scheduler, and notification surfacing now flow through the shell end-to-end.
- **Operational Dashboards Persistence:** Retail, corporate, and reporting metrics now flow through EF-backed services with durable storage and caching.
- **Telemetry & Health:** Extend telemetry visuals atop the new aggregates and health snapshots to complete the system-health workspace experience.
- **Queued – Milestone #4: Distributed Sync & Global Identity.** Begin the conflict resolution engine, delta-sync provider, and GUID transition workstreams to support multi-site operations.
- **Completed – Milestone #5: Dynamic Labeling & Multi-Context UI.** Localization context selector in the shell, context-aware bindings across agreements, metadata-driven forms with persisted `PartyMetadata`, and migration `20260102140153_AddPartyMetadata` in place and applied.
- **Completed – Milestone #6: Advanced Clinical & Retail Workflows.** CPOE/MAR orders/dispense/admin flows and omnichannel POS suspend/return flows delivered with migration `20260102144900_202601021530_M6ClinicalRetail` applied.
- **Completed – Milestone #7: Proactive AI & Predictive Telemetry.** Predictive SLA cards (risk/ETA/driver/action hint) and inventory anomaly detection (ledger analytics, alerts, worker) delivered with migration `20260102161935_202601021730_M7PredictiveTelemetryInventory` applied.

## 10. Progress to Date (January 2026)
- Delivered predictive SLA monitor with risk/ETA/driver cards and action hints, plus inventory anomaly analytics (shrinkage/stock-out) with alerts driven by a background worker and helper overlays for clinic onboarding.
- Stabilized the WPF shell with AsyncRelayCommand dispatcher marshaling and UI-thread aligned workspace initialization across clinical, financial, and inventory modules.
- Introduced EF Core migrations for the core data store, removed runtime seeding, added a migration bootstrapper that backfills legacy EnsureCreated databases before applying migrations, and wired the shell startup to apply migrations automatically.
- Eliminated agreement workspace pricing history seeding so dashboards only reflect real calculation activity recorded during the session.
- Added agreement approval timelines, renewal scheduling, workflow commands that surface status metrics and drive EF-backed approval history, and centralized pricing logic through the domain `PricingCalculator` with workflow-aware safeguards.
- Delivered workflow validation diagnostics and escalation publishing from `AgreementService`, wiring the shell event bus and notification center so approvers see real-time alerts when approvals age out.
- Delivered identity and agreements services over EF repositories, enabling editable party profiles, pricing dashboards, rate authoring, and pricing calculations using persisted data.
- Instrumented the shell startup pipeline and module loader to log diagnostics under `%LOCALAPPDATA%\ExcellCore`, documenting the publish failure fix path and providing a standing playbook for future startup regressions.
- Added focused unit tests covering pricing calculations, workflow validation checks, escalation notifications, triage ordering, identity rollups, actionable reminder/escalation publishing, SLA heat-map/action insight generation, the party lookup endpoint, and impacted-party save validation using a migratable SQLite context and test doubles for agreements.
- Implemented a JSON sync transport adapter to package outbound deltas and apply inbound payloads, and added a reporting export service that generates CSV exports from schedules; both are registered in DI and validated by new tests.
- Delivered proactive triage cards inside the agreements workspace, surfacing impacted identities, potential value, and one-click reminder or fast-track actions tied to the domain service.
- Expanded SLA triage analytics with agreement heat-map buckets and reminder/fast-track insight summaries surfaced directly on the agreements workspace for rapid executive review.
- Logged reminder and fast-track activity into the persistent action log while triage cards now pull impacted party names from the identity graph for contextual prioritization.
- Automated hourly escalation monitor to escalate aged approvals and surface notifications through the shell event bus for approver awareness.
- Unified module styling through shared summary-card resources applied across identity, clinical, inventory, financial, retail, corporate, and reporting dashboards.
- Persisted retail and corporate dashboards via new EF-backed services (`IRetailOperationsService`, `ICorporatePortfolioService`) and applied migration `20251227140000_RetailAndCorporateDashboards`, documenting the update flow (`dotnet ef migrations add`, `dotnet ef database update`) to avoid future drift.
- Persisted reporting workspace dashboards and export schedules through the domain `ReportingService`, complete with a dedicated migration and seeding path that hydrates the module from the shared SQLite store.
- Persisted slow-query telemetry aggregates, thresholds, and health snapshots powered by the domain `TelemetryService` and the shell `TelemetryAggregationWorker`, surfacing severity-aware system health updates in the main window banner. Added predictive SLA cards (risk/ETA/driver) and a “Getting Started” overlay for clinic onboarding.
- Built a reporting telemetry workspace with trend, severity breakdown, health refresher views, and predictive SLA triage cards that bind directly to the telemetry aggregates and thresholds persisted by the domain service.
- Documented milestone roadmap and shared module architecture, providing clear next steps toward operational telemetry and data persistence; added inventory anomaly analytics worker and alerts seeded via migration `20260102161935_202601021730_M7PredictiveTelemetryInventory`.

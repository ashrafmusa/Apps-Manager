# Sequential UUID Migration Plan

## Current Identifier Landscape
| Aggregate | Primary Key Property | Type | Notes |
|-----------|----------------------|------|-------|
| Party | PartyId | Guid | Created via `Guid.NewGuid()` inside `PartyService` when absent. |
| PartyIdentifier | PartyIdentifierId | Guid | Generated per identifier addition inside `PartyService`. |
| Agreement | AgreementId | Guid | Assigned in `AgreementService` for new agreements. |
| AgreementRate | AgreementRateId | Guid | Generated per rate entry. |
| AgreementApproval | AgreementApprovalId | Guid | Set during approval creation in `AgreementService`. |
| AgreementImpactedParty | AgreementImpactedPartyId | Guid | Created when impacted parties are added. |
| RetailTransaction | RetailTransactionId | Guid | Seeded in `RetailOperationsService` during transaction creation. |
| Ticket | TicketId | Guid | Generated for retail ticket entries. |
| CorporateContract | CorporateContractId | Guid | Generated in `CorporatePortfolioService`. |
| ReportingDashboard | ReportingDashboardId | Guid | Generated within reporting bootstrapper. |
| ReportingSchedule | ReportingScheduleId | Guid | Generated within reporting bootstrapper. |
| TelemetryEvent | TelemetryEventId | Guid | Assigned by telemetry capture pipeline. |
| TelemetryAggregate | TelemetryAggregateId | Guid | Generated during aggregation worker runs. |
| TelemetryThreshold | TelemetryThresholdId | Guid | Created via configuration seeding. |
| TelemetryHealthSnapshot | TelemetryHealthSnapshotId | Guid | Generated per snapshot emission. |
| ActionLog | ActionLogId | Guid | Created in `AgreementService` escalation/reminder flows. |

> All aggregates already use GUID identifiers; there are no integer primary keys to remap before global sync.

## Migration Strategy
1. **Introduce Sequential GUID Generator**
   - Provide an `ISequentialGuidGenerator` abstraction within the domain layer.
   - Implement a COMB-style generator in infrastructure so new IDs remain globally unique while sorting by creation time.
2. **Refactor Creation Workflows**
   - Update services that currently call `Guid.NewGuid()` (e.g., AgreementService, PartyService, RetailOperationsService) to request IDs from the generator.
   - Ensure tests inject a deterministic generator to keep assertions stable.
3. **Backfill Existing Records**
   - No schema change required; future migrations will touch only generation logic. Existing GUIDs remain valid and globally unique.
4. **Central Registry Alignment**
   - During the first multi-site sync, register each locally generated sequential GUID with the central catalog to prevent duplication and support reverse lookup.
5. **Telemetry Hooks**
   - Emit metrics when fallbacks to random GUIDs occur so operations can detect any regression in sequential generation.

## Next Tasks
- Add the `ISequentialGuidGenerator` abstraction and infrastructure implementation.
- Schedule service-level refactors to consume the generator (track progress in Milestone #4 checklist).
- Update automated tests to verify deterministic ID issuance under controlled generators.

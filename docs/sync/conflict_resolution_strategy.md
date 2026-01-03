# Conflict Resolution Strategy for Distributed Sync

## Objectives
- Maintain a single logical source of truth across intermittently connected sites.
- Resolve concurrent edits to Agreements and Parties deterministically and audibly.
- Minimize merge drift by capturing intent (field-level deltas) alongside chronology.

## Resolution Model
1. **Vector Clock Stamp**
   - Each aggregate instance carries a vector clock per site (e.g., `{ClinicalA:5, RetailB:3}`).
   - Updates increment the local site entry before persistence.
   - During sync, a comparison runs:
     - **Dominant Update:** If one clock is strictly greater-or-equal in all dimensions, accept newer version.
     - **Divergent Update:** If neither dominates, treat as conflict and engage merge rules.
2. **Merge rules**
   - **Agreements**
     - Field-level merge where monetary and date attributes prefer the vector clock winner.
     - Impacted party collections merge by PartyId; conflicting relationships flagged for manual review.
   - **Parties**
     - Demographics prefer latest edit; identifier additions merge union-style; identifier updates follow vector clock winner with audit trail entry.
3. **Manual escalation**
   - When vector clocks collide on critical fields (Agreement rate change + impacted party change), log conflict to `SyncConflictLog` and push to the triage UI.

## Service Contracts
- `IConflictResolverService`
  - `ConflictResolutionResult Resolve<TAggregate>(TAggregate local, TAggregate incoming, VectorClock localClock, VectorClock incomingClock)`
  - `Task<IReadOnlyList<SyncConflict>> ReconcileAsync(IEnumerable<SyncDelta> deltas, CancellationToken token)`
- `SyncDelta` captures aggregate key, modified fields, vector clock, and originating site metadata.

## Persistence & Audit
- Introduce `SyncVector` table storing latest known vector per aggregate and site.
- Append `SyncConflictLog` table for escalated cases (aggregate type, key, fields, resolver hint, timestamp).
- Ensure `AuditTrail` references `SourceSite` for downstream reporting.

## Next Actions
1. Update domain models to persist vector clocks alongside Agreements and Parties (new owned type `VectorClockStamp`).
2. Implement `ConflictResolverService` in `Extensions.Sync` with extender hooks per aggregate type.
3. Extend sync pipeline to emit field-level deltas (reuse metadata from EF change tracker).
4. Build triage view model to surface conflicts and provide accept/override commands.

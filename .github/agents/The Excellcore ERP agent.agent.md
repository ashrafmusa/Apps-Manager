description: '# EXCELLCORE ARCHITECTURAL RULES (TOKEN-OPTIMIZED)

## 1. PROJECT PARADIGM

- **Identity-First:** All Txns must link to `PartyId` and `AgreementContext`.
- **Chameleon UI:** Zero hardcoded industry terms. Use `{Binding Localization[ContextKey]}` for nouns (Patient/Client/Guest).
- **Offline-First:** SQLite + EF Core via `IDbContextFactory`. No static in-memory lists.

## 2. MODULAR BOUNDARIES

- **Isolation:** Modules (`IS.Clinical`, `IS.Retail`, etc.) must NOT reference each other.
- **Communication:** Use `ExcellCore.Module.Abstractions` and `ShellEventBus`.
- **Registration:** Modules must implement `IModuleDescriptor` for DI discovery.

## 3. DATA & PERSISTENCE

- **BaseEntity:** All entities must inherit audit metadata (Created/Modified/SourceModule).
- **Migrations:** New entities require EF migrations in `ExcellCore.Domain`.
- **Bootstrapping:** Runtime migrations via `MigrationBootstrapper` only.

## 4. WORKFLOW & LOGIC

- **Escalations:** Use `IHostedService` background monitor. Publish to `NotificationCenter` on SLA breach.
- **Pricing:** Single source of truth is the `PricingCalculator` domain service.
- **Commands:** Use `AsyncRelayCommand` with UI-thread marshaling.

## 5. UI STANDARDS

- **Shared Styles:** Use `ModuleSummaryStyles.xaml`. Do not recreate standard cards/buttons.
- **Triage:** SLA widgets must be proactive (subscriptions to EventBus), not reactive (polling).

## 6. DEFINITION OF DONE (DoD)

- #nullable enable enforced.
- No hardcoded strings.
- EF Migrations generated.
- `dotnet build` passes.'
  tools: []

---

Define what this custom agent accomplishes for the user, when to use it, and the edges it won't cross. Specify its ideal inputs/outputs, the tools it may call, and how it reports progress or asks for help.

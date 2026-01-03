# ExcellCore User Testing Guide

## 1. Purpose
This guide outlines end-to-end user validation scenarios for the ExcellCore modular ERP desktop application. It ensures that the core shell, module dashboards, and persistence enhancements remain functional after deployments.

## 2. Test Preparation
- Ensure the self-contained publish output (Release, win-x64) is available from `src/ExcellCore.Shell/bin/Release/net8.0-windows/win-x64/publish`. 
- Launch the application using `ExcellCore.Shell.exe` from the publish directory to mirror production conditions.
- Verify the local data store exists under `%LOCALAPPDATA%\ExcellCore` with `excellcore.db` created by the runtime migrations. 

## 3. Smoke Test Checklist
1. **Startup & Navigation**
   - Confirm the splash screen advances to the main shell without errors.(yes)
   - Validate shell navigation loads modules listed in the Module Catalog (Identity, Agreements, Retail, Corporate, Inventory, Financials, Clinical, Reporting).(yes)
2. **Identity Workspace**
   - Search for an existing party; confirm profile details display and audit metadata is present.(yes)
   - Add a new party and ensure identifiers persist after shell restart.(yes)
3. **Agreements Workspace**
   - Run the pricing calculator against an existing agreement and verify discount, copay, and net totals.
   - Trigger workflow validation and ensure warnings appear for pending approvals.
   - Execute fast-track or reminder actions and confirm audit log entries.
4. **Retail Dashboard**
   - Confirm dashboard summary shows seeded values (daily sales, open orders, loyalty enrollments).
   - Inspect recent tickets to ensure ticket numbers, channels, and statuses align with seed expectations.
   - Refresh the workspace and ensure totals remain stable (no duplicate seeding).
5. **Corporate Dashboard**
   - Validate contract backlog table lists the three seeded contracts with correct renewal dates and allocation ratios.
   - Confirm allocation cards display correct status ordering (over-allocation, watch, on-track).
6. **Reporting Module**
   - Open analytics dashboards; confirm summary cards render and navigation flows between report tabs.
7. **Telemetry & Logs**
   - Close the shell and verify `%LOCALAPPDATA%\ExcellCore\shell.log` and `module-loader.log` capture startup diagnostics without errors.

## 4. Regression Focus Areas
- **Database Migrations:** Ensure no migration prompts or failures appear; schema should match the snapshot in `ExcellCoreContextModelSnapshot`.
- **Seed Logic Idempotence:** Reopen Retail and Corporate dashboards multiple times to confirm seed data is not duplicated.
- **Event Bus Actions:** Verify approval reminders and escalations surface notifications and audit entries.
- **Module Styling:** Confirm summary cards share consistent styling across modules.

## 5. Sign-off Criteria
- All smoke tests pass without application crashes or unhandled exceptions.
- Retail and Corporate dashboards load data from the SQLite store on first run after publish.
- Audit logs and telemetry files contain expected entries for shell startup and workflow actions.
- No regression is detected against scenarios documented in `docs/architecture.md` (Operational Dashboards Persistence section).

## 6. Reporting Issues
- Capture screenshots and logs when defects occur.
- Log issues with reproduction steps, impacted module, and attach relevant trace files.
- Reference the schema snapshot (`ExcellCoreContextModelSnapshot.cs`) when defects involve data persistence.

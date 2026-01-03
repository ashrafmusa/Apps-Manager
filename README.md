# ExcellCore Modular ERP

[![CI](https://github.com/ashrafmusa/Apps-Manager/actions/workflows/ci.yml/badge.svg)](https://github.com/ashrafmusa/Apps-Manager/actions/workflows/ci.yml)

ExcellCore is a Windows desktop ERP framework focused on modular growth. Each business capability is delivered as a plug-in module that the shell discovers and activates at runtime. The current solution ships two core modules—Master Identity Registry and Agreement Engine—backed by a shared domain, persistence layer, and infrastructure services.

---

## Table of Contents
1. [System Architecture](#system-architecture)
2. [Prerequisites](#prerequisites)
3. [Initial Setup](#initial-setup)
4. [Building and Running](#building-and-running)
5. [Project Structure](#project-structure)
6. [Runtime Behavior](#runtime-behavior)
7. [Data and Persistence](#data-and-persistence)
8. [Logging and Diagnostics](#logging-and-diagnostics)
9. [Testing](#testing)
10. [Development Workflow](#development-workflow)
11. [Roadmap](#roadmap)

---

## System Architecture

- **Shell (WPF)**: Hosts the main window, renders module navigation, and displays the currently selected module view inside a `ContentControl`.
- **Module Loader**: Discovers module manifests (`modules/<ModuleId>/manifest.json`), loads assemblies, registers descriptors, and activates modules through dependency injection.
- **Domain Layer**: Centralizes entities (Party, Agreement, AgreementRate) alongside an EF Core `DbContext` (`ExcellCoreContext`).
- **Infrastructure Layer**: Provides DI registration, module catalog/host services, and SQLite database configuration.
- **Modules**:
  - **Core.Identity** – Master Identity Registry with full CRUD UI, search, and persistence commands.
  - **Core.Agreements** – Agreement Engine with agreement/rate maintenance, pricing calculator, and command infrastructure.

---

## Prerequisites

1. Windows 10/11 with desktop experience.
2. [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
3. Visual Studio 2022 (17.8+) with **.NET Desktop Development** workload or VS Code with C# Dev Kit.

Optional tooling: SQLite viewer (e.g., DB Browser for SQLite) for inspecting the persistence database.

---

## Initial Setup

```bash
dotnet restore src/ExcellCore.sln
```

This pulls NuGet dependencies for all projects (domain, infrastructure, modules, tests).

---

## Building and Running

Build the full solution:

```bash
dotnet build src/ExcellCore.sln
```

Launch the desktop shell (no rebuild):

```bash
dotnet run --no-build --project src/ExcellCore.Shell/ExcellCore.Shell.csproj
```

> **Tip:** The shell is a WPF application; ensure you run the command in an interactive Windows session so the window can display.

---

## Project Structure

- **src/ExcellCore.Shell** – WPF application entry point and main window.
- **src/ExcellCore.Infrastructure** – Dependency injection, module catalog/host, manifest provider, EF Core SQLite factory.
- **src/ExcellCore.Domain** – Entities, DTOs, EF Core context, agreement domain services.
- **src/ExcellCore.Module.Abstractions** – Module contracts (`IModule`, `IModuleCatalog`, `IModuleHost`, `ModuleManifest`).
- **src/ExcellCore.Module.Core.Identity** – Identity module view models, commands, views, and module registration.
- **src/ExcellCore.Module.Core.Agreements** – Agreement module (view model, commands, workspace UI, module entry point).
- **src/ExcellCore.Tests** – xUnit automation placeholder.

Sibling directories (e.g., `bin`, `obj`) are generated during builds.

---

## Runtime Behavior

1. **Startup**
   - `App` configures logging, sets up the service collection, and invokes `ModuleLoader.LoadModulesAsync` to register modules defined by manifests.
   - Modules contribute services and views via `ConfigureServices` and `Configure` methods.
2. **Main Window**
   - The shell lists each registered module in a `ListView`. Selecting a module resolves the view through `IModuleHost` and renders it in the main content region.
3. **Master Identity Registry**
   - Provides search, new, and save commands for Party records with validation and notifications.
   - Data grid shows existing parties; detail pane allows editing of selected identity.
4. **Agreement Engine**
   - Enables agreement search, agreement/rate editing, rate management (add/remove), and pricing calculation.
   - Pricing panel calculates net, discount, and copay amounts via `IAgreementService` using stored rate definitions.

---

## Data and Persistence

- SQLite database stored at `%LOCALAPPDATA%/ExcellCore/excellcore.db`.
- `ExcellCoreContext` registers DbSets for `Parties`, `PartyIdentifiers`, `Agreements`, and `AgreementRates`.
- Decimal precision handled via EF Core relational mapping (requires `Microsoft.EntityFrameworkCore.Relational`).
- Agreement saves ensure rate collections are replaced atomically to keep data consistent.

**Seeding:** No seed data ships by default. Create records through the modules or a dedicated migration/seeding routine (future work).

---

## Logging and Diagnostics

- Shell logging writes to the console during development (uses `Microsoft.Extensions.Logging.Console`).
- Unhandled exceptions are captured in `%LOCALAPPDATA%/ExcellCore/shell.log` for troubleshooting startup and UI errors.
- Adjust logging providers in `App.xaml.cs` as needed (e.g., file, Seq, Application Insights).

---

## Testing

- `ExcellCore.Tests` targets net8.0-windows for WPF compatibility. Add xUnit-based tests for domain services (identity CRUD, agreement pricing) to protect business logic.
- Suggested test areas:
  - Agreement pricing calculations for varying discount/copay inputs.
  - Identity CRUD workflows to validate data persistence and validation safeguards.

Run all tests:

```bash
dotnet test src/ExcellCore.sln
```

---

## Development Workflow

1. Update module manifest (`src/ExcellCore.Shell/modules/<ModuleId>/manifest.json`) with `ModuleId`, `DisplayName`, `AssemblyPath`, and `Enabled` flag.
2. Implement module class inheriting `IModule` and optionally `IModuleDescriptor` for metadata.
3. Register view models/services in `ConfigureServices`; register WPF views with `IModuleHost` in `Configure`.
4. Add DI wiring in `ServiceCollectionExtensions` when infrastructure services expand.
5. Validate by running `dotnet run --no-build --project src/ExcellCore.Shell/ExcellCore.Shell.csproj` and exercising the new module UI.

---

## Roadmap

- Seed identity and agreement sample data for demos.
- Add additional modules (e.g., Pricing Analytics, Inventory Management).
- Introduce authentication/authorization layers when multi-user scenarios emerge.
- Expand automated testing coverage and integrate CI pipeline.
- Consider packaging via MSIX for easier deployment.

---

For architectural background and detailed design decisions, refer to `docs/architecture.md` within the repository.

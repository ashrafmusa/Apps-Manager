using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Services;

public sealed class ClinicalWorkflowService : IClinicalWorkflowService
{
    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;
    private readonly ISequentialGuidGenerator _idGenerator;

    public ClinicalWorkflowService(IDbContextFactory<ExcellCoreContext> contextFactory, ISequentialGuidGenerator idGenerator)
    {
        _contextFactory = contextFactory;
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<ClinicalDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        await EnsureSeedDataAsync(dbContext, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var startOfDayUtc = utcNow.Date;

        var partyLookup = await dbContext.Parties
            .AsNoTracking()
            .Select(p => new { p.PartyId, p.DisplayName })
            .ToDictionaryAsync(p => p.PartyId, p => p.DisplayName, cancellationToken);

        var orders = await dbContext.MedicationOrders
            .AsNoTracking()
            .OrderByDescending(o => o.OrderedOnUtc)
            .Take(25)
            .ToListAsync(cancellationToken);

        var orderDtos = orders
            .Select(o => new MedicationOrderDto(
                o.MedicationOrderId,
                string.IsNullOrWhiteSpace(o.OrderNumber) ? FormattableString.Invariant($"ORD-{ShortId(o.MedicationOrderId)}") : o.OrderNumber,
                partyLookup.TryGetValue(o.PartyId, out var displayName) ? displayName : "Patient",
                o.Medication,
                o.Dose,
                o.Route,
                o.Frequency,
                string.IsNullOrWhiteSpace(o.Status) ? "Ordered" : o.Status,
                string.IsNullOrWhiteSpace(o.OrderingProvider) ? "Ordering provider" : o.OrderingProvider,
                o.OrderedOnUtc,
                o.StartOnUtc))
            .ToList();

        var orderLookup = orders.ToDictionary(o => o.MedicationOrderId, o => o);

        var administrations = await dbContext.MedicationAdministrations
            .AsNoTracking()
            .Where(a => a.AdministeredOnUtc == null || a.AdministeredOnUtc >= startOfDayUtc)
            .OrderBy(a => a.ScheduledForUtc)
            .Take(25)
            .ToListAsync(cancellationToken);

        var upcoming = administrations
            .Select(a =>
            {
                var order = orderLookup.TryGetValue(a.MedicationOrderId, out var value) ? value : null;
                var orderNumber = order?.OrderNumber ?? "Order";
                var medication = order?.Medication ?? "Medication";
                var status = string.IsNullOrWhiteSpace(a.Status) ? "Scheduled" : a.Status;
                return new MedicationAdministrationDto(
                    a.MedicationAdministrationId,
                    a.MedicationOrderId,
                    orderNumber,
                    medication,
                    a.ScheduledForUtc,
                    a.AdministeredOnUtc,
                    status,
                    a.PerformedBy);
            })
            .ToList();

        var dispensesToday = await dbContext.DispenseEvents
            .AsNoTracking()
            .Where(d => d.DispensedOnUtc >= startOfDayUtc)
            .CountAsync(cancellationToken);

        var summary = new ClinicalSummaryDto(orderDtos.Count, upcoming.Count, dispensesToday);

        return new ClinicalDashboardDto(orderDtos, upcoming, summary);
    }

    public async Task<PatientJourneyDto> GetPatientJourneyAsync(Guid? patientId = null, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        await EnsureSeedDataAsync(dbContext, cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var inventoryAlerts = await dbContext.InventoryAnomalyAlerts
            .AsNoTracking()
            .Where(a => a.DetectedOnUtc >= nowUtc.AddDays(-2))
            .OrderByDescending(a => a.DetectedOnUtc)
            .ToListAsync(cancellationToken);

        var patients = await dbContext.Parties
            .AsNoTracking()
            .Where(p => p.PartyType == "Patient")
            .OrderBy(p => p.DisplayName)
            .ToListAsync(cancellationToken);

        if (patients.Count == 0)
        {
            throw new InvalidOperationException("No patients available for journey view.");
        }

        var selectedPatientId = patientId ?? patients.First().PartyId;
        var selectedPatient = patients.FirstOrDefault(p => p.PartyId == selectedPatientId) ?? patients.First();

        var orders = await dbContext.MedicationOrders
            .AsNoTracking()
            .Where(o => o.PartyId == selectedPatient.PartyId)
            .OrderByDescending(o => o.OrderedOnUtc)
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(o => o.MedicationOrderId).ToList();

        var dispenses = await dbContext.DispenseEvents
            .AsNoTracking()
            .Where(d => orderIds.Contains(d.MedicationOrderId))
            .OrderBy(d => d.DispensedOnUtc)
            .ToListAsync(cancellationToken);

        var administrations = await dbContext.MedicationAdministrations
            .AsNoTracking()
            .Where(a => orderIds.Contains(a.MedicationOrderId))
            .OrderBy(a => a.ScheduledForUtc)
            .ToListAsync(cancellationToken);

        var events = new List<JourneyEventDto>();

        foreach (var order in orders)
        {
            var (signalSeverity, signalMessage) = ResolveInlineSignals(order, inventoryAlerts, nowUtc);
            events.Add(new JourneyEventDto(
                order.MedicationOrderId,
                "Order",
                FormattableString.Invariant($"{order.Medication} {order.Dose} {order.Route}"),
                string.IsNullOrWhiteSpace(order.Status) ? "Ordered" : order.Status,
                order.Frequency,
                order.OrderedOnUtc,
                order.MedicationOrderId,
                signalSeverity,
                signalMessage));

            AddCheckpointEvents(order, events);
        }

        foreach (var dispense in dispenses)
        {
            events.Add(new JourneyEventDto(
                dispense.DispenseEventId,
                "Dispense",
                string.IsNullOrWhiteSpace(dispense.InventoryItem) ? "Dispense" : dispense.InventoryItem,
                "Completed",
                FormattableString.Invariant($"Qty {dispense.Quantity} {dispense.Unit} @ {dispense.Location ?? "Pharmacy"}"),
                dispense.DispensedOnUtc,
            dispense.MedicationOrderId));
        }

        foreach (var admin in administrations)
        {
            events.Add(new JourneyEventDto(
                admin.MedicationAdministrationId,
                "Administration",
                "Administration",
                string.IsNullOrWhiteSpace(admin.Status) ? "Scheduled" : admin.Status,
                admin.PerformedBy ?? "",
                admin.AdministeredOnUtc ?? admin.ScheduledForUtc,
                admin.MedicationOrderId));
        }

        events = events
            .OrderBy(e => e.WhenUtc)
            .ToList();

        var worklist = new List<RoleWorklistItemDto>();

        // Provider worklist: active orders needing review
        foreach (var order in orders.Where(o => string.IsNullOrWhiteSpace(o.Status) || !o.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)))
        {
            worklist.Add(new RoleWorklistItemDto(
                "Provider",
                FormattableString.Invariant($"Order {order.Medication} ({order.OrderNumber ?? "Order"})"),
                string.IsNullOrWhiteSpace(order.Status) ? "Ordered" : order.Status,
                order.OrderedOnUtc,
                order.MedicationOrderId,
                null));
        }

        // Pharmacist worklist: most recent dispenses by order
        foreach (var dispense in dispenses)
        {
            worklist.Add(new RoleWorklistItemDto(
                "Pharmacist",
                string.IsNullOrWhiteSpace(dispense.InventoryItem) ? "Dispense" : dispense.InventoryItem,
                "Dispensed",
                dispense.DispensedOnUtc,
                dispense.MedicationOrderId,
                null));
        }

        // Nurse worklist: administrations not yet completed or scheduled soon
        foreach (var admin in administrations.Where(a => a.AdministeredOnUtc == null || a.AdministeredOnUtc <= nowUtc))
        {
            var due = admin.ScheduledForUtc;
            worklist.Add(new RoleWorklistItemDto(
                "Nurse",
                "Medication Administration",
                string.IsNullOrWhiteSpace(admin.Status) ? "Scheduled" : admin.Status,
                due,
                admin.MedicationOrderId,
                admin.MedicationAdministrationId));
        }

        var signals = new List<JourneySignalDto>();

        foreach (var admin in administrations)
        {
            if (admin.AdministeredOnUtc is null && admin.ScheduledForUtc < nowUtc)
            {
                signals.Add(new JourneySignalDto(
                    "Warning",
                    "Administration overdue",
                    admin.ScheduledForUtc,
                    admin.MedicationOrderId,
                    admin.MedicationAdministrationId));
            }
        }

        return new PatientJourneyDto(
            selectedPatient.PartyId,
            selectedPatient.DisplayName ?? "Patient",
            events,
            worklist,
            signals);
    }

    private void AddCheckpointEvents(MedicationOrder order, List<JourneyEventDto> events)
    {
        var baseTime = order.StartOnUtc ?? order.OrderedOnUtc;
        var labNames = new[] { "CBC", "CMP", "Coags", "LFT Panel" };
        var radNames = new[] { "Chest X-ray", "CT Abd/Pelvis", "MRI Brain", "Ultrasound" };

        var labTitle = ResolveFromSet(labNames, order.MedicationOrderId);
        var radTitle = ResolveFromSet(radNames, order.MedicationOrderId);

        events.Add(new JourneyEventDto(
            _idGenerator.Create(),
            "Lab",
            FormattableString.Invariant($"{labTitle} collected"),
            "Completed",
            "Results available",
            baseTime.AddHours(1),
            order.MedicationOrderId));

        events.Add(new JourneyEventDto(
            _idGenerator.Create(),
            "Radiology",
            FormattableString.Invariant($"{radTitle} scheduled"),
            "Scheduled",
            "Awaiting report",
            baseTime.AddHours(2),
            order.MedicationOrderId));

        events.Add(new JourneyEventDto(
            _idGenerator.Create(),
            "Billing",
            "Charge capture",
            "Queued",
            "Billing review in progress",
            baseTime.AddHours(3),
            order.MedicationOrderId));
    }

    private static string ResolveFromSet(string[] source, Guid anchor)
    {
        if (source.Length == 0)
        {
            return string.Empty;
        }

        var index = Math.Abs(anchor.GetHashCode()) % source.Length;
        return source[index];
    }

    private static string? UpgradeSeverity(string? current, string incoming)
    {
        if (string.IsNullOrWhiteSpace(incoming))
        {
            return current;
        }

        var priority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Info"] = 1,
            ["Warning"] = 2,
            ["Critical"] = 3
        };

        if (current is null)
        {
            return incoming;
        }

        var currentPriority = priority.TryGetValue(current, out var cp) ? cp : 0;
        var incomingPriority = priority.TryGetValue(incoming, out var ip) ? ip : 0;

        return incomingPriority > currentPriority ? incoming : current;
    }

    private (string? Severity, string? Message) ResolveInlineSignals(MedicationOrder order, IReadOnlyList<InventoryAnomalyAlert> inventoryAlerts, DateTime nowUtc)
    {
        string? severity = null;
        string? message = null;

        var ageHours = (nowUtc - order.OrderedOnUtc).TotalHours;
        if (ageHours >= 24)
        {
            severity = "Critical";
            message = "SLA risk: order past 24h threshold.";
        }
        else if (ageHours >= 18)
        {
            severity = "Warning";
            message = "SLA risk: order approaching 24h threshold.";
        }

        var inventoryMatch = inventoryAlerts
            .FirstOrDefault(a =>
                (!string.IsNullOrWhiteSpace(order.Medication) && a.ItemName.Contains(order.Medication, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(order.Medication) && a.Sku.Replace("-", " ").Contains(order.Medication, StringComparison.OrdinalIgnoreCase)));

        if (inventoryMatch is not null)
        {
            severity = UpgradeSeverity(severity, inventoryMatch.Severity);
            message = FormattableString.Invariant($"Inventory {inventoryMatch.SignalType}: {inventoryMatch.Message}");
        }

        return (severity, message);
    }

    private async Task EnsureSeedDataAsync(ExcellCoreContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.MedicationOrders.AsNoTracking().AnyAsync(cancellationToken))
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;

        var patients = new List<Party>
        {
            CreatePatient("Jared Collins", nowUtc.AddYears(-42)),
            CreatePatient("Maya Patel", nowUtc.AddYears(-35)),
            CreatePatient("Omar Hassan", nowUtc.AddYears(-29))
        };

        await dbContext.Parties.AddRangeAsync(patients, cancellationToken);

        var orders = new List<MedicationOrder>
        {
            CreateOrder(patients[0].PartyId, "ORD-1001", "Ceftriaxone", "1 g", "IV", "q24h", "In Progress", "Dr. Chen", nowUtc.AddHours(-6), nowUtc.AddHours(-5.5), nowUtc.AddHours(18)),
            CreateOrder(patients[1].PartyId, "ORD-1002", "Metformin", "500 mg", "PO", "BID", "Active", "Dr. Grant", nowUtc.AddHours(-12), nowUtc.AddHours(-11.5), null),
            CreateOrder(patients[2].PartyId, "ORD-1003", "Heparin", "5,000 units", "SQ", "q8h", "Active", "Dr. Ellis", nowUtc.AddHours(-3), nowUtc.AddHours(-2.5), nowUtc.AddHours(16))
        };

        foreach (var order in orders)
        {
            order.DispenseEvents.Add(new DispenseEvent
            {
                DispenseEventId = _idGenerator.Create(),
                MedicationOrderId = order.MedicationOrderId,
                InventoryItem = FormattableString.Invariant($"{order.Medication.ToUpperInvariant().Replace(" ", string.Empty)}-PACK"),
                Quantity = 1,
                Unit = "unit",
                DispensedOnUtc = nowUtc.AddHours(-2),
                DispensedBy = "Pharmacy",
                Location = "Main Pharmacy",
                Audit = new AuditTrail { CreatedBy = "seed", SourceModule = "IS.Clinical" }
            });

            order.Administrations.Add(new MedicationAdministration
            {
                MedicationAdministrationId = _idGenerator.Create(),
                MedicationOrderId = order.MedicationOrderId,
                ScheduledForUtc = order.StartOnUtc ?? nowUtc,
                AdministeredOnUtc = order.StartOnUtc,
                PerformedBy = "RN Team",
                Status = "Completed",
                Notes = "Initial dose",
                Audit = new AuditTrail { CreatedBy = "seed", SourceModule = "IS.Clinical" }
            });

            order.Administrations.Add(new MedicationAdministration
            {
                MedicationAdministrationId = _idGenerator.Create(),
                MedicationOrderId = order.MedicationOrderId,
                ScheduledForUtc = (order.StartOnUtc ?? nowUtc).AddHours(8),
                AdministeredOnUtc = null,
                PerformedBy = null,
                Status = "Scheduled",
                Notes = null,
                Audit = new AuditTrail { CreatedBy = "seed", SourceModule = "IS.Clinical" }
            });
        }

        await dbContext.MedicationOrders.AddRangeAsync(orders, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Party CreatePatient(string displayName, DateTime? birthDateUtc)
    {
        var dob = birthDateUtc?.Date;
        return new Party
        {
            PartyId = _idGenerator.Create(),
            PartyType = "Patient",
            DisplayName = displayName,
            DateOfBirth = dob.HasValue ? DateOnly.FromDateTime(dob.Value) : null,
            Audit = new AuditTrail
            {
                CreatedBy = "seed",
                SourceModule = "IS.Clinical"
            }
        };
    }

    private MedicationOrder CreateOrder(
        Guid patientId,
        string orderNumber,
        string medication,
        string dose,
        string route,
        string frequency,
        string status,
        string orderingProvider,
        DateTime orderedOnUtc,
        DateTime? startOnUtc,
        DateTime? endOnUtc)
    {
        return new MedicationOrder
        {
            MedicationOrderId = _idGenerator.Create(),
            PartyId = patientId,
            OrderNumber = orderNumber,
            Medication = medication,
            Dose = dose,
            Route = route,
            Frequency = frequency,
            Status = status,
            OrderingProvider = orderingProvider,
            OrderedOnUtc = orderedOnUtc,
            StartOnUtc = startOnUtc,
            EndOnUtc = endOnUtc,
            Audit = new AuditTrail
            {
                CreatedBy = "seed",
                SourceModule = "IS.Clinical"
            }
        };
    }

    public async Task<MedicationOrderDto> CreateOrderAsync(CreateMedicationOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        await EnsureSeedDataAsync(dbContext, cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var startOnUtc = request.StartOnUtc ?? nowUtc.AddMinutes(30);
        var status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim();

        var partyId = request.PartyId;
        if (!partyId.HasValue || !await dbContext.Parties.AsNoTracking().AnyAsync(p => p.PartyId == partyId.Value, cancellationToken))
        {
            var patient = new Party
            {
                PartyId = _idGenerator.Create(),
                PartyType = "Patient",
                DisplayName = string.IsNullOrWhiteSpace(request.PatientName) ? "Clinical Demo Patient" : request.PatientName.Trim(),
                Audit = new AuditTrail { CreatedBy = "workflow", SourceModule = "IS.Clinical" }
            };

            await dbContext.Parties.AddAsync(patient, cancellationToken);
            partyId = patient.PartyId;
        }

        var orderId = _idGenerator.Create();
        var orderNumber = string.IsNullOrWhiteSpace(request.OrderNumber)
            ? FormattableString.Invariant($"ORD-{ShortId(orderId)}")
            : request.OrderNumber.Trim();

        var order = new MedicationOrder
        {
            MedicationOrderId = orderId,
            PartyId = partyId.Value,
            OrderNumber = orderNumber,
            Medication = request.Medication.Trim(),
            Dose = request.Dose.Trim(),
            Route = request.Route.Trim(),
            Frequency = request.Frequency.Trim(),
            Status = status,
            OrderingProvider = string.IsNullOrWhiteSpace(request.OrderingProvider) ? "Ordering provider" : request.OrderingProvider.Trim(),
            OrderedOnUtc = nowUtc,
            StartOnUtc = startOnUtc,
            Notes = request.Notes,
            Audit = new AuditTrail { CreatedBy = "workflow", SourceModule = "IS.Clinical" }
        };

        dbContext.MedicationOrders.Add(order);

        if (request.ScheduleFirstDose)
        {
            order.Administrations.Add(new MedicationAdministration
            {
                MedicationAdministrationId = _idGenerator.Create(),
                MedicationOrderId = orderId,
                ScheduledForUtc = startOnUtc,
                AdministeredOnUtc = null,
                Status = "Scheduled",
                PerformedBy = null,
                Notes = "Initial scheduled dose",
                Audit = new AuditTrail { CreatedBy = "workflow", SourceModule = "IS.Clinical" }
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var patientName = await dbContext.Parties
            .AsNoTracking()
            .Where(p => p.PartyId == partyId.Value)
            .Select(p => p.DisplayName)
            .FirstAsync(cancellationToken);

        return new MedicationOrderDto(
            order.MedicationOrderId,
            order.OrderNumber,
            string.IsNullOrWhiteSpace(patientName) ? "Patient" : patientName,
            order.Medication,
            order.Dose,
            order.Route,
            order.Frequency,
            order.Status,
            order.OrderingProvider,
            order.OrderedOnUtc,
            order.StartOnUtc);
    }

    public async Task<DispenseEventDto> RecordDispenseAsync(RecordDispenseRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        await EnsureSeedDataAsync(dbContext, cancellationToken);

        var order = await dbContext.MedicationOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.MedicationOrderId == request.MedicationOrderId, cancellationToken);

        if (order is null)
        {
            throw new InvalidOperationException("Medication order not found.");
        }

        var dispenseEvent = new DispenseEvent
        {
            DispenseEventId = _idGenerator.Create(),
            MedicationOrderId = order.MedicationOrderId,
            InventoryItem = request.InventoryItem.Trim(),
            Quantity = request.Quantity,
            Unit = request.Unit.Trim(),
            DispensedOnUtc = request.DispensedOnUtc ?? DateTime.UtcNow,
            DispensedBy = request.DispensedBy,
            Location = request.Location,
            Audit = new AuditTrail { CreatedBy = "workflow", SourceModule = "IS.Clinical" }
        };

        dbContext.DispenseEvents.Add(dispenseEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DispenseEventDto(
            dispenseEvent.DispenseEventId,
            dispenseEvent.MedicationOrderId,
            dispenseEvent.InventoryItem ?? string.Empty,
            dispenseEvent.Quantity,
            dispenseEvent.Unit,
            dispenseEvent.DispensedOnUtc,
            dispenseEvent.DispensedBy,
            dispenseEvent.Location);
    }

    public async Task<MedicationAdministrationDto> RecordAdministrationAsync(RecordAdministrationRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        await EnsureSeedDataAsync(dbContext, cancellationToken);

        var order = await dbContext.MedicationOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.MedicationOrderId == request.MedicationOrderId, cancellationToken);

        if (order is null)
        {
            throw new InvalidOperationException("Medication order not found.");
        }

        var administration = new MedicationAdministration
        {
            MedicationAdministrationId = _idGenerator.Create(),
            MedicationOrderId = order.MedicationOrderId,
            ScheduledForUtc = request.ScheduledForUtc,
            AdministeredOnUtc = request.AdministeredOnUtc,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Scheduled" : request.Status,
            PerformedBy = request.PerformedBy,
            Notes = request.Notes,
            Audit = new AuditTrail { CreatedBy = "workflow", SourceModule = "IS.Clinical" }
        };

        dbContext.MedicationAdministrations.Add(administration);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new MedicationAdministrationDto(
            administration.MedicationAdministrationId,
            administration.MedicationOrderId,
            order.OrderNumber,
            order.Medication,
            administration.ScheduledForUtc,
            administration.AdministeredOnUtc,
            administration.Status,
            administration.PerformedBy);
    }

    public async Task<MedicationAdministrationDto?> CompleteNextAdministrationAsync(Guid medicationOrderId, string performedBy, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        await EnsureSeedDataAsync(dbContext, cancellationToken);

        var admin = await dbContext.MedicationAdministrations
            .Where(a => a.MedicationOrderId == medicationOrderId && a.AdministeredOnUtc == null)
            .OrderBy(a => a.ScheduledForUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (admin is null)
        {
            return null;
        }

        admin.AdministeredOnUtc = DateTime.UtcNow;
        admin.Status = "Completed";
        admin.PerformedBy = string.IsNullOrWhiteSpace(performedBy) ? "RN" : performedBy;
        admin.Audit.ModifiedOnUtc = DateTime.UtcNow;
        admin.Audit.ModifiedBy = performedBy;

        await dbContext.SaveChangesAsync(cancellationToken);

        var order = await dbContext.MedicationOrders
            .AsNoTracking()
            .FirstAsync(o => o.MedicationOrderId == medicationOrderId, cancellationToken);

        return new MedicationAdministrationDto(
            admin.MedicationAdministrationId,
            admin.MedicationOrderId,
            order.OrderNumber,
            order.Medication,
            admin.ScheduledForUtc,
            admin.AdministeredOnUtc,
            admin.Status,
            admin.PerformedBy);
    }

    private static string ShortId(Guid id)
    {
        return id.ToString("N").Substring(0, 8);
    }
}

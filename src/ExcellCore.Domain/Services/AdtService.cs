using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Services;

public sealed class AdtService : IAdtService
{
    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;
    private readonly ISequentialGuidGenerator _idGenerator;

    public AdtService(IDbContextFactory<ExcellCoreContext> contextFactory, ISequentialGuidGenerator idGenerator)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<BedBoardDto> GetBedBoardAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSeedAdtDataAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var wards = await dbContext.Wards
            .AsNoTracking()
            .Include(w => w.Rooms)
            .ThenInclude(r => r.Beds)
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var wardDtos = wards.Select(w => new WardDto(
            w.WardId,
            w.Name,
            w.Rooms
                .OrderBy(r => r.RoomNumber)
                .Select(r => new RoomDto(
                    r.RoomId,
                    r.RoomNumber,
                    r.Beds
                        .OrderBy(b => b.BedNumber)
                        .Select(ToBedDto)
                        .ToList()))
                .ToList()))
            .ToList();

        var beds = wardDtos.SelectMany(x => x.Rooms).SelectMany(r => r.Beds).ToList();
        var summary = new BedBoardSummaryDto(
            beds.Count,
            beds.Count(b => b.OccupiedByPatientId.HasValue),
            beds.Count(b => b.IsIsolation));

        return new BedBoardDto(wardDtos, summary);
    }

    public async Task<AdtResult> AdmitAsync(AdmitRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSeedAdtDataAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var bed = await FindBedAsync(dbContext, request.WardName, request.RoomNumber, request.BedNumber, cancellationToken)
            ?? throw new InvalidOperationException("Requested bed was not found.");

        if (bed.OccupiedByPartyId.HasValue)
        {
            throw new InvalidOperationException("Bed is already occupied.");
        }

        var patient = new Party
        {
            PartyId = _idGenerator.Create(),
            PartyType = "Patient",
            DisplayName = string.IsNullOrWhiteSpace(request.PatientName) ? "Patient" : request.PatientName,
            Audit = AuditTrail.ForCreate(request.PerformedBy ?? "adt")
        };

        await dbContext.Parties.AddAsync(patient, cancellationToken).ConfigureAwait(false);

        bed.Status = "Occupied";
        bed.IsIsolation = request.IsIsolation;
        bed.OccupiedByPartyId = patient.PartyId;
        bed.OccupiedOnUtc = DateTime.UtcNow;
        bed.Audit = bed.Audit.Update(request.PerformedBy ?? "adt");

        await RecordOccupancyTelemetryAsync(dbContext, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdtResult(bed.BedId, patient.PartyId, bed.Status, bed.IsIsolation, bed.OccupiedOnUtc);
    }

    public async Task<AdtResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSeedAdtDataAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var fromBed = await dbContext.Beds.FirstOrDefaultAsync(b => b.BedId == request.FromBedId, cancellationToken)
            ?? throw new InvalidOperationException("Source bed not found.");

        if (!fromBed.OccupiedByPartyId.HasValue)
        {
            throw new InvalidOperationException("Source bed is not occupied.");
        }

        var toBed = await FindBedAsync(dbContext, request.ToWardName, request.ToRoomNumber, request.ToBedNumber, cancellationToken)
            ?? throw new InvalidOperationException("Target bed not found.");

        if (toBed.OccupiedByPartyId.HasValue)
        {
            throw new InvalidOperationException("Target bed is occupied.");
        }

        var patientId = fromBed.OccupiedByPartyId;

        fromBed.Status = "Available";
        fromBed.IsIsolation = false;
        fromBed.OccupiedByPartyId = null;
        fromBed.OccupiedOnUtc = null;
        fromBed.Audit = fromBed.Audit.Update(request.PerformedBy ?? "adt");

        toBed.Status = "Occupied";
        toBed.OccupiedByPartyId = patientId;
        toBed.OccupiedOnUtc = DateTime.UtcNow;
        toBed.Audit = toBed.Audit.Update(request.PerformedBy ?? "adt");

        await RecordOccupancyTelemetryAsync(dbContext, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdtResult(toBed.BedId, patientId!.Value, toBed.Status, toBed.IsIsolation, toBed.OccupiedOnUtc);
    }

    public async Task DischargeAsync(DischargeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSeedAdtDataAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var bed = await dbContext.Beds.FirstOrDefaultAsync(b => b.BedId == request.BedId, cancellationToken)
            ?? throw new InvalidOperationException("Bed not found.");

        bed.Status = "Available";
        bed.IsIsolation = false;
        bed.OccupiedByPartyId = null;
        bed.OccupiedOnUtc = null;
        bed.Audit = bed.Audit.Update(request.PerformedBy ?? "adt");

        await RecordOccupancyTelemetryAsync(dbContext, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static BedDto ToBedDto(Bed bed) => new(bed.BedId, bed.BedNumber, bed.Status ?? "Available", bed.IsIsolation, bed.OccupiedByPartyId, bed.OccupiedOnUtc);

    private async Task EnsureSeedAdtDataAsync(ExcellCoreContext dbContext, CancellationToken cancellationToken)
    {
        var hasBeds = await dbContext.Beds.AnyAsync(cancellationToken).ConfigureAwait(false);
        if (hasBeds)
        {
            return;
        }

        var wardId = _idGenerator.Create();
        var rooms = new List<Room>
        {
            new()
            {
            RoomId = _idGenerator.Create(),
                WardId = wardId,
                RoomNumber = "101",
                Beds = new List<Bed>
                {
                    new()
                    {
                        BedId = _idGenerator.Create(),
                        BedNumber = "A",
                        Status = "Available",
                        Audit = AuditTrail.ForCreate("seed")
                    },
                    new()
                    {
                        BedId = _idGenerator.Create(),
                        BedNumber = "B",
                        Status = "Available",
                        Audit = AuditTrail.ForCreate("seed")
                    }
                },
                Audit = AuditTrail.ForCreate("seed")
            },
            new()
            {
                RoomId = _idGenerator.Create(),
                WardId = wardId,
                RoomNumber = "102",
                Beds = new List<Bed>
                {
                    new()
                    {
                        BedId = _idGenerator.Create(),
                        BedNumber = "A",
                        Status = "Available",
                        Audit = AuditTrail.ForCreate("seed")
                    }
                },
                Audit = AuditTrail.ForCreate("seed")
            }
        };

        var ward = new Ward
        {
            WardId = wardId,
            Name = "General",
            Rooms = rooms,
            Audit = AuditTrail.ForCreate("seed")
        };

        await dbContext.Wards.AddAsync(ward, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Bed?> FindBedAsync(ExcellCoreContext dbContext, string wardName, string roomNumber, string bedNumber, CancellationToken cancellationToken)
    {
        var bed = await dbContext.Beds
            .Include(b => b.Room)
            .ThenInclude(r => r.Ward)
            .FirstOrDefaultAsync(b => b.BedNumber == bedNumber, cancellationToken)
            .ConfigureAwait(false);

        if (bed?.Room?.Ward?.Name != wardName || bed.Room?.RoomNumber != roomNumber)
        {
            return null;
        }

        return bed;
    }

    private static async Task RecordOccupancyTelemetryAsync(ExcellCoreContext dbContext, CancellationToken cancellationToken)
    {
        var occupied = await dbContext.Beds.CountAsync(b => b.OccupiedByPartyId != null, cancellationToken).ConfigureAwait(false);
        var total = await dbContext.Beds.CountAsync(cancellationToken).ConfigureAwait(false);

        var telemetry = new TelemetryEvent
        {
            TelemetryEventId = Guid.NewGuid(),
            EventType = "BedOccupancy",
            DurationMilliseconds = occupied,
            CommandText = FormattableString.Invariant($"occupied:{occupied};total:{total}"),
            OccurredOnUtc = DateTime.UtcNow,
            Audit = AuditTrail.ForCreate("adt")
        };

        await dbContext.TelemetryEvents.AddAsync(telemetry, cancellationToken).ConfigureAwait(false);
    }
}

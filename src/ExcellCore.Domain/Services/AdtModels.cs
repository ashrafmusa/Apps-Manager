using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Services;

public sealed record AdmitRequest(string WardName, string RoomNumber, string BedNumber, string PatientName, bool IsIsolation = false, string? PerformedBy = "adt-service");

public sealed record TransferRequest(Guid FromBedId, string ToWardName, string ToRoomNumber, string ToBedNumber, string? PerformedBy = "adt-service");

public sealed record DischargeRequest(Guid BedId, string? PerformedBy = "adt-service");

public sealed record AdtResult(Guid BedId, Guid PatientId, string Status, bool IsIsolation, DateTime? OccupiedOnUtc);

public sealed record BedBoardDto(IReadOnlyList<WardDto> Wards, BedBoardSummaryDto Summary);

public sealed record WardDto(Guid WardId, string Name, IReadOnlyList<RoomDto> Rooms);

public sealed record RoomDto(Guid RoomId, string Number, IReadOnlyList<BedDto> Beds);

public sealed record BedDto(Guid BedId, string BedNumber, string Status, bool IsIsolation, Guid? OccupiedByPatientId, DateTime? OccupiedSinceUtc);

public sealed record BedBoardSummaryDto(int TotalBeds, int OccupiedBeds, int IsolationBeds);

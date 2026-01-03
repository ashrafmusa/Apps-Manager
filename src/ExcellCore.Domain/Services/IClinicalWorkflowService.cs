using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Domain.Services;

public interface IClinicalWorkflowService
{
    Task<ClinicalDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<PatientJourneyDto> GetPatientJourneyAsync(Guid? patientId = null, CancellationToken cancellationToken = default);

    Task<MedicationOrderDto> CreateOrderAsync(CreateMedicationOrderRequest request, CancellationToken cancellationToken = default);

    Task<DispenseEventDto> RecordDispenseAsync(RecordDispenseRequest request, CancellationToken cancellationToken = default);

    Task<MedicationAdministrationDto> RecordAdministrationAsync(RecordAdministrationRequest request, CancellationToken cancellationToken = default);

    Task<MedicationAdministrationDto?> CompleteNextAdministrationAsync(Guid medicationOrderId, string performedBy, CancellationToken cancellationToken = default);
}

using FinOps.Models;

namespace FinOps.Services;

public interface IReservationService
{
    Task<ReservationAnalysis> GetReservationCandidatesAsync(TenantSubscription subscription);
}

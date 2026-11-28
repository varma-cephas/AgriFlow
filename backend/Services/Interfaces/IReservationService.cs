using backend.Models.Reservations;

namespace backend.Services.Interfaces;

public interface IReservationService
{
    Task<ReserveResult> ReserveAsync(int userId, int businessId, ReserveRequest request, CancellationToken ct = default);
}
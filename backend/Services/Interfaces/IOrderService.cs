using backend.Models.Orders;

namespace backend.Services.Interfaces;

public interface IOrderService
{
    Task<PlaceOrderResult> PlaceOrderAsync(int userId, int businessId, PlaceOrderRequest request, CancellationToken ct = default);
}
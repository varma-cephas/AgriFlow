using System.ComponentModel.DataAnnotations;
using backend.Models.Orders;

namespace backend.Models.Reservations;

public class ReserveRequest
{
    public int ListingId { get; set; }
    public decimal Quantity { get; set; }

    [Required, MaxLength(250)]
    public string DeliveryAddress { get; set; } = string.Empty;
}

public record ReserveResponse(
    string OrderNumber,
    int ListingId,
    string ListingCode,
    string Crop,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,          // RWF, copied from the listing
    decimal EstimatedValue,     // RWF, quantity x unit price
    DateOnly ExpectedHarvestDate,
    string Status);

public record ReserveResult(ServiceOutcome Outcome, string Message, ReserveResponse? Data = null);
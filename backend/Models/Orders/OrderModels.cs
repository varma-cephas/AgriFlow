using System.ComponentModel.DataAnnotations;

namespace backend.Models.Orders;

public enum ServiceOutcome { Ok, BadRequest, Forbidden, NotFound, Conflict }

public class PlaceOrderRequest
{
    public int ListingId { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly DeliveryDate { get; set; }   // "2026-12-10"

    [Required, MaxLength(250)]
    public string DeliveryAddress { get; set; } = string.Empty;
}

public record PlaceOrderResponse(
    string OrderNumber,
    int ListingId,
    string ListingCode,
    string Crop,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,     // RWF
    decimal TotalAmount,   // RWF
    DateOnly DeliveryDate,
    string Status);

public record PlaceOrderResult(ServiceOutcome Outcome, string Message, PlaceOrderResponse? Data = null);
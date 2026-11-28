using System.ComponentModel.DataAnnotations;

namespace backend.Models.Agent
{
    public record FarmerSeedStockWorkspaceResponse(
        int FarmerId,
        string FarmerName,
        string FarmerInitials,
        string FarmName,
        string District,
        List<SeedStockItemDto> Items,
        List<CropOptionDto> AvailableCrops
    );

    public record SeedStockItemDto(
        int Id,
        int CropId,
        string CropName,
        string Variety,
        string? Supplier,
        decimal Quantity,
        string Unit,
        int UsedByBatchesCount,
        bool IsLowStock,
        string LastUpdatedDate,
        string LastUpdatedByName,
        bool LastUpdatedByIsAgent
    );

    public record CropOptionDto(
        int Id,
        string Name,
        int MaturityDays
    );

    public record UpsertSeedStockRequest(
        [Required] int CropId,
        [Required, MaxLength(80)] string Variety,
        [MaxLength(120)] string? Supplier,
        [Range(0, 1000000)] decimal Quantity,
        [Required, MaxLength(10)] string Unit
    );
}

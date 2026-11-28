using System.ComponentModel.DataAnnotations;

namespace backend.Models.Agent
{
    public record FarmVisitWorkspaceResponse(
        FarmVisitKpisDto Kpis,
        List<FarmVisitRowDto> Items
    );

    public record FarmVisitKpisDto(
        int WaitingCount,
        int DueThisWeekCount,
        int DoneThisMonthCount,
        int CouldNotVerifyCount
    );

    public record FarmVisitRowDto(
        int Id,
        int FarmerId,
        string FarmerName,
        string FarmerInitials,
        string FarmName,
        string DistrictSector,
        string CellVillage,
        string LandUpi,
        string VisitByDate,
        string RequestedByName,
        string Status,          // "Requested" | "Done" | "Cancelled"
        string? Note,
        string? CompletedDate
    );

    public record ConfirmFarmVisitRequest(
        [Required] DateOnly VisitDate,
        [Required, MaxLength(500)] string Findings
    );

    public record RejectFarmVisitRequest(
        [Required, MaxLength(500)] string Reason
    );
}

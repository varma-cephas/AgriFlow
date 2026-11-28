namespace backend.Models.Agent
{
    public record AgentOverviewResponse(
        AgentIdentityDto Agent,
        NextVisitDto? NextVisit,
        AgentKpisDto Kpis,
        List<FarmerGlanceDto> FarmersAtAGlance,
        List<AttentionItemDto> AttentionItems,
        List<AssignedFarmerRowDto> MyFarmers
    );

    public record AgentIdentityDto(
        int UserId,
        string FullName,
        string DistrictLabel,
        string RoleTitle
    );

    public record NextVisitDto(
        int VisitRequestId,
        int FarmerId,
        string FarmerName,
        string VisitBy,
        string RelativeLabel,
        string? Note
    );

    public record AgentKpisDto(
        int AssignedFarmersTotal,
        int NonDigitalFarmersCount,
        int NewFarmersThisMonth,
        int ActiveBatchesTotal,
        int HarvestsDueWithin14Days,
        int OrdersToHandleTotal,
        int VisitsThisWeekCount
    );

    public record FarmerGlanceDto(
        int FarmerId,
        string Initials,
        string FullName,
        string FarmName,
        string District,
        int SeasonProgressPercent,
        string StatusBadge
    );

    public record AttentionItemDto(
        string Id,
        string Category,      // "HarvestDue" | "LowSeed" | "OrderToConfirm" | "VisitPlanned"
        string Title,
        string Subtitle,
        string ActionLabel,   // "Record" | "Restock" | "Confirm" | "View"
        int FarmerId
    );

    public record AssignedFarmerRowDto(
        int FarmerId,
        string Initials,
        string FullName,
        bool IsNonDigital,
        string AccountBadge,  // "Non-digital" | "Self-manages too"
        string FarmName,
        string District,
        string Phone,
        int ActiveBatchesCount,
        int LowSeedCount,
        int OrdersToConfirmCount
    );
}

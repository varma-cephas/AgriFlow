namespace backend.Models.Agent
{
    public class AgentAuditLogResponse
    {
    }
    public record AgentAuditLogItemDto(
    long Id,
    string TimeDisplay,
    string IsoTimestamp,
    string ActorName,
    string ActorInitials,
    string ActorRole,
    string? OnBehalfOfFarmerName,
    string ActionCategory,
    string ActionLabel,
    string Summary,
    string Result
);
}

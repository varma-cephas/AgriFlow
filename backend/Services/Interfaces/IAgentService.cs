using backend.Models.Agent;

namespace backend.Services.Interfaces
{
    public enum ProxyAccessResult
    {
        Allowed,
        FarmerNotFound,
        NotAssigned
    }

    public record ProxyCheckOutcome(
        ProxyAccessResult Status,
        string? FarmerName = null
    );

    public interface IAgentService
    {
        Task<AgentOverviewResponse?> GetOverviewAsync(int agentUserId, CancellationToken ct = default);
        Task<List<AssignedFarmerRowDto>> GetMyFarmersAsync(int agentUserId, CancellationToken ct = default);

        Task<(ProxyCheckOutcome Access, FarmerSeedStockWorkspaceResponse? Data)> GetFarmerSeedStockAsync(
            int agentUserId, int farmerId, CancellationToken ct = default);

        Task<(ProxyCheckOutcome Access, bool Success, string Message, SeedStockItemDto? Item)> AddFarmerSeedStockAsync(
            int agentUserId, int farmerId, UpsertSeedStockRequest req, CancellationToken ct = default);

        Task<(ProxyCheckOutcome Access, bool Success, string Message, SeedStockItemDto? Item)> UpdateFarmerSeedStockAsync(
            int agentUserId, int farmerId, int seedStockId, UpsertSeedStockRequest req, CancellationToken ct = default);

        Task<(ProxyCheckOutcome Access, bool Success, string Message)> DeleteFarmerSeedStockAsync(
            int agentUserId, int farmerId, int seedStockId, CancellationToken ct = default);

        Task<(bool Success, string Message)> RequestFarmerAssignmentAsync(
            int agentUserId, int farmerId, CancellationToken ct = default);
        Task<FarmVisitWorkspaceResponse> GetFarmVisitRequestsAsync(
    int agentUserId, CancellationToken ct = default);

        Task<(bool Success, string Message)> ConfirmFarmVisitAsync(
            int agentUserId, int visitId, ConfirmFarmVisitRequest req, CancellationToken ct = default);

        Task<(bool Success, string Message)> RejectFarmVisitAsync(
            int agentUserId, int visitId, RejectFarmVisitRequest req, CancellationToken ct = default);
        Task<(bool Success, string Message, RegisterNonDigitalFarmerResponse? Data)> RegisterNonDigitalFarmerAsync(
    int agentUserId, RegisterNonDigitalFarmerRequest req, CancellationToken ct = default);
    }
}

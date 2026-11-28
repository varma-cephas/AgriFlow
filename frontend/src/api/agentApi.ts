import axiosClient from './axiosClient';
import type {
  AgentOverviewResponse,
  AssignedFarmerRow,
  FarmerSeedStockWorkspace,
  SeedStockItem,
  UpsertSeedStockPayload,
  FarmVisitWorkspaceResponse,
  ConfirmFarmVisitPayload,
  RejectFarmVisitPayload,
  RegisterNonDigitalFarmerPayload,
  RegisterNonDigitalFarmerResponse,
  AgentAuditLogItem
} from '../types/agent';

export const agentApi = {
  getOverview: async () => {
    const { data } = await axiosClient.get<AgentOverviewResponse>('/agents/overview');
    return data;
  },

  getMyFarmers: async () => {
    const { data } = await axiosClient.get<AssignedFarmerRow[]>('/agents/my-farmers');
    return data;
  },

  getFarmerSeedStock: async (farmerId: number) => {
    const { data } = await axiosClient.get<FarmerSeedStockWorkspace>(
      `/agents/farmers/${farmerId}/seed-stock`
    );
    return data;
  },

  addFarmerSeedStock: async (farmerId: number, payload: UpsertSeedStockPayload) => {
    const { data } = await axiosClient.post<SeedStockItem>(
      `/agents/farmers/${farmerId}/seed-stock`,
      payload
    );
    return data;
  },

  updateFarmerSeedStock: async (
    farmerId: number,
    seedStockId: number,
    payload: UpsertSeedStockPayload
  ) => {
    const { data } = await axiosClient.put<SeedStockItem>(
      `/agents/farmers/${farmerId}/seed-stock/${seedStockId}`,
      payload
    );
    return data;
  },

  deleteFarmerSeedStock: async (farmerId: number, seedStockId: number) => {
    const { data } = await axiosClient.delete<{ message: string }>(
      `/agents/farmers/${farmerId}/seed-stock/${seedStockId}`
    );
    return data;
  },

  requestFarmerAssignment: async (farmerId: number) => {
    const { data } = await axiosClient.post<{ message: string }>(
      `/agents/farmers/${farmerId}/request-assignment`
    );
    return data;
  },

  getFarmVisitRequests: async () => {
    const { data } = await axiosClient.get<FarmVisitWorkspaceResponse>('/agents/farm-visits');
    return data;
  },

  confirmFarmVisit: async (visitId: number, payload: ConfirmFarmVisitPayload) => {
    const { data } = await axiosClient.post<{ message: string }>(
      `/agents/farm-visits/${visitId}/confirm`,
      payload
    );
    return data;
  },

  rejectFarmVisit: async (visitId: number, payload: RejectFarmVisitPayload) => {
    const { data } = await axiosClient.post<{ message: string }>(
      `/agents/farm-visits/${visitId}/reject`,
      payload
    );
    return data;
  },


  registerFarmer: async (payload: RegisterNonDigitalFarmerPayload) => {
    const { data } = await axiosClient.post<RegisterNonDigitalFarmerResponse>(
      '/agents/farmers',
      payload
    );
    return data;
  },

  getAuditLogs: async (params?: {
    search?: string;
    role?: string;
    action?: string;
    dateRange?: string;
  }) => {
    const { data } = await axiosClient.get<AgentAuditLogItem[]>('/agents/audit-logs', {
      params,
    });
    return data;
  },
};


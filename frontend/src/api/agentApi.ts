import { isAxiosError } from 'axios';
import client from './axiosClient';

export type Need = { type: 'HarvestDue' | 'LowSeed' | 'OrderToConfirm'; message: string };

export interface FarmerSummary {
  farmerId: number;
  name: string;
  farm: string;
  district: string;
  phone: string | null;
  activeBatches: number;
  lowStockCount: number;
  ordersToConfirm: number;
  seasonProgress: number;
  needs: Need[];
}

export interface AgentDashboardData {
  agent: { id: number; name: string; district: string };
  kpis: {
    assignedFarmers: number;
    activeBatches: number;
    harvestsDueIn14Days: number;
    ordersToHandle: number;
  };
  farmers: FarmerSummary[];
  needsAttention: { farmerId: number; farmer: string; need: Need }[];
}

export type VisitFilter = 'Requested' | 'Done' | 'All';

export interface FarmVisit {
  id: number;
  farmer: { id: number; name: string; farm: string; phone: string | null };
  location: { province: string; district: string; sector: string; cell: string; village: string };
  landUpi: string;
  visitBy: string;
  requestedBy: { id: number; name: string };
  note: string | null;
  status: 'Requested' | 'Done' | 'Cancelled';
  completedAt: string | null;
}

export function agentError(error: unknown) {
  if (isAxiosError(error)) {
    return error.response?.data?.message || error.response?.data?.title ||
      (error.response?.status === 401 ? 'Your session has expired. Please sign in again.' :
        'AgriFlow could not load or save this information. Please try again.');
  }
  return 'Something went wrong. Please try again.';
}

export const agentApi = {
  dashboard: (signal: AbortSignal) =>
    client.get<AgentDashboardData>('/agent/dashboard', { signal }).then(r => r.data),
  farmers: (signal: AbortSignal) =>
    client.get<FarmerSummary[]>('/agent/farmers', { signal }).then(r => r.data),
  farmer: (id: string, signal: AbortSignal) =>
    client.get<FarmerSummary>(`/agent/farmers/${id}`, { signal }).then(r => r.data),
  visits: (status: VisitFilter, signal: AbortSignal) =>
    client.get<FarmVisit[]>('/agent/visit-requests', { params: { status }, signal }).then(r => r.data),
  completeVisit: (id: number, visitDate: string, findings: string) =>
    client.post(`/agent/visit-requests/${id}/complete`, { visitDate, findings }),
  cannotVerify: (id: number, reason: string) =>
    client.post(`/agent/visit-requests/${id}/cannot-verify`, { reason }),
};

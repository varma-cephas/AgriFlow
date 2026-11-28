export interface AgentIdentity {
  userId: number;
  fullName: string;
  districtLabel: string;
  roleTitle: string;
}

export interface NextVisit {
  visitRequestId: number;
  farmerId: number;
  farmerName: string;
  visitBy: string;
  relativeLabel: string;
  note?: string | null;
}

export interface AgentKpis {
  assignedFarmersTotal: number;
  nonDigitalFarmersCount: number;
  newFarmersThisMonth: number;
  activeBatchesTotal: number;
  harvestsDueWithin14Days: number;
  ordersToHandleTotal: number;
  visitsThisWeekCount: number;
}

export type FarmerGlanceStatus =
  | 'On track'
  | 'Harvest due'
  | 'Low seed'
  | 'Order to confirm'
  | 'Visit planned';

export interface FarmerGlance {
  farmerId: number;
  initials: string;
  fullName: string;
  farmName: string;
  district: string;
  seasonProgressPercent: number;
  statusBadge: FarmerGlanceStatus;
}

export type AttentionCategory =
  | 'HarvestDue'
  | 'LowSeed'
  | 'OrderToConfirm'
  | 'VisitPlanned';

export interface AttentionItem {
  id: string;
  category: AttentionCategory;
  title: string;
  subtitle: string;
  actionLabel: string;
  farmerId: number;
}

export interface AssignedFarmerRow {
  farmerId: number;
  initials: string;
  fullName: string;
  isNonDigital: boolean;
  accountBadge: 'Non-digital' | 'Self-manages too';
  farmName: string;
  district: string;
  phone: string;
  activeBatchesCount: number;
  lowSeedCount: number;
  ordersToConfirmCount: number;
}

export interface AgentOverviewResponse {
  agent: AgentIdentity;
  nextVisit: NextVisit | null;
  kpis: AgentKpis;
  farmersAtAGlance: FarmerGlance[];
  attentionItems: AttentionItem[];
  myFarmers: AssignedFarmerRow[];
}


export interface SeedStockItem {
  id: number;
  cropId: number;
  cropName: string;
  variety: string;
  supplier?: string | null;
  quantity: number;
  unit: string;
  usedByBatchesCount: number;
  isLowStock: boolean;
  lastUpdatedDate: string;
  lastUpdatedByName: string;
  lastUpdatedByIsAgent: boolean;
}

export interface CropOption {
  id: number;
  name: string;
  maturityDays: number;
}

export interface FarmerSeedStockWorkspace {
  farmerId: number;
  farmerName: string;
  farmerInitials: string;
  farmName: string;
  district: string;
  items: SeedStockItem[];
  availableCrops: CropOption[];
}

export interface UpsertSeedStockPayload {
  cropId: number;
  variety: string;
  supplier?: string | null;
  quantity: number;
  unit: string;
}


export type FarmVisitStatus = 'Requested' | 'Done' | 'Cancelled';

export interface FarmVisitKpis {
  waitingCount: number;
  dueThisWeekCount: number;
  doneThisMonthCount: number;
  couldNotVerifyCount: number;
}

export interface FarmVisitRow {
  id: number;
  farmerId: number;
  farmerName: string;
  farmerInitials: string;
  farmName: string;
  districtSector: string;
  cellVillage: string;
  landUpi: string;
  visitByDate: string;
  requestedByName: string;
  status: FarmVisitStatus;
  note?: string | null;
  completedDate?: string | null;
}

export interface FarmVisitWorkspaceResponse {
  kpis: FarmVisitKpis;
  items: FarmVisitRow[];
}

export interface ConfirmFarmVisitPayload {
  visitDate: string; // YYYY-MM-DD
  findings: string;
}

export interface RejectFarmVisitPayload {
  reason: string;
}

export interface RegisterNonDigitalFarmerPayload {
  fullName: string;
  phone?: string;
  village: string;
  preferredLanguage: string;
  farmName: string;
  province: string;
  district: string;
  farmSizeAcres?: number;
  consentConfirmed: boolean;
}

export interface RegisterNonDigitalFarmerResponse {
  farmerId: number;
  fullName: string;
  firstName: string;
  farmName: string;
  message: string;
}
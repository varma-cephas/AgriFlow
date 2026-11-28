import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { RefreshCw, LogOut, Package, Plus, Info, X } from 'lucide-react';
import { AgentLayout } from '../../components/agent/AgentLayout';
import { SwitchFarmerModal } from '../../components/agent/SwitchFarmerModal';
import { NotAssignedModal } from '../../components/agent/NotAssignedModal';
import { SeedStockFormModal } from '../../components/agent/SeedStockFormModal';
import { agentApi } from '../../api/agentApi';
import type {
  AssignedFarmerRow,
  FarmerSeedStockWorkspace,
  SeedStockItem,
  UpsertSeedStockPayload,
} from '../../types/agent';

const CROP_BADGE_COLORS = [
  'bg-red-50 text-red-700 border-red-200/70',
  'bg-emerald-50 text-emerald-700 border-emerald-200/70',
  'bg-amber-50 text-amber-700 border-amber-200/70',
  'bg-orange-50 text-orange-700 border-orange-200/70',
];

const FARMER_TABS = [
  { id: 'overview', label: 'Overview', active: false },
  { id: 'seed-stock', label: 'Seed stock', active: true },
  { id: 'batches', label: 'Planting batches', active: false },
  { id: 'listings', label: 'Listings', active: false },
  { id: 'orders', label: 'Sales & orders', active: false },
];

export const AgentFarmerSeedStockPage: React.FC = () => {
  const { farmerId } = useParams<{ farmerId: string }>();
  const navigate = useNavigate();
  const numericFarmerId = Number(farmerId);

  const [workspace, setWorkspace] = useState<FarmerSeedStockWorkspace | null>(null);
  const [assignedFarmers, setAssignedFarmers] = useState<AssignedFarmerRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  // Modals
  const [switchOpen, setSwitchOpen] = useState(false);
  const [formOpen, setFormOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<SeedStockItem | null>(null);
  const [notAssignedState, setNotAssignedState] = useState<{
    open: boolean;
    farmerName: string;
  }>({ open: false, farmerName: '' });

  const loadWorkspace = useCallback(async () => {
    if (!numericFarmerId) return;
    setLoading(true);
    try {
      const [ws, farmersList] = await Promise.all([
        agentApi.getFarmerSeedStock(numericFarmerId),
        agentApi.getMyFarmers(),
      ]);
      setWorkspace(ws);
      setAssignedFarmers(farmersList);
      setNotAssignedState({ open: false, farmerName: '' });
    } catch (err: any) {
      if (err.response?.status === 403 && err.response?.data?.code === 'NOT_ASSIGNED') {
        setNotAssignedState({
          open: true,
          farmerName: err.response.data.farmerName ?? 'this farmer',
        });
      } else {
        setToastMessage(err.response?.data?.message || 'Unable to load farmer seed stock.');
      }
    } finally {
      setLoading(false);
    }
  }, [numericFarmerId]);

  useEffect(() => {
    loadWorkspace();
  }, [loadWorkspace]);

  const filteredItems = useMemo(() => {
    if (!workspace) return [];
    const q = searchQuery.trim().toLowerCase();
    if (!q) return workspace.items;
    return workspace.items.filter(
      (item) =>
        item.cropName.toLowerCase().includes(q) ||
        item.variety.toLowerCase().includes(q) ||
        (item.supplier ?? '').toLowerCase().includes(q)
    );
  }, [workspace, searchQuery]);

  const handleSaveSeedStock = async (
    payload: UpsertSeedStockPayload,
    existingId?: number
  ) => {
    if (!workspace) return;
    if (existingId) {
      await agentApi.updateFarmerSeedStock(workspace.farmerId, existingId, payload);
      setToastMessage('Seed stock updated and logged in audit trail.');
    } else {
      await agentApi.addFarmerSeedStock(workspace.farmerId, payload);
      setToastMessage('New seed stock added to farmer records.');
    }
    await loadWorkspace();
  };

  const handleDelete = async (item: SeedStockItem) => {
    if (!workspace) return;
    if (!window.confirm(`Delete ${item.cropName} · ${item.variety} from ${workspace.farmerName}'s seed stock?`)) {
      return;
    }
    try {
      await agentApi.deleteFarmerSeedStock(workspace.farmerId, item.id);
      setToastMessage(`Removed ${item.cropName} · ${item.variety}.`);
      await loadWorkspace();
    } catch (err: any) {
      setToastMessage(err.response?.data?.message || 'Unable to delete seed stock.');
    }
  };

  const breadcrumbText = workspace
    ? `My farmers / ${workspace.farmerName} / Seed stock`
    : 'My farmers / Seed stock';

  const firstName = workspace?.farmerName.split(' ')[0] ?? 'Farmer';

  return (
    <AgentLayout
      breadcrumb={breadcrumbText}
      searchQuery={searchQuery}
      onSearchChange={setSearchQuery}
      onComingSoon={(label) =>
        setToastMessage(`${label} will be connected in the next implementation step.`)
      }
    >
      <div className="space-y-5">
        {/* Amber Proxy Banner ("You are acting for ...") */}
        {workspace && (
          <div className="rounded-xl bg-[#fbf2d8] border border-amber-300/80 px-4 py-3 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <div className="w-8 h-8 rounded-full bg-agri-700 text-white flex items-center justify-center text-xs font-bold shrink-0">
                {workspace.farmerInitials}
              </div>
              <div className="text-xs">
                <p className="font-bold text-slate-900">
                  You are acting for {workspace.farmerName} · {workspace.farmName}
                </p>
                <p className="text-slate-600 mt-0.5">
                  Every change you make is saved in {firstName}&apos;s records and logged with your
                  name.
                </p>
              </div>
            </div>

            <div className="flex items-center gap-2 shrink-0">
              <button
                type="button"
                onClick={() => setSwitchOpen(true)}
                className="inline-flex items-center gap-1.5 rounded-lg bg-white hover:bg-slate-50 border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-800 shadow-2xs transition cursor-pointer"
              >
                <RefreshCw className="w-3.5 h-3.5 text-slate-600" />
                Switch farmer
              </button>

              <button
                type="button"
                onClick={() => navigate('/agent/dashboard')}
                className="inline-flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-xs font-semibold text-slate-700 hover:bg-amber-200/40 transition cursor-pointer"
              >
                <LogOut className="w-3.5 h-3.5" />
                Exit
              </button>
            </div>
          </div>
        )}

        {/* Toast Feedback */}
        {toastMessage && (
          <div className="rounded-xl bg-blue-50 border border-blue-200 px-4 py-3 flex items-center justify-between gap-3 text-xs text-blue-900">
            <div className="flex items-center gap-2">
              <Info className="w-4 h-4 text-blue-600 shrink-0" />
              <span>{toastMessage}</span>
            </div>
            <button
              type="button"
              onClick={() => setToastMessage(null)}
              className="text-blue-600 hover:text-blue-800 cursor-pointer"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {/* Sub-Workspace Pill Tabs */}
        <div className="flex flex-wrap items-center gap-2">
          {FARMER_TABS.map((tab) => (
            <button
              key={tab.id}
              type="button"
              onClick={() => {
                if (!tab.active) {
                  setToastMessage(
                    `${tab.label} tab for ${workspace?.farmerName ?? 'farmer'} is queued next.`
                  );
                }
              }}
              className={`rounded-full px-4 py-1.5 text-xs font-semibold transition cursor-pointer ${
                tab.active
                  ? 'bg-agri-700 text-white shadow-2xs'
                  : 'bg-white border border-slate-200 text-slate-600 hover:bg-slate-50'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {/* Page Title + Add Seed Stock CTA */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pt-1">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-emerald-50 border border-emerald-200/80 flex items-center justify-center text-agri-700 shrink-0">
              <Package className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-xl font-bold text-slate-900 tracking-tight">
                {firstName}&apos;s seed stock
              </h1>
              <p className="text-xs text-slate-500 mt-0.5">
                Changes are saved to {firstName}&apos;s records.
              </p>
            </div>
          </div>

          <button
            type="button"
            disabled={!workspace}
            onClick={() => {
              setEditingItem(null);
              setFormOpen(true);
            }}
            className="inline-flex items-center gap-1.5 rounded-lg bg-agri-700 hover:bg-agri-800 disabled:opacity-50 text-white font-semibold text-xs px-4 py-2.5 transition cursor-pointer self-start sm:self-auto"
          >
            <Plus className="w-4 h-4" />
            Add seed stock
          </button>
        </div>

        {/* Seed Stock Table Card */}
        <div className="rounded-2xl bg-white border border-slate-200/90 overflow-hidden">
          {loading ? (
            <div className="p-8 text-center text-xs text-slate-400">
              Loading seed stock records...
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-slate-50/90 border-b border-slate-200/80 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
                    <th className="py-3.5 px-5">Seed / Variety</th>
                    <th className="py-3.5 px-4">Quantity</th>
                    <th className="py-3.5 px-4">Used by batches</th>
                    <th className="py-3.5 px-4">Status</th>
                    <th className="py-3.5 px-4">Last updated</th>
                    <th className="py-3.5 px-5 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 text-xs">
                  {filteredItems.map((item, idx) => {
                    const badgeColor = CROP_BADGE_COLORS[idx % CROP_BADGE_COLORS.length];

                    return (
                      <tr key={item.id} className="hover:bg-slate-50/60 transition">
                        <td className="py-4 px-5">
                          <div className="flex items-center gap-3">
                            <div
                              className={`w-8 h-8 rounded-lg border flex items-center justify-center text-xs font-bold shrink-0 ${badgeColor}`}
                            >
                              {item.cropName[0]?.toUpperCase()}
                            </div>
                            <div>
                              <p className="font-semibold text-slate-900">
                                {item.cropName} · {item.variety}
                              </p>
                              <p className="text-[11px] text-slate-400">
                                {item.supplier || 'No supplier recorded'}
                              </p>
                            </div>
                          </div>
                        </td>

                        <td className="py-4 px-4 font-bold text-slate-900">
                          {item.quantity} {item.unit}
                        </td>

                        <td className="py-4 px-4 text-slate-600">
                          {item.usedByBatchesCount}{' '}
                          {item.usedByBatchesCount === 1 ? 'batch' : 'batches'}
                        </td>

                        <td className="py-4 px-4">
                          {item.isLowStock ? (
                            <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-50 border border-amber-200/80 px-2.5 py-0.5 text-[11px] font-semibold text-amber-800">
                              <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
                              Low stock
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 border border-emerald-200/80 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-700">
                              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                              In stock
                            </span>
                          )}
                        </td>

                        <td className="py-4 px-4">
                          <p className="font-semibold text-slate-800">{item.lastUpdatedDate}</p>
                          <p className="text-[11px] text-slate-400">
                            by {item.lastUpdatedByName}
                            {item.lastUpdatedByIsAgent ? ' (agent)' : ''}
                          </p>
                        </td>

                        <td className="py-4 px-5 text-right">
                          <div className="inline-flex flex-col items-end gap-1">
                            <button
                              type="button"
                              onClick={() => {
                                setEditingItem(item);
                                setFormOpen(true);
                              }}
                              className="text-xs font-semibold text-agri-700 hover:underline cursor-pointer"
                            >
                              Edit
                            </button>
                            <button
                              type="button"
                              onClick={() => handleDelete(item)}
                              className="text-xs font-semibold text-red-600 hover:underline cursor-pointer"
                            >
                              Delete
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}

                  {filteredItems.length === 0 && (
                    <tr>
                      <td colSpan={6} className="py-8 text-center text-xs text-slate-400">
                        No seed stock records found for {firstName}.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* Modals */}
      {workspace && (
        <>
          <SwitchFarmerModal
            isOpen={switchOpen}
            farmers={assignedFarmers}
            currentFarmerId={workspace.farmerId}
            onClose={() => setSwitchOpen(false)}
            onSelectFarmer={(nextId) => navigate(`/agent/farmers/${nextId}/seed-stock`)}
          />

          <SeedStockFormModal
            isOpen={formOpen}
            farmerName={workspace.farmerName}
            crops={workspace.availableCrops}
            editingItem={editingItem}
            onClose={() => setFormOpen(false)}
            onSave={handleSaveSeedStock}
          />
        </>
      )}

      <NotAssignedModal
        isOpen={notAssignedState.open}
        farmerId={numericFarmerId}
        farmerName={notAssignedState.farmerName}
        onClose={() => navigate('/agent/dashboard')}
      />
    </AgentLayout>
  );
};
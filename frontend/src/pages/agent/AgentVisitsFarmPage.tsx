import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Map,
  Clock,
  CheckCircle2,
  AlertTriangle,
  Info,
  X,
} from 'lucide-react';
import { AgentLayout } from '../../components/agent/AgentLayout';
import { ConfirmFarmVisitModal } from '../../components/agent/ConfirmFarmVisitModal';
import { agentApi } from '../../api/agentApi';
import type { FarmVisitRow, FarmVisitWorkspaceResponse } from '../../types/agent';

type VisitFilterTab = 'Waiting' | 'Done' | 'All';

const AVATAR_STYLES = [
  'bg-amber-100 text-amber-800',
  'bg-purple-100 text-purple-800',
  'bg-emerald-100 text-emerald-800',
  'bg-sky-100 text-sky-800',
];

export const AgentFarmVisitsPage: React.FC = () => {
  const [data, setData] = useState<FarmVisitWorkspaceResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<VisitFilterTab>('Waiting');
  const [searchQuery, setSearchQuery] = useState('');
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const [selectedVisit, setSelectedVisit] = useState<FarmVisitRow | null>(null);
  const [modalMode, setModalMode] = useState<'confirm' | 'reject'>('confirm');

  const loadVisits = useCallback(async () => {
    setLoading(true);
    try {
      const res = await agentApi.getFarmVisitRequests();
      setData(res);
    } catch (err: any) {
      setToastMessage(err.response?.data?.message || 'Unable to load farm visit requests.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadVisits();
  }, [loadVisits]);

  const counts = useMemo(() => {
    const items = data?.items ?? [];
    return {
      Waiting: items.filter((i) => i.status === 'Requested').length,
      Done: items.filter((i) => i.status === 'Done').length,
      All: items.length,
    };
  }, [data]);

  const filteredItems = useMemo(() => {
    if (!data) return [];
    let list = data.items;
    if (activeTab === 'Waiting') list = list.filter((i) => i.status === 'Requested');
    if (activeTab === 'Done') list = list.filter((i) => i.status === 'Done');

    const q = searchQuery.trim().toLowerCase();
    if (!q) return list;

    return list.filter(
      (i) =>
        i.farmerName.toLowerCase().includes(q) ||
        i.farmName.toLowerCase().includes(q) ||
        i.districtSector.toLowerCase().includes(q) ||
        i.landUpi.toLowerCase().includes(q)
    );
  }, [data, activeTab, searchQuery]);

  const handleConfirmVisit = async (visitId: number, visitDate: string, findings: string) => {
    const res = await agentApi.confirmFarmVisit(visitId, { visitDate, findings });
    setToastMessage(res.message);
    await loadVisits();
  };

  const handleRejectVisit = async (visitId: number, reason: string) => {
    const res = await agentApi.rejectFarmVisit(visitId, { reason });
    setToastMessage(res.message);
    await loadVisits();
  };

  return (
    <AgentLayout
      breadcrumb="Farm visit requests"
      pendingVisitsCount={data?.kpis.waitingCount}
      searchQuery={searchQuery}
      onSearchChange={setSearchQuery}
      onComingSoon={(label) =>
        setToastMessage(`${label} will be connected in the next implementation step.`)
      }
    >
      <div className="space-y-5">
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

        {/* Header */}
        <div className="flex items-center gap-3.5">
          <div className="w-10 h-10 rounded-xl bg-emerald-50 border border-emerald-200/80 flex items-center justify-center text-agri-700 shrink-0">
          </div>
          <div>
            <h1 className="text-xl font-bold text-slate-900 tracking-tight">
              Farm visit requests
            </h1>
            <p className="text-xs text-slate-500 mt-0.5">
              Admins ask you to check self-registered farms in person. Confirming a visit verifies
              the farm.
            </p>
          </div>
        </div>

        {/* 3 KPI Summary Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          {/* Card 1: Waiting */}
          <div className="rounded-2xl bg-white border border-slate-200/90 p-5 flex items-center gap-3.5">
            <div className="w-9 h-9 rounded-lg bg-amber-50 border border-amber-200/70 text-amber-700 flex items-center justify-center shrink-0">
              <Clock className="w-4 h-4" />
            </div>
            <div>
              <p className="text-[11px] font-medium text-slate-500">Waiting</p>
              <p className="text-2xl font-bold text-slate-900 leading-tight mt-0.5">
                {data?.kpis.waitingCount ?? 0}
              </p>
              <p className="text-[11px] text-slate-400 mt-0.5">
                {data?.kpis.dueThisWeekCount ?? 0} due this week
              </p>
            </div>
          </div>

          {/* Card 2: Done this month */}
          <div className="rounded-2xl bg-white border border-slate-200/90 p-5 flex items-center gap-3.5">
            <div className="w-9 h-9 rounded-lg bg-emerald-50 border border-emerald-200/70 text-agri-700 flex items-center justify-center shrink-0">
              <CheckCircle2 className="w-4 h-4" />
            </div>
            <div>
              <p className="text-[11px] font-medium text-slate-500">Done this month</p>
              <p className="text-2xl font-bold text-slate-900 leading-tight mt-0.5">
                {data?.kpis.doneThisMonthCount ?? 0}
              </p>
              <p className="text-[11px] text-slate-400 mt-0.5">Farms verified</p>
            </div>
          </div>

          {/* Card 3: Couldn't verify */}
          <div className="rounded-2xl bg-white border border-slate-200/90 p-5 flex items-center gap-3.5">
            <div className="w-9 h-9 rounded-lg bg-red-50 border border-red-200/70 text-red-600 flex items-center justify-center shrink-0">
              <AlertTriangle className="w-4 h-4" />
            </div>
            <div>
              <p className="text-[11px] font-medium text-slate-500">Couldn&apos;t verify</p>
              <p className="text-2xl font-bold text-slate-900 leading-tight mt-0.5">
                {data?.kpis.couldNotVerifyCount ?? 0}
              </p>
              <p className="text-[11px] text-slate-400 mt-0.5">Sent back to admin</p>
            </div>
          </div>
        </div>

        {/* Filter Pills: Waiting / Done / All */}
        <div className="flex items-center gap-2">
          {(['Waiting', 'Done', 'All'] as VisitFilterTab[]).map((tab) => {
            const active = activeTab === tab;
            return (
              <button
                key={tab}
                type="button"
                onClick={() => setActiveTab(tab)}
                className={`inline-flex items-center gap-1.5 rounded-full px-4 py-1.5 text-xs font-semibold transition cursor-pointer ${
                  active
                    ? 'bg-agri-700 text-white shadow-2xs'
                    : 'bg-white border border-slate-200 text-slate-600 hover:bg-slate-50'
                }`}
              >
                <span>{tab}</span>
                <span className={active ? 'text-emerald-200' : 'text-slate-400'}>
                  {counts[tab]}
                </span>
              </button>
            );
          })}
        </div>

        {/* Farm Visit Requests Table */}
        <div className="rounded-2xl bg-white border border-slate-200/90 overflow-hidden">
          {loading ? (
            <div className="p-8 text-center text-xs text-slate-400">
              Loading farm visit requests...
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-slate-50/90 border-b border-slate-200/80 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
                    <th className="py-3.5 px-5">Farm</th>
                    <th className="py-3.5 px-4">Location</th>
                    <th className="py-3.5 px-4">Land UPI</th>
                    <th className="py-3.5 px-4">Visit by</th>
                    <th className="py-3.5 px-4">Status</th>
                    <th className="py-3.5 px-5 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 text-xs">
                  {filteredItems.map((item, idx) => {
                    const avatarStyle = AVATAR_STYLES[idx % AVATAR_STYLES.length];

                    return (
                      <tr key={item.id} className="hover:bg-slate-50/60 transition">
                        <td className="py-4 px-5">
                          <div className="flex items-center gap-3">
                            <div
                              className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold shrink-0 ${avatarStyle}`}
                            >
                              {item.farmerInitials}
                            </div>
                            <div>
                              <p className="font-semibold text-slate-900">{item.farmerName}</p>
                              <p className="text-[11px] text-slate-400">{item.farmName}</p>
                            </div>
                          </div>
                        </td>

                        <td className="py-4 px-4">
                          <p className="font-semibold text-slate-800">{item.districtSector}</p>
                          <p className="text-[11px] text-slate-400">{item.cellVillage}</p>
                        </td>

                        <td className="py-4 px-4 font-mono text-[11px] text-slate-700">
                          {item.landUpi}
                        </td>

                        <td className="py-4 px-4">
                          <p className="font-semibold text-slate-800">{item.visitByDate}</p>
                          <p className="text-[11px] text-slate-400">
                            Requested by {item.requestedByName}
                          </p>
                        </td>

                        <td className="py-4 px-4">
                          {item.status === 'Requested' && (
                            <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-50 border border-amber-200/80 px-2.5 py-0.5 text-[11px] font-semibold text-amber-800">
                              <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
                              Requested
                            </span>
                          )}
                          {item.status === 'Done' && (
                            <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 border border-emerald-200/80 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-700">
                              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                              Verified
                            </span>
                          )}
                          {item.status === 'Cancelled' && (
                            <span className="inline-flex items-center gap-1.5 rounded-full bg-red-50 border border-red-200/80 px-2.5 py-0.5 text-[11px] font-semibold text-red-700">
                              <span className="w-1.5 h-1.5 rounded-full bg-red-500" />
                              Couldn&apos;t verify
                            </span>
                          )}
                        </td>

                        <td className="py-4 px-5 text-right">
                          {item.status === 'Requested' ? (
                            <div className="inline-flex flex-col items-end gap-1">
                              <button
                                type="button"
                                onClick={() => {
                                  setSelectedVisit(item);
                                  setModalMode('confirm');
                                }}
                                className="text-xs font-semibold text-agri-700 hover:underline cursor-pointer"
                              >
                                Confirm visit
                              </button>
                              <button
                                type="button"
                                onClick={() => {
                                  setSelectedVisit(item);
                                  setModalMode('reject');
                                }}
                                className="text-xs font-semibold text-red-600 hover:underline cursor-pointer"
                              >
                                Can&apos;t verify
                              </button>
                            </div>
                          ) : (
                            <span className="text-[11px] text-slate-400">
                              {item.completedDate ?? 'Completed'}
                            </span>
                          )}
                        </td>
                      </tr>
                    );
                  })}

                  {filteredItems.length === 0 && (
                    <tr>
                      <td colSpan={6} className="py-8 text-center text-xs text-slate-400">
                        No farm visit requests in this view.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Bottom Info Banner: What to check on the visit */}
        <div className="rounded-xl bg-blue-50/70 border border-blue-200/80 px-4 py-3.5 flex items-start gap-3 text-xs">
          <Info className="w-4 h-4 text-blue-600 shrink-0 mt-0.5" />
          <div>
            <p className="font-semibold text-blue-950">What to check on the visit</p>
            <p className="text-blue-800/85 text-[11px] mt-0.5">
              The land UPI matches the parcel, the farmer is who they say they are, and the crops
              listed are really grown there.
            </p>
          </div>
        </div>
      </div>

      <ConfirmFarmVisitModal
        visit={selectedVisit}
        initialMode={modalMode}
        onClose={() => setSelectedVisit(null)}
        onConfirm={handleConfirmVisit}
        onReject={handleRejectVisit}
      />
    </AgentLayout>
  );
};
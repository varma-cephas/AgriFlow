import React, { useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  Calendar,
  Users,
  Sprout,
  CalendarClock,
  Truck,
  Package,
  ArrowRight,
  UserPlus,
  Info,
  X,
} from 'lucide-react';
import { AgentLayout } from '../../components/agent/AgentLayout';
import { agentApi } from '../../api/agentApi';
import type {
  AgentOverviewResponse,
  AttentionCategory,
  FarmerGlanceStatus,
} from '../../types/agent';

const AVATAR_STYLES = [
  'bg-emerald-100 text-emerald-800',
  'bg-amber-100 text-amber-800',
  'bg-sky-100 text-sky-800',
  'bg-purple-100 text-purple-800',
  'bg-teal-100 text-teal-800',
];

function getGreeting(): string {
  const hour = new Date().getHours();
  if (hour < 12) return 'Good morning';
  if (hour < 18) return 'Good afternoon';
  return 'Good evening';
}

function getGlanceBadgeClasses(status: FarmerGlanceStatus): {
  pill: string;
  dot: string;
  bar: string;
} {
  switch (status) {
    case 'Harvest due':
      return {
        pill: 'bg-amber-50 text-amber-800 border-amber-200/80',
        dot: 'bg-amber-500',
        bar: 'bg-amber-500',
      };
    case 'Low seed':
      return {
        pill: 'bg-red-50 text-red-700 border-red-200/80',
        dot: 'bg-red-500',
        bar: 'bg-red-500',
      };
    case 'Order to confirm':
      return {
        pill: 'bg-blue-50 text-blue-700 border-blue-200/80',
        dot: 'bg-blue-500',
        bar: 'bg-blue-600',
      };
    case 'Visit planned':
      return {
        pill: 'bg-purple-50 text-purple-700 border-purple-200/80',
        dot: 'bg-purple-500',
        bar: 'bg-purple-600',
      };
    case 'On track':
    default:
      return {
        pill: 'bg-emerald-50 text-emerald-800 border-emerald-200/80',
        dot: 'bg-emerald-600',
        bar: 'bg-agri-700',
      };
  }
}

function getAttentionIconStyle(category: AttentionCategory) {
  switch (category) {
    case 'HarvestDue':
      return {
        box: 'bg-amber-50 border-amber-200/70 text-amber-700',
        Icon: CalendarClock,
      };
    case 'LowSeed':
      return {
        box: 'bg-red-50 border-red-200/70 text-red-600',
        Icon: Package,
      };
    case 'OrderToConfirm':
    default:
      return {
        box: 'bg-blue-50 border-blue-200/70 text-blue-600',
        Icon: Truck,
      };
  }
}

export const AgentDashboard: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();

  const [data, setData] = useState<AgentOverviewResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    agentApi
      .getOverview()
      .then((res) => {
        if (active) setData(res);
      })
      .catch((err) => {
        if (active) {
          setError(err.response?.data?.message || 'Failed to load agent overview.');
        }
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (loading || !data || !location.hash) return;
    const targetId = location.hash.replace('#', '');
    document.getElementById(targetId)?.scrollIntoView({ behavior: 'smooth' });
  }, [loading, data, location.hash]);

  const filteredGlance = useMemo(() => {
    if (!data) return [];
    const q = searchQuery.trim().toLowerCase();
    if (!q) return data.farmersAtAGlance;
    return data.farmersAtAGlance.filter(
      (f) =>
        f.fullName.toLowerCase().includes(q) ||
        f.farmName.toLowerCase().includes(q) ||
        f.district.toLowerCase().includes(q)
    );
  }, [data, searchQuery]);

  const filteredFarmers = useMemo(() => {
    if (!data) return [];
    const q = searchQuery.trim().toLowerCase();
    if (!q) return data.myFarmers;
    return data.myFarmers.filter(
      (f) =>
        f.fullName.toLowerCase().includes(q) ||
        f.farmName.toLowerCase().includes(q) ||
        f.district.toLowerCase().includes(q) ||
        f.phone.toLowerCase().includes(q)
    );
  }, [data, searchQuery]);

  const handleOpenFarmerWorkspace = (farmerId: number) => {
    navigate(`/agent/farmers/${farmerId}/seed-stock`);
  };

  if (loading) {
    return (
      <AgentLayout breadcrumb="Overview">
        <div className="space-y-6 animate-pulse">
          <div className="h-44 rounded-2xl bg-slate-200/80" />
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            {[1, 2, 3, 4].map((n) => (
              <div key={n} className="h-32 rounded-2xl bg-slate-200/70" />
            ))}
          </div>
          <div className="h-72 rounded-2xl bg-slate-200/70" />
        </div>
      </AgentLayout>
    );
  }

  if (error || !data) {
    return (
      <AgentLayout breadcrumb="Overview">
        <div className="rounded-xl bg-red-50 border border-red-200 p-4 text-xs text-red-700">
          {error ?? 'Unable to load workspace data.'}
        </div>
      </AgentLayout>
    );
  }

  const firstName = data.agent.fullName.split(' ')[0];

  return (
    <AgentLayout
      breadcrumb="Overview"
      roleTitle={data.agent.roleTitle}
      hasNotifications={data.attentionItems.length > 0}
      searchQuery={searchQuery}
      onSearchChange={setSearchQuery}
      onComingSoon={(label) =>
        setToastMessage(`${label} will be connected in the next implementation step.`)
      }
    >
      <div className="space-y-6">
        {/* Contextual Preview Toast */}
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

        {/* 1. Emerald Hero Banner */}
        <section className="relative rounded-2xl bg-gradient-to-r from-[#17613b] via-[#135232] to-[#0e3f26] text-white p-6 lg:p-8 overflow-hidden shadow-sm">
          <div className="pointer-events-none absolute -right-12 -top-16 w-64 h-64 rounded-full bg-white/[0.04]" />

          <div className="relative z-10 flex flex-col lg:flex-row lg:items-center justify-between gap-6">
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-widest text-emerald-200/80">
                Field agent · {data.agent.districtLabel} district
              </p>
              <h1 className="text-2xl lg:text-3xl font-bold tracking-tight mt-1">
                {getGreeting()}, {firstName}
              </h1>
              <p className="text-xs sm:text-sm text-emerald-100/85 mt-1.5">
                You support {data.kpis.assignedFarmersTotal}{' '}
                {data.kpis.assignedFarmersTotal === 1 ? 'farmer' : 'farmers'} ·{' '}
                {data.kpis.visitsThisWeekCount} need a visit this week
              </p>

              <div className="mt-5 flex flex-wrap items-center gap-3">
                <button
                  type="button"
                  disabled={data.myFarmers.length === 0}
                  onClick={() => {
                    const first = data.myFarmers[0];
                    if (first) handleOpenFarmerWorkspace(first.farmerId);
                  }}
                  className="inline-flex items-center gap-2 rounded-lg bg-white hover:bg-emerald-50 disabled:opacity-60 text-agri-900 font-semibold text-xs px-4 py-2.5 transition cursor-pointer"
                >
                  <Users className="w-3.5 h-3.5 text-agri-700" />
                  Open a farmer&apos;s workspace
                </button>

                <button
                  type="button"
                  onClick={() => navigate('/agent/register-farmer')}
                  className="inline-flex items-center gap-2 rounded-lg bg-white/12 hover:bg-white/18 border border-white/20 text-white font-semibold text-xs px-4 py-2.5 transition cursor-pointer"
                >
                  <UserPlus className="w-3.5 h-3.5" />
                  Register a farmer
                </button>
              </div>
            </div>

            {/* Right Translucent Next Visit Card */}
            {data.nextVisit && (
              <div className="lg:max-w-sm w-full rounded-xl bg-white/10 border border-white/15 backdrop-blur-xs p-4 flex items-start gap-3.5">
                <div className="w-9 h-9 rounded-lg bg-emerald-400/20 border border-emerald-300/25 flex items-center justify-center shrink-0 mt-0.5">
                  <Calendar className="w-4 h-4 text-emerald-200" />
                </div>
                <div className="text-xs leading-relaxed">
                  <p className="font-semibold text-white">
                    Next visit: {data.nextVisit.farmerName}, {data.nextVisit.relativeLabel}
                  </p>
                  {data.nextVisit.note && (
                    <p className="text-emerald-100/80 mt-1">{data.nextVisit.note}</p>
                  )}
                </div>
              </div>
            )}
          </div>
        </section>

        {/* 2. Four KPI Summary Cards */}
        <section className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {/* Card 1: Assigned farmers */}
          <div className="rounded-2xl bg-white border border-slate-200/90 p-5">
            <div className="flex items-center justify-between">
              <div className="w-9 h-9 rounded-lg bg-emerald-50 text-agri-700 flex items-center justify-center">
                <Users className="w-4 h-4" />
              </div>
              <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-700">
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />+
                {data.kpis.newFarmersThisMonth} this month
              </span>
            </div>
            <div className="mt-4 text-2xl font-bold text-slate-900">
              {data.kpis.assignedFarmersTotal}
            </div>
            <div className="text-xs font-semibold text-slate-800 mt-0.5">Assigned farmers</div>
            <div className="text-[11px] text-slate-400 mt-0.5">
              {data.kpis.nonDigitalFarmersCount} non-digital
            </div>
          </div>

          {/* Card 2: Active batches */}
          <div className="rounded-2xl bg-white border border-slate-200/90 p-5">
            <div className="flex items-center justify-between">
              <div className="w-9 h-9 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center">
                <Sprout className="w-4 h-4" />
              </div>
              <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-700">
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                Active
              </span>
            </div>
            <div className="mt-4 text-2xl font-bold text-slate-900">
              {data.kpis.activeBatchesTotal}
            </div>
            <div className="text-xs font-semibold text-slate-800 mt-0.5">Active batches</div>
            <div className="text-[11px] text-slate-400 mt-0.5">Across your farmers</div>
          </div>

          {/* Card 3: Harvests due */}
          <div className="rounded-2xl bg-white border border-slate-200/90 p-5">
            <div className="flex items-center justify-between">
              <div className="w-9 h-9 rounded-lg bg-amber-50 text-amber-700 flex items-center justify-center">
                <CalendarClock className="w-4 h-4" />
              </div>
              <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 px-2.5 py-0.5 text-[11px] font-semibold text-amber-800">
                <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
                This fortnight
              </span>
            </div>
            <div className="mt-4 text-2xl font-bold text-slate-900">
              {data.kpis.harvestsDueWithin14Days}
            </div>
            <div className="text-xs font-semibold text-slate-800 mt-0.5">Harvests due</div>
            <div className="text-[11px] text-slate-400 mt-0.5">Within 14 days</div>
          </div>

          {/* Card 4: Orders to handle */}
          <div className="rounded-2xl bg-white border border-slate-200/90 p-5">
            <div className="flex items-center justify-between">
              <div className="w-9 h-9 rounded-lg bg-purple-50 text-purple-600 flex items-center justify-center">
                <Truck className="w-4 h-4" />
              </div>
              <span className="inline-flex items-center gap-1 rounded-full bg-purple-50 px-2.5 py-0.5 text-[11px] font-semibold text-purple-700">
                <span className="w-1.5 h-1.5 rounded-full bg-purple-500" />
                Action
              </span>
            </div>
            <div className="mt-4 text-2xl font-bold text-slate-900">
              {data.kpis.ordersToHandleTotal}
            </div>
            <div className="text-xs font-semibold text-slate-800 mt-0.5">Orders to handle</div>
            <div className="text-[11px] text-slate-400 mt-0.5">Confirm or dispatch</div>
          </div>
        </section>

        {/* 3. Middle Grid: Your Farmers at a Glance + Needs Your Attention */}
        <section className="grid grid-cols-1 lg:grid-cols-12 gap-5">
          {/* Left: Your farmers at a glance (7 cols) */}
          <div className="lg:col-span-7 rounded-2xl bg-white border border-slate-200/90 p-6">
            <h2 className="text-sm font-bold text-slate-900">Your farmers at a glance</h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Season progress and what each farmer needs next
            </p>

            <div className="mt-4 divide-y divide-slate-100">
              {filteredGlance.map((farmer, idx) => {
                const badgeStyle = getGlanceBadgeClasses(farmer.statusBadge);
                const avatarStyle = AVATAR_STYLES[idx % AVATAR_STYLES.length];

                return (
                  <div
                    key={farmer.farmerId}
                    onClick={() => handleOpenFarmerWorkspace(farmer.farmerId)}
                    className="py-3.5 first:pt-2 last:pb-1 flex flex-col sm:flex-row sm:items-center justify-between gap-3 cursor-pointer hover:bg-slate-50/70 rounded-lg px-2 -mx-2 transition"
                  >
                    <div className="flex items-center gap-3 min-w-0 sm:w-52">
                      <div
                        className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold shrink-0 ${avatarStyle}`}
                      >
                        {farmer.initials}
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-semibold text-slate-900 truncate">
                          {farmer.fullName}
                        </p>
                        <p className="text-[11px] text-slate-400 truncate">
                          {farmer.farmName} · {farmer.district}
                        </p>
                      </div>
                    </div>

                    {/* Season Progress Bar */}
                    <div className="flex-1 max-w-[200px]">
                      <div className="flex items-center justify-between text-[11px] text-slate-500 mb-1">
                        <span>{farmer.seasonProgressPercent}% of season tasks done</span>
                      </div>
                      <div className="h-1.5 w-full rounded-full bg-slate-100 overflow-hidden">
                        <div
                          className={`h-full rounded-full transition-all ${badgeStyle.bar}`}
                          style={{ width: `${farmer.seasonProgressPercent}%` }}
                        />
                      </div>
                    </div>

                    {/* Status Pill */}
                    <span
                      className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-[11px] font-medium shrink-0 self-start sm:self-center ${badgeStyle.pill}`}
                    >
                      <span className={`w-1.5 h-1.5 rounded-full ${badgeStyle.dot}`} />
                      {farmer.statusBadge}
                    </span>
                  </div>
                );
              })}

              {filteredGlance.length === 0 && (
                <p className="py-6 text-center text-xs text-slate-400">
                  No assigned farmers match &ldquo;{searchQuery}&rdquo;.
                </p>
              )}
            </div>
          </div>

          {/* Right: Needs your attention (5 cols) */}
          <div
            id="attention-section"
            className="lg:col-span-5 rounded-2xl bg-white border border-slate-200/90 p-6"
          >
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-bold text-slate-900">Needs your attention</h2>
              <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-50 border border-amber-200/80 px-2.5 py-0.5 text-[11px] font-semibold text-amber-800">
                <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
                {data.attentionItems.length} items
              </span>
            </div>

            <div className="mt-4 space-y-3">
              {data.attentionItems.map((item) => {
                const { box, Icon } = getAttentionIconStyle(item.category);

                return (
                  <div
                    key={item.id}
                    className="rounded-xl border border-slate-100 bg-slate-50/60 p-3.5 flex items-center justify-between gap-3"
                  >
                    <div className="flex items-start gap-3 min-w-0">
                      <div
                        className={`w-8 h-8 rounded-lg border flex items-center justify-center shrink-0 mt-0.5 ${box}`}
                      >
                        <Icon className="w-4 h-4" />
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-semibold text-slate-900">{item.title}</p>
                        <p className="text-[11px] text-slate-500 mt-0.5 leading-snug">
                          {item.subtitle}
                        </p>
                      </div>
                    </div>

                    <button
                      type="button"
                      onClick={() => handleOpenFarmerWorkspace(item.farmerId)}
                      className="inline-flex items-center gap-1 text-xs font-semibold text-agri-700 hover:text-agri-800 shrink-0 cursor-pointer"
                    >
                      <span>{item.actionLabel}</span>
                      <ArrowRight className="w-3.5 h-3.5" />
                    </button>
                  </div>
                );
              })}

              {data.attentionItems.length === 0 && (
                <div className="rounded-xl bg-emerald-50/60 border border-emerald-200/70 p-4 text-xs text-emerald-800">
                  All caught up — no pending harvests, low seed stock, or unconfirmed orders.
                </div>
              )}
            </div>
          </div>
        </section>

        {/* 4. Bottom Full-Width Table: My farmers */}
        <section
          id="my-farmers-section"
          className="rounded-2xl bg-white border border-slate-200/90 p-6"
        >
          <div className="mb-4">
            <h2 className="text-sm font-bold text-slate-900">My farmers</h2>
            <p className="text-xs text-slate-500 mt-0.5">
              You can only act for farmers assigned to you.
            </p>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-slate-50/90 border-y border-slate-200/80 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
                  <th className="py-3 px-4">Farmer</th>
                  <th className="py-3 px-4">Farm · Area</th>
                  <th className="py-3 px-4">Phone</th>
                  <th className="py-3 px-4">Active batches</th>
                  <th className="py-3 px-4">Seed stock</th>
                  <th className="py-3 px-4">Orders</th>
                  <th className="py-3 px-4 text-right">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 text-xs">
                {filteredFarmers.map((farmer, idx) => {
                  const avatarStyle = AVATAR_STYLES[idx % AVATAR_STYLES.length];

                  return (
                    <tr key={farmer.farmerId} className="hover:bg-slate-50/60 transition">
                      <td className="py-3.5 px-4">
                        <div className="flex items-center gap-3">
                          <div
                            className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold shrink-0 ${avatarStyle}`}
                          >
                            {farmer.initials}
                          </div>
                          <div>
                            <p className="font-semibold text-slate-900">{farmer.fullName}</p>
                            <p className="text-[11px] text-slate-400">{farmer.accountBadge}</p>
                          </div>
                        </div>
                      </td>

                      <td className="py-3.5 px-4 text-slate-700">
                        {farmer.farmName} · {farmer.district}
                      </td>

                      <td className="py-3.5 px-4 text-slate-600">{farmer.phone}</td>

                      <td className="py-3.5 px-4 font-semibold text-slate-800">
                        {farmer.activeBatchesCount}
                      </td>

                      <td className="py-3.5 px-4">
                        {farmer.lowSeedCount > 0 ? (
                          <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-50 border border-amber-200/80 px-2.5 py-0.5 text-[11px] font-semibold text-amber-800">
                            <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
                            {farmer.lowSeedCount} low
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 border border-emerald-200/80 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-700">
                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                            OK
                          </span>
                        )}
                      </td>

                      <td className="py-3.5 px-4 text-slate-700">
                        {farmer.ordersToConfirmCount > 0
                          ? `${farmer.ordersToConfirmCount} to confirm`
                          : '—'}
                      </td>

                      <td className="py-3.5 px-4 text-right">
                        <button
                          type="button"
                          onClick={() => handleOpenFarmerWorkspace(farmer.farmerId)}
                          className="inline-flex items-center gap-1 font-semibold text-agri-700 hover:text-agri-800 cursor-pointer"
                        >
                          <span>Act as farmer</span>
                          <ArrowRight className="w-3.5 h-3.5" />
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </AgentLayout>
  );
};
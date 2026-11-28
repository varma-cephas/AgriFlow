import React, { useCallback, useEffect, useState } from 'react';
import { FileText, Download, Search, Lock } from 'lucide-react';
import { AgentLayout } from '../../components/agent/AgentLayout';
import { agentApi } from '../../api/agentApi';
import type { AgentAuditLogItem } from '../../types/agent';

const AVATAR_STYLES = [
  'bg-emerald-100 text-emerald-800',
  'bg-amber-100 text-amber-800',
  'bg-sky-100 text-sky-800',
  'bg-purple-100 text-purple-800',
];

export const AgentAuditLogPage: React.FC = () => {
  const [logs, setLogs] = useState<AgentAuditLogItem[]>([]);
  const [loading, setLoading] = useState(true);

  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState('All');
  const [actionFilter, setActionFilter] = useState('All');
  const [dateRange, setDateRange] = useState('all');

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    try {
      const data = await agentApi.getAuditLogs({
        search: search.trim() || undefined,
        role: roleFilter,
        action: actionFilter,
        dateRange,
      });
      setLogs(data);
    } catch {
      setLogs([]);
    } finally {
      setLoading(false);
    }
  }, [search, roleFilter, actionFilter, dateRange]);

  useEffect(() => {
    const timer = setTimeout(() => {
      fetchLogs();
    }, 200);
    return () => clearTimeout(timer);
  }, [fetchLogs]);

  const handleExportCsv = () => {
    if (logs.length === 0) return;

    const escapeCsv = (val: string) => `"${val.replace(/"/g, '""')}"`;
    const headers = ['Timestamp (UTC)', 'Done By', 'Role', 'On Behalf Of', 'Action', 'Details', 'Result'];
    const rows = logs.map((item) => [
      escapeCsv(item.isoTimestamp),
      escapeCsv(item.actorName),
      escapeCsv(item.actorRole),
      escapeCsv(item.onBehalfOfFarmerName ?? '-'),
      escapeCsv(item.actionLabel),
      escapeCsv(item.summary),
      escapeCsv(item.result),
    ]);

    const csvContent = [headers.join(','), ...rows.map((r) => r.join(','))].join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);

    const link = document.createElement('a');
    link.href = url;
    link.download = `agriflow-agent-audit-log-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  };

  return (
    <AgentLayout breadcrumb="Audit log" searchQuery={search} onSearchChange={setSearch}>
      <div className="space-y-5">
        {/* Header + Export CSV */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex items-center gap-3.5">
            <div className="w-10 h-10 rounded-xl bg-emerald-50 border border-emerald-200/80 flex items-center justify-center text-agri-700 shrink-0">
              <FileText className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-xl font-bold text-slate-900 tracking-tight">Audit log</h1>
              <p className="text-xs text-slate-500 mt-0.5">
                Every important action — who did it, when, and on whose behalf. Read-only.
              </p>
            </div>
          </div>

          <button
            type="button"
            disabled={logs.length === 0}
            onClick={handleExportCsv}
            className="inline-flex items-center gap-2 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 disabled:opacity-50 text-slate-800 font-semibold text-xs px-3.5 py-2 shadow-2xs transition cursor-pointer self-start sm:self-auto"
          >
            <Download className="w-3.5 h-3.5 text-slate-600" />
            <span>Export CSV</span>
          </button>
        </div>

        {/* Search & Filter Controls */}
        <div className="flex flex-col md:flex-row items-stretch md:items-center gap-3">
          <div className="relative flex-1">
            <Search className="w-3.5 h-3.5 text-slate-400 absolute left-3.5 top-2.5" />
            <input
              type="text"
              placeholder="Search user, record or action..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full rounded-lg border border-slate-200 bg-white pl-9 pr-3.5 py-2 text-xs text-slate-800 placeholder:text-slate-400 focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
            />
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <select
              value={roleFilter}
              onChange={(e) => setRoleFilter(e.target.value)}
              className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 focus:border-agri-700 focus:outline-none"
            >
              <option value="All">User role: All</option>
              <option value="Agent">User role: Agent</option>
              <option value="Farmer">User role: Farmer</option>
            </select>

            <select
              value={actionFilter}
              onChange={(e) => setActionFilter(e.target.value)}
              className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 focus:border-agri-700 focus:outline-none"
            >
              <option value="All">Action: All</option>
              <option value="SeedStock">Action: Seed stock</option>
              <option value="FarmVisit">Action: Farm visits</option>
              <option value="FarmerRegistration">Action: Registration</option>
            </select>

            <select
              value={dateRange}
              onChange={(e) => setDateRange(e.target.value)}
              className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 focus:border-agri-700 focus:outline-none"
            >
              <option value="all">Date: All time</option>
              <option value="today">Date: Today</option>
              <option value="7d">Date: Last 7 days</option>
              <option value="30d">Date: Last 30 days</option>
            </select>
          </div>
        </div>

        {/* Audit Log Table */}
        <div className="rounded-2xl bg-white border border-slate-200/90 overflow-hidden">
          {loading ? (
            <div className="p-8 text-center text-xs text-slate-400">Loading audit trail...</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-slate-50/90 border-b border-slate-200/80 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
                    <th className="py-3.5 px-5">Time</th>
                    <th className="py-3.5 px-4">Done by</th>
                    <th className="py-3.5 px-4">On behalf of</th>
                    <th className="py-3.5 px-4">Action</th>
                    <th className="py-3.5 px-5 text-right">Result</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 text-xs">
                  {logs.map((item, idx) => {
                    const avatarStyle = AVATAR_STYLES[idx % AVATAR_STYLES.length];
                    const isSuccess = item.result === 'Success';

                    return (
                      <tr key={item.id} className="hover:bg-slate-50/60 transition">
                        <td className="py-3.5 px-5 font-medium text-slate-600 whitespace-nowrap">
                          {item.timeDisplay}
                        </td>

                        <td className="py-3.5 px-4">
                          <div className="flex items-center gap-2.5">
                            <div
                              className={`w-7 h-7 rounded-full flex items-center justify-center text-[11px] font-bold shrink-0 ${avatarStyle}`}
                            >
                              {item.actorInitials}
                            </div>
                            <div>
                              <p className="font-semibold text-slate-900">{item.actorName}</p>
                              <p className="text-[11px] text-slate-400">{item.actorRole}</p>
                            </div>
                          </div>
                        </td>

                        <td className="py-3.5 px-4 text-slate-700 font-medium">
                          {item.onBehalfOfFarmerName || '—'}
                        </td>

                        <td className="py-3.5 px-4">
                          <p className="font-semibold text-slate-900">{item.actionLabel}</p>
                          <p className="text-[11px] text-slate-400 mt-0.5">{item.summary}</p>
                        </td>

                        <td className="py-3.5 px-5 text-right whitespace-nowrap">
                          {isSuccess ? (
                            <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 border border-emerald-200/80 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-700">
                              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                              {item.result}
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1.5 rounded-full bg-red-50 border border-red-200/80 px-2.5 py-0.5 text-[11px] font-semibold text-red-700">
                              <span className="w-1.5 h-1.5 rounded-full bg-red-500" />
                              {item.result}
                            </span>
                          )}
                        </td>
                      </tr>
                    );
                  })}

                  {logs.length === 0 && (
                    <tr>
                      <td colSpan={5} className="py-8 text-center text-xs text-slate-400">
                        No audit log entries match the selected filters.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Bottom Immutability Info Banner */}
        <div className="rounded-xl bg-blue-50/75 border border-blue-200/80 px-4 py-3.5 flex items-start gap-3 text-xs">
          <Lock className="w-4 h-4 text-blue-600 shrink-0 mt-0.5" />
          <div>
            <p className="font-semibold text-blue-950">
              The audit log can&apos;t be edited or deleted
            </p>
            <p className="text-blue-800/85 text-[11px] mt-0.5">
              Agent actions always record both the agent and the farmer they acted for.
            </p>
          </div>
        </div>
      </div>
    </AgentLayout>
  );
};
import React, { useMemo, useState } from 'react';
import { RefreshCw, Search, X } from 'lucide-react';
import type { AssignedFarmerRow } from '../../types/agent';

interface SwitchFarmerModalProps {
  isOpen: boolean;
  farmers: AssignedFarmerRow[];
  currentFarmerId: number;
  onClose: () => void;
  onSelectFarmer: (farmerId: number) => void;
}

export const SwitchFarmerModal: React.FC<SwitchFarmerModalProps> = ({
  isOpen,
  farmers,
  currentFarmerId,
  onClose,
  onSelectFarmer,
}) => {
  const [search, setSearch] = useState('');
  const [selectedId, setSelectedId] = useState<number>(currentFarmerId);

  React.useEffect(() => {
    setSelectedId(currentFarmerId);
    setSearch('');
  }, [currentFarmerId, isOpen]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return farmers;
    return farmers.filter(
      (f) =>
        f.fullName.toLowerCase().includes(q) ||
        f.farmName.toLowerCase().includes(q)
    );
  }, [farmers, search]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/45 backdrop-blur-[1px] p-4">
      <div className="relative w-full max-w-[440px] rounded-2xl bg-white p-6 shadow-xl border border-slate-100">
        <button
          type="button"
          onClick={onClose}
          aria-label="Close modal"
          className="absolute right-4 top-4 text-slate-400 hover:text-slate-600 cursor-pointer"
        >
          <X className="w-4 h-4" />
        </button>

        {/* Header */}
        <div className="flex items-start gap-3">
          <div className="w-9 h-9 rounded-full bg-emerald-50 border border-emerald-200 flex items-center justify-center text-agri-700 shrink-0">
            <RefreshCw className="w-4 h-4" />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-900">Switch farmer</h3>
            <p className="text-xs text-slate-500 mt-0.5">
              You can act for the {farmers.length} farmers assigned to you.
            </p>
          </div>
        </div>

        {/* Search Input */}
        <div className="relative mt-4">
          <Search className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
          <input
            type="text"
            placeholder="Search your farmers..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full rounded-lg border border-slate-200 pl-9 pr-3 py-2 text-xs text-slate-800 placeholder:text-slate-400 focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
          />
        </div>

        {/* Assigned Farmer Cards */}
        <div className="mt-3.5 max-h-72 overflow-y-auto space-y-2 pr-0.5">
          {filtered.map((farmer) => {
            const isSelected = farmer.farmerId === selectedId;
            const isCurrent = farmer.farmerId === currentFarmerId;

            return (
              <button
                key={farmer.farmerId}
                type="button"
                onClick={() => setSelectedId(farmer.farmerId)}
                className={`w-full rounded-xl border p-3 flex items-center justify-between text-left transition cursor-pointer ${
                  isSelected
                    ? 'border-agri-700 bg-emerald-50/60 ring-1 ring-agri-700'
                    : 'border-slate-200 bg-white hover:border-slate-300'
                }`}
              >
                <div className="flex items-center gap-3 min-w-0">
                  <div
                    className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold shrink-0 ${
                      isSelected
                        ? 'bg-agri-700 text-white'
                        : 'bg-slate-100 text-slate-700'
                    }`}
                  >
                    {farmer.initials}
                  </div>
                  <div className="min-w-0">
                    <p className="text-xs font-semibold text-slate-900 truncate">
                      {farmer.fullName}
                    </p>
                    <p className="text-[11px] text-slate-500 truncate">{farmer.farmName}</p>
                  </div>
                </div>

                {isCurrent && (
                  <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-agri-700 shrink-0">
                    <span className="w-1.5 h-1.5 rounded-full bg-agri-700" />
                    Current
                  </span>
                )}
              </button>
            );
          })}
        </div>

        {/* Footer Buttons */}
        <div className="mt-5 flex items-center justify-end gap-2.5">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 font-medium text-xs px-4 py-2 transition cursor-pointer"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={() => {
              onSelectFarmer(selectedId);
              onClose();
            }}
            className="rounded-lg bg-agri-700 hover:bg-agri-800 text-white font-semibold text-xs px-4 py-2 transition cursor-pointer"
          >
            Switch
          </button>
        </div>
      </div>
    </div>
  );
};
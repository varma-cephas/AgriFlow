import React from 'react';
import { CheckCircle2, X } from 'lucide-react';
import type { RegisterNonDigitalFarmerResponse } from '../../types/agent';

interface FarmerRegisteredModalProps {
  result: RegisterNonDigitalFarmerResponse | null;
  onRegisterAnother: () => void;
  onOpenWorkspace: (farmerId: number) => void;
}

export const FarmerRegisteredModal: React.FC<FarmerRegisteredModalProps> = ({
  result,
  onRegisterAnother,
  onOpenWorkspace,
}) => {
  if (!result) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/45 backdrop-blur-[1px] p-4">
      <div className="relative w-full max-w-[440px] rounded-2xl bg-white p-6 shadow-xl border border-slate-100">
        <button
          type="button"
          onClick={onRegisterAnother}
          aria-label="Close modal"
          className="absolute right-4 top-4 text-slate-400 hover:text-slate-600 cursor-pointer"
        >
          <X className="w-4 h-4" />
        </button>

        <div className="flex items-start gap-3.5">
          <div className="w-9 h-9 rounded-full bg-emerald-50 border border-emerald-200 flex items-center justify-center text-agri-700 shrink-0 mt-0.5">
            <CheckCircle2 className="w-4 h-4" />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-900">
              {result.fullName} is registered
            </h3>
            <p className="text-xs text-slate-500 mt-1 leading-relaxed">
              {result.farmName} is now assigned to you. Open their workspace to add their seed
              stock.
            </p>
          </div>
        </div>

        <div className="mt-6 flex items-center justify-end gap-2.5">
          <button
            type="button"
            onClick={onRegisterAnother}
            className="rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 font-semibold text-xs px-4 py-2.5 transition cursor-pointer"
          >
            Register another
          </button>

          <button
            type="button"
            onClick={() => onOpenWorkspace(result.farmerId)}
            className="rounded-lg bg-agri-700 hover:bg-agri-800 text-white font-semibold text-xs px-4 py-2.5 transition cursor-pointer"
          >
            Open {result.firstName}&apos;s workspace
          </button>
        </div>
      </div>
    </div>
  );
};
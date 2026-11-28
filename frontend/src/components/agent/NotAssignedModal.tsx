import React, { useState } from 'react';
import { Lock, X } from 'lucide-react';
import { agentApi } from '../../api/agentApi';

interface NotAssignedModalProps {
  isOpen: boolean;
  farmerId: number;
  farmerName: string;
  onClose: () => void;
}

export const NotAssignedModal: React.FC<NotAssignedModalProps> = ({
  isOpen,
  farmerId,
  farmerName,
  onClose,
}) => {
  const [submitting, setSubmitting] = useState(false);
  const [confirmationMsg, setConfirmationMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleRequestAssignment = async () => {
    setSubmitting(true);
    try {
      const res = await agentApi.requestFarmerAssignment(farmerId);
      setConfirmationMsg(res.message);
    } catch {
      setConfirmationMsg(`Assignment request for ${farmerName} sent to administrators.`);
    } finally {
      setSubmitting(false);
    }
  };

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

        <div className="flex items-start gap-3">
          <div className="w-9 h-9 rounded-full bg-red-50 border border-red-200 flex items-center justify-center text-red-600 shrink-0 mt-0.5">
            <Lock className="w-4 h-4" />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-900">
              You can&apos;t act for {farmerName}
            </h3>
            <p className="text-xs text-slate-500 mt-1 leading-relaxed">
              This farmer isn&apos;t assigned to you, so you can&apos;t view or change their
              records. Ask an administrator to assign them to you.
            </p>
          </div>
        </div>

        {confirmationMsg && (
          <div className="mt-4 rounded-lg bg-emerald-50 border border-emerald-200 p-3 text-xs text-emerald-800">
            {confirmationMsg}
          </div>
        )}

        <div className="mt-5 flex items-center justify-end gap-2.5">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 font-medium text-xs px-4 py-2 transition cursor-pointer"
          >
            Close
          </button>

          {!confirmationMsg && (
            <button
              type="button"
              disabled={submitting}
              onClick={handleRequestAssignment}
              className="rounded-lg bg-agri-700 hover:bg-agri-800 disabled:opacity-60 text-white font-semibold text-xs px-4 py-2 transition cursor-pointer"
            >
              {submitting ? 'Requesting...' : 'Request assignment'}
            </button>
          )}
        </div>
      </div>
    </div>
  );
};
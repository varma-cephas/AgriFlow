import React, { useEffect, useState } from 'react';
import { CheckCircle2, Info, X } from 'lucide-react';
import type { FarmVisitRow } from '../../types/agent';

interface ConfirmFarmVisitModalProps {
  visit: FarmVisitRow | null;
  initialMode: 'confirm' | 'reject';
  onClose: () => void;
  onConfirm: (visitId: number, visitDate: string, findings: string) => Promise<void>;
  onReject: (visitId: number, reason: string) => Promise<void>;
}

export const ConfirmFarmVisitModal: React.FC<ConfirmFarmVisitModalProps> = ({
  visit,
  initialMode,
  onClose,
  onConfirm,
  onReject,
}) => {
  const todayStr = new Date().toISOString().slice(0, 10);

  const [mode, setMode] = useState<'confirm' | 'reject'>(initialMode);
  const [visitDate, setVisitDate] = useState(todayStr);
  const [notes, setNotes] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!visit) return;
    setMode(initialMode);
    setVisitDate(todayStr);
    setNotes('');
    setError(null);
  }, [visit, initialMode, todayStr]);

  if (!visit) return null;

  const firstName = visit.farmerName.split(' ')[0];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!notes.trim()) {
      setError(
        mode === 'confirm'
          ? 'Please enter what you found during the farm inspection.'
          : 'Please explain why this farm could not be verified.'
      );
      return;
    }

    setSubmitting(true);
    try {
      if (mode === 'confirm') {
        await onConfirm(visit.id, visitDate, notes.trim());
      } else {
        await onReject(visit.id, notes.trim());
      }
      onClose();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Unable to submit visit outcome.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/45 backdrop-blur-[1px] p-4">
      <div className="relative w-full max-w-[460px] rounded-2xl bg-white p-6 shadow-xl border border-slate-100">
        <button
          type="button"
          onClick={onClose}
          aria-label="Close modal"
          className="absolute right-4 top-4 text-slate-400 hover:text-slate-600 cursor-pointer"
        >
          <X className="w-4 h-4" />
        </button>

        {/* Modal Header */}
        <div className="flex items-start gap-3">
          <div className="w-9 h-9 rounded-full bg-emerald-50 border border-emerald-200 flex items-center justify-center text-agri-700 shrink-0 mt-0.5">
            <CheckCircle2 className="w-4 h-4" />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-900">
              {mode === 'confirm'
                ? `Confirm visit to ${visit.farmName}`
                : `Can't verify ${visit.farmName}`}
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">
              {visit.farmerName} · UPI {visit.landUpi} · {visit.districtSector}
            </p>
          </div>
        </div>

        {error && (
          <div className="mt-4 rounded-lg bg-red-50 border border-red-200 p-2.5 text-xs text-red-700">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="mt-5 space-y-4">
          {mode === 'confirm' && (
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                Visit date <span className="text-red-500">*</span>
              </label>
              <input
                type="date"
                required
                value={visitDate}
                onChange={(e) => setVisitDate(e.target.value)}
                className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs text-slate-800 focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
              />
            </div>
          )}

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1.5">
              {mode === 'confirm' ? 'What you found' : 'Reason farm could not be verified'}{' '}
              <span className="text-red-500">*</span>
            </label>
            <textarea
              rows={3}
              required
              placeholder={
                mode === 'confirm'
                  ? 'Parcel matches the UPI. Maize and beans growing on about 3.5 ha.'
                  : 'e.g. Land UPI does not match the parcel shown or farmer was unreachable.'
              }
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              className="w-full rounded-lg border border-agri-700/70 bg-white px-3 py-2.5 text-xs text-slate-800 placeholder:text-slate-400 focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
            />
          </div>

          {/* Info Callout */}
          {mode === 'confirm' ? (
            <div className="rounded-xl bg-emerald-50/80 border border-emerald-200/80 p-3 flex items-start gap-2.5 text-xs">
              <Info className="w-4 h-4 text-agri-700 shrink-0 mt-0.5" />
              <div>
                <p className="font-semibold text-emerald-950">Confirming verifies the farm</p>
                <p className="text-emerald-800/85 text-[11px] mt-0.5">
                  {firstName}&apos;s listings become visible to buyers with the Verified farm badge.
                </p>
              </div>
            </div>
          ) : (
            <div className="rounded-xl bg-red-50/80 border border-red-200/80 p-3 flex items-start gap-2.5 text-xs">
              <Info className="w-4 h-4 text-red-600 shrink-0 mt-0.5" />
              <div>
                <p className="font-semibold text-red-950">Sent back to admin</p>
                <p className="text-red-800/85 text-[11px] mt-0.5">
                  {firstName}&apos;s account remains pending until an administrator resolves this
                  issue.
                </p>
              </div>
            </div>
          )}

          {/* Action Footer */}
          <div className="pt-1 flex items-center justify-end gap-2.5">
            {mode === 'confirm' ? (
              <>
                <button
                  type="button"
                  onClick={() => {
                    setMode('reject');
                    setError(null);
                  }}
                  className="rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 font-semibold text-xs px-4 py-2 transition cursor-pointer"
                >
                  Can&apos;t verify
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="rounded-lg bg-agri-700 hover:bg-agri-800 disabled:opacity-60 text-white font-semibold text-xs px-4 py-2 transition cursor-pointer"
                >
                  {submitting ? 'Verifying...' : 'Confirm & verify farm'}
                </button>
              </>
            ) : (
              <>
                <button
                  type="button"
                  onClick={() => {
                    setMode('confirm');
                    setError(null);
                  }}
                  className="rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 font-semibold text-xs px-4 py-2 transition cursor-pointer"
                >
                  Back to confirm
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="rounded-lg bg-red-600 hover:bg-red-700 disabled:opacity-60 text-white font-semibold text-xs px-4 py-2 transition cursor-pointer"
                >
                  {submitting ? 'Sending...' : 'Send back to admin'}
                </button>
              </>
            )}
          </div>
        </form>
      </div>
    </div>
  );
};
import React, { useState } from 'react';
import { Mail, X } from 'lucide-react';
import { authApi } from '../api/authApi';

interface ForgotPasswordModalProps {
  isOpen: boolean;
  initialEmail?: string;
  onClose: () => void;
}

export const ForgotPasswordModal: React.FC<ForgotPasswordModalProps> = ({
  isOpen,
  initialEmail = '',
  onClose,
}) => {
  const [email, setEmail] = useState(initialEmail);
  const [submitting, setSubmitting] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await authApi.requestOtp({ email, purpose: 'PasswordReset' });
      setSent(true);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Unable to send reset code.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/45 backdrop-blur-[1px] p-4">
      <div className="relative w-full max-w-[420px] rounded-2xl bg-white p-6 shadow-xl border border-slate-100">
        <button
          type="button"
          onClick={onClose}
          aria-label="Close modal"
          className="absolute right-4 top-4 text-slate-400 hover:text-slate-600 cursor-pointer"
        >
          <X className="w-4 h-4" />
        </button>

        <div className="flex items-start gap-3">
          <div className="w-9 h-9 rounded-full bg-agri-50 border border-emerald-200 flex items-center justify-center text-agri-700 shrink-0 mt-0.5">
            <Mail className="w-4 h-4" />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-900">Reset your password</h3>
            <p className="text-xs text-slate-500 mt-1 leading-relaxed">
              Enter the email you signed up with. We&apos;ll send a link or verification code to
              choose a new password.
            </p>
          </div>
        </div>

        {error && (
          <div className="mt-4 p-2.5 rounded-lg bg-red-50 border border-red-200 text-xs text-red-700">
            {error}
          </div>
        )}

        {sent ? (
          <div className="mt-5 space-y-4">
            <div className="p-3 rounded-lg bg-agri-50 border border-emerald-200 text-xs text-agri-800 leading-relaxed">
              If <strong>{email}</strong> is registered on AgriFlow, a reset verification code has
              been sent to your inbox.
            </div>
            <div className="flex justify-end">
              <button
                type="button"
                onClick={() => {
                  setSent(false);
                  onClose();
                }}
                className="rounded-lg bg-agri-700 hover:bg-agri-800 text-white font-semibold text-xs px-4 py-2 transition cursor-pointer"
              >
                Done
              </button>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="mt-5 space-y-4">
            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1.5">
                Email address
              </label>
              <div className="relative">
                <Mail className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
                <input
                  type="email"
                  required
                  placeholder="jeanbosco@greenhills.example"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="w-full rounded-lg border border-agri-700 pl-9 pr-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-agri-700/20"
                />
              </div>
            </div>

            <div className="flex items-center justify-end gap-2.5 pt-1">
              <button
                type="button"
                onClick={onClose}
                className="rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 font-medium text-xs px-4 py-2 transition cursor-pointer"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={submitting}
                className="rounded-lg bg-agri-700 hover:bg-agri-800 disabled:opacity-60 text-white font-semibold text-xs px-4 py-2 transition cursor-pointer"
              >
                {submitting ? 'Sending...' : 'Send reset code'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
};
import React, { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { MailCheck, ArrowLeft, RefreshCw } from 'lucide-react';
import { AuthSplitLayout } from '../../components/AuthSplitLayout';
import { authApi } from '../../api/authApi';
import { useAuth } from '../../context/AuthContext';
import type { UserRole } from '../../types/auth';

interface VerifyLocationState {
  email?: string;
  role?: UserRole;
  purpose?: 'Verification' | 'Login';
  keepSignedIn?: boolean;
}

export const VerifyOtpPage: React.FC = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const { setSession } = useAuth();

  const state = (location.state as VerifyLocationState) ?? {};
  const [email] = useState<string>(state.email ?? '');
  const purpose = state.purpose ?? 'Verification';
  const keepSignedIn = state.keepSignedIn ?? true;

  const [digits, setDigits] = useState<string[]>(['', '', '', '', '', '']);
  const [submitting, setSubmitting] = useState(false);
  const [resending, setResending] = useState(false);
  const [cooldown, setCooldown] = useState(45);
  const [error, setError] = useState<string | null>(null);
  const [infoMessage, setInfoMessage] = useState<string | null>(null);

  const inputRefs = useRef<Array<HTMLInputElement | null>>([]);

  useEffect(() => {
    inputRefs.current[0]?.focus();
  }, []);

  useEffect(() => {
    if (cooldown <= 0) return;
    const timer = setInterval(() => setCooldown((c) => Math.max(0, c - 1)), 1000);
    return () => clearInterval(timer);
  }, [cooldown]);

  const handleDigitChange = (index: number, value: string) => {
    const clean = value.replace(/\D/g, '');
    if (!clean) {
      const next = [...digits];
      next[index] = '';
      setDigits(next);
      return;
    }

    // Handle pasting full 6-digit code
    if (clean.length > 1) {
      const pasted = clean.slice(0, 6).split('');
      const next = ['', '', '', '', '', ''];
      pasted.forEach((ch, idx) => {
        next[idx] = ch;
      });
      setDigits(next);
      const focusIdx = Math.min(pasted.length, 5);
      inputRefs.current[focusIdx]?.focus();
      return;
    }

    const next = [...digits];
    next[index] = clean;
    setDigits(next);
    if (index < 5) {
      inputRefs.current[index + 1]?.focus();
    }
  };

  const handleKeyDown = (index: number, e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Backspace' && !digits[index] && index > 0) {
      inputRefs.current[index - 1]?.focus();
    }
  };

  const handleVerify = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);
        setInfoMessage(null);

        const code = digits.join('');
        if (!email) {
            setError('Missing email address. Please return to registration or sign in.');
            return;
        }
        if (code.length !== 6) {
            setError('Enter all 6 digits from your verification email.');
            return;
        }

        setSubmitting(true);
        try {
            const auth = await authApi.verifyOtp({ email, code, purpose });
            setSession(auth, keepSignedIn);

            if (auth.verificationStatus === 'Unverified' || auth.verificationStatus === 'Pending') {
            navigate('/pending', { replace: true });
            } else if (auth.role === 'Buyer') {
            navigate('/buyer/dashboard', { replace: true });
            } else {
            navigate('/farmer/dashboard', { replace: true });
            }
        } catch (err: any) {
            setError(err.response?.data?.message || 'Invalid or expired verification code.');
        } finally {
            setSubmitting(false);
        }
    };

  const handleResend = async () => {
    if (!email || cooldown > 0 || resending) return;
    setError(null);
    setInfoMessage(null);
    setResending(true);
    try {
      await authApi.requestOtp({ email, purpose });
      setDigits(['', '', '', '', '', '']);
      setCooldown(45);
      setInfoMessage('A new 6-digit verification code has been sent to your email.');
      inputRefs.current[0]?.focus();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Unable to resend code right now.');
    } finally {
      setResending(false);
    }
  };

  return (
    <AuthSplitLayout
      headline={
        state.role === 'Buyer'
          ? 'Reliable bulk supply, direct from verified farms.'
          : 'Sell your harvest before you harvest it.'
      }
    >
      <div>
        <Link
        to={purpose === 'Login' ? '/login' : '/register'}
        className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-500 hover:text-slate-800 mb-6"
        >
        <ArrowLeft className="w-3.5 h-3.5" />
        {purpose === 'Login' ? 'Back to sign in' : 'Back to registration'}
        </Link>

        <div className="w-11 h-11 rounded-full bg-agri-50 border border-emerald-200 flex items-center justify-center text-agri-700 mb-5">
          <MailCheck className="w-5 h-5" />
        </div>

        <h2 className="text-2xl font-bold text-slate-900 tracking-tight">
          Check your email
        </h2>
        <p className="text-sm text-slate-500 mt-1.5 leading-relaxed">
          We sent a 6-digit verification code to{' '}
          <span className="font-semibold text-slate-800">
            {email || 'your email address'}
          </span>
          . Enter it below within 5 minutes.
        </p>

        {error && (
          <div className="mt-5 p-3 rounded-lg bg-red-50 border border-red-200 text-xs text-red-700">
            {error}
          </div>
        )}

        {infoMessage && (
          <div className="mt-5 p-3 rounded-lg bg-agri-50 border border-emerald-200 text-xs text-agri-800">
            {infoMessage}
          </div>
        )}

        <form onSubmit={handleVerify} className="mt-6 space-y-6">
          <div className="flex items-center justify-between gap-2 sm:gap-3">
            {digits.map((digit, index) => (
              <input
                key={index}
                ref={(el) => {
                  inputRefs.current[index] = el;
                }}
                type="text"
                inputMode="numeric"
                maxLength={6}
                value={digit}
                onChange={(e) => handleDigitChange(index, e.target.value)}
                onKeyDown={(e) => handleKeyDown(index, e)}
                className="w-12 h-13 sm:w-14 sm:h-14 text-center text-xl font-bold text-slate-900 rounded-xl border border-slate-200 focus:border-agri-700 focus:outline-none focus:ring-2 focus:ring-agri-700/20"
              />
            ))}
          </div>

          <button
            type="submit"
            disabled={submitting}
            className="w-full rounded-lg bg-agri-700 hover:bg-agri-800 disabled:opacity-60 text-white font-semibold text-sm py-2.5 transition cursor-pointer"
          >
            {submitting ? 'Verifying code...' : 'Verify and continue'}
          </button>
        </form>

        <div className="mt-6 flex items-center justify-between text-xs text-slate-500">
          <span>Didn&apos;t receive the email?</span>
          <button
            type="button"
            disabled={cooldown > 0 || resending || !email}
            onClick={handleResend}
            className="inline-flex items-center gap-1.5 font-semibold text-agri-700 hover:text-agri-800 disabled:text-slate-400 cursor-pointer disabled:cursor-not-allowed"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${resending ? 'animate-spin' : ''}`} />
            {cooldown > 0 ? `Resend in ${cooldown}s` : 'Resend code'}
          </button>
        </div>
      </div>
    </AuthSplitLayout>
  );
};
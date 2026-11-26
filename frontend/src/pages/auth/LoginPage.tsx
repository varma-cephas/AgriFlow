import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Mail, Lock, Info } from 'lucide-react';
import { AuthSplitLayout } from '../../components/AuthSplitLayout';
import { ForgotPasswordModal } from '../../components/ForgotPasswordModal';
import { authApi } from '../../api/authApi';
import { useAuth } from '../../context/AuthContext';

export const LoginPage: React.FC = () => {
  const navigate = useNavigate();
  const { setSession } = useAuth();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [keepSignedIn, setKeepSignedIn] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [forgotOpen, setForgotOpen] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const auth = await authApi.login({ email, password });
      setSession(auth, keepSignedIn);

      if (auth.verificationStatus === 'Unverified' || auth.verificationStatus === 'Pending') {
        navigate('/pending', { replace: true });
      } else if (auth.role === 'Buyer') {
        navigate('/buyer/dashboard', { replace: true });
      } else {
        navigate('/farmer/dashboard', { replace: true });
      }
    } catch (err: any) {
      setError(err.response?.data?.message || 'Invalid email or password.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthSplitLayout headline="Farm-to-business supply, traced from seed to sale.">
      <div className="max-w-[440px] mx-auto">
        <h2 className="text-2xl font-bold text-slate-900 tracking-tight">Welcome back</h2>
        <p className="text-sm text-slate-500 mt-1">Sign in to your AgriFlow workspace.</p>

        {error && (
          <div className="mt-5 p-3 rounded-lg bg-red-50 border border-red-200 text-xs text-red-700">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="mt-6 space-y-4">
          <div>
            <label className="block text-xs font-medium text-slate-700 mb-1.5">
              Email address
            </label>
            <div className="relative">
              <Mail className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
              <input
                type="email"
                required
                placeholder="you@example.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full rounded-lg border border-slate-200 pl-9 pr-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-700 mb-1.5">
              Password
            </label>
            <div className="relative">
              <Lock className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
              <input
                type={showPassword ? 'text' : 'password'}
                required
                placeholder="Enter your password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full rounded-lg border border-slate-200 pl-9 pr-14 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
              />
              <button
                type="button"
                onClick={() => setShowPassword((v) => !v)}
                className="absolute right-3 top-2 text-xs font-semibold text-agri-700 hover:text-agri-800 cursor-pointer"
              >
                {showPassword ? 'Hide' : 'Show'}
              </button>
            </div>
          </div>

          <div className="flex items-center justify-between pt-0.5">
            <label className="flex items-center gap-2 cursor-pointer select-none">
              <input
                type="checkbox"
                checked={keepSignedIn}
                onChange={(e) => setKeepSignedIn(e.target.checked)}
                className="w-4 h-4 rounded border-slate-300 accent-agri-700"
              />
              <span className="text-xs text-slate-700">Keep me signed in</span>
            </label>

            <button
              type="button"
              onClick={() => setForgotOpen(true)}
              className="text-xs font-semibold text-agri-700 hover:underline cursor-pointer"
            >
              Forgot password?
            </button>
          </div>

          <button
            type="submit"
            disabled={submitting}
            className="w-full rounded-lg bg-agri-700 hover:bg-agri-800 disabled:opacity-60 text-white font-semibold text-sm py-2.5 transition cursor-pointer"
          >
            {submitting ? 'Signing in...' : 'Sign in'}
          </button>
        </form>

        <p className="text-center text-xs text-slate-500 mt-5">
          New to AgriFlow?{' '}
          <Link to="/register" className="font-semibold text-agri-700 hover:underline">
            Create an account
          </Link>
        </p>

        {/* Agents and administrators info card */}
        <div className="mt-6 rounded-xl bg-blue-50/80 border border-blue-200/80 p-3.5 flex items-start gap-3">
          <Info className="w-4 h-4 text-blue-600 shrink-0 mt-0.5" />
          <div className="text-xs leading-relaxed">
            <p className="font-semibold text-slate-800">Agents and administrators</p>
            <p className="text-slate-600 mt-0.5">
              Your account is created by an AgriFlow administrator. Use the details they sent you.
            </p>
          </div>
        </div>
      </div>

      <ForgotPasswordModal
        isOpen={forgotOpen}
        initialEmail={email}
        onClose={() => setForgotOpen(false)}
      />
    </AuthSplitLayout>
  );
};
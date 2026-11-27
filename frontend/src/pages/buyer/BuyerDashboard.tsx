import React from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Store, Building2, MapPin, ShieldCheck, Clock, LogOut } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';

export const BuyerDashboard: React.FC = () => {
  const navigate = useNavigate();
  const { user, profile, logout } = useAuth();
  const business = profile?.buyerBusiness;
  const status = business?.verificationStatus ?? user?.verificationStatus ?? 'Pending';

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-white border-b border-slate-200 px-6 py-4 flex items-center justify-between">
        <div className="flex items-center gap-2.5 text-agri-800 font-bold text-lg">
          <Store className="w-6 h-6 text-agri-700" />
          <span>AgriFlow</span>
          <span className="ml-2 rounded-full bg-blue-50 border border-blue-200 px-2.5 py-0.5 text-xs font-medium text-blue-800">
            Buyer Marketplace
          </span>
        </div>

        <div className="flex items-center gap-4">
          <span className="text-xs text-slate-600 hidden sm:inline">
            {user?.fullName} {profile?.jobTitle ? `· ${profile.jobTitle}` : ''}
          </span>
          <button
            type="button"
            onClick={handleLogout}
            className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 cursor-pointer"
          >
            <LogOut className="w-3.5 h-3.5" />
            Sign out
          </button>
        </div>
      </header>

      <main className="max-w-5xl mx-auto p-6 space-y-6">
        {status !== 'Verified' && (
          <div className="rounded-xl bg-amber-50 border border-amber-200 p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div className="flex items-start gap-3">
              <Clock className="w-5 h-5 text-amber-700 shrink-0 mt-0.5" />
              <div className="text-xs">
                <p className="font-semibold text-amber-900">
                  Business verification status: {status}
                </p>
                <p className="text-amber-800 mt-0.5">
                  You can browse available produce listings now. Reserving harvests and placing
                  bulk orders unlock once your RDB code is verified.
                </p>
              </div>
            </div>
            <Link
              to="/pending"
              className="text-xs font-semibold text-amber-900 underline shrink-0"
            >
              View status timeline
            </Link>
          </div>
        )}

        <div className="bg-white rounded-2xl border border-slate-200 p-6">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <h1 className="text-xl font-bold text-slate-900 flex items-center gap-2">
                <Building2 className="w-5 h-5 text-agri-700" />
                {business?.name ?? 'Business Account'}
              </h1>
              <p className="text-xs text-slate-500 flex items-center gap-1.5 mt-1">
                <MapPin className="w-3.5 h-3.5 text-slate-400" />
                {business ? `${business.address}, ${business.city}` : 'Loading business details...'}
              </p>
            </div>

            <span
              className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold ${
                status === 'Verified'
                  ? 'bg-agri-50 text-agri-800 border border-emerald-200'
                  : 'bg-amber-50 text-amber-800 border border-amber-200'
              }`}
            >
              {status === 'Verified' ? (
                <ShieldCheck className="w-3.5 h-3.5" />
              ) : (
                <Clock className="w-3.5 h-3.5" />
              )}
              {status}
            </span>
          </div>

          <div className="mt-6 grid grid-cols-1 sm:grid-cols-3 gap-4 pt-5 border-t border-slate-100 text-xs">
            <div>
              <span className="text-slate-400 block">Business Type</span>
              <span className="font-semibold text-slate-800 mt-0.5 block">
                {business?.businessType ?? '—'}
              </span>
            </div>
            <div>
              <span className="text-slate-400 block">RDB Company Code</span>
              <span className="font-semibold text-slate-800 mt-0.5 block">
                {business?.rdbCompanyCode ?? '—'}
              </span>
            </div>
            <div>
              <span className="text-slate-400 block">Procurement Contact</span>
              <span className="font-semibold text-slate-800 mt-0.5 block">
                {user?.email ?? '—'}
              </span>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
};
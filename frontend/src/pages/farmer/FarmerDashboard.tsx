import React from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Sprout, MapPin, ShieldAlert, ShieldCheck, LogOut, Clock } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';

export const FarmerDashboard: React.FC = () => {
  const navigate = useNavigate();
  const { user, profile, logout } = useAuth();
  const farm = profile?.farmerProfile;
  const status = farm?.verificationStatus ?? user?.verificationStatus ?? 'Unverified';

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-white border-b border-slate-200 px-6 py-4 flex items-center justify-between">
        <div className="flex items-center gap-2.5 text-agri-800 font-bold text-lg">
          <Sprout className="w-6 h-6 text-agri-700" />
          <span>AgriFlow</span>
          <span className="ml-2 rounded-full bg-agri-50 border border-emerald-200 px-2.5 py-0.5 text-xs font-medium text-agri-800">
            Farmer Workspace
          </span>
        </div>

        <div className="flex items-center gap-4">
          <span className="text-xs text-slate-600 hidden sm:inline">
            {user?.fullName} ({user?.email})
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
                  Farm verification status: {status}
                </p>
                <p className="text-amber-800 mt-0.5">
                  You can record seed stock and planting batches now. Your listings will become
                  visible to buyers once an administrator or agronomist verifies your UPI.
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
              <h1 className="text-xl font-bold text-slate-900">
                {farm?.farmName ?? 'My Farm'}
              </h1>
              <p className="text-xs text-slate-500 flex items-center gap-1.5 mt-1">
                <MapPin className="w-3.5 h-3.5 text-slate-400" />
                {farm
                  ? `${farm.village}, ${farm.cell}, ${farm.sector}, ${farm.district} (${farm.province})`
                  : 'Loading farm location...'}
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
                <ShieldAlert className="w-3.5 h-3.5" />
              )}
              {status}
            </span>
          </div>

          <div className="mt-6 grid grid-cols-1 sm:grid-cols-3 gap-4 pt-5 border-t border-slate-100 text-xs">
            <div>
              <span className="text-slate-400 block">Land UPI</span>
              <span className="font-semibold text-slate-800 mt-0.5 block">
                {farm?.landUpi ?? '—'}
              </span>
            </div>
            <div>
              <span className="text-slate-400 block">Main Crops</span>
              <span className="font-semibold text-slate-800 mt-0.5 block">
                {farm?.mainCrops ?? '—'}
              </span>
            </div>
            <div>
              <span className="text-slate-400 block">Farm Size</span>
              <span className="font-semibold text-slate-800 mt-0.5 block">
                {farm?.farmSizeHa ? `${farm.farmSizeHa} ha` : 'Not specified'}
              </span>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
};
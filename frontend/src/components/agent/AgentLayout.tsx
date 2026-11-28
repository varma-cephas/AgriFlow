import React, { useEffect, useState } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
import {
  Sprout,
  Home,
  Users,
  MapPinCheck,
  UserPlus,
  Store,
  Search,
  Bell,
  LogOut,
  FileText
} from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { agentApi } from '../../api/agentApi';

interface AgentLayoutProps {
  breadcrumb?: string;
  roleTitle?: string;
  hasNotifications?: boolean;
  pendingVisitsCount?: number;
  searchQuery?: string;
  onSearchChange?: (value: string) => void;
  onComingSoon?: (featureLabel: string) => void;
  children: React.ReactNode;
}

function getInitials(name: string): string {
  const parts = name.trim().split(/\s+/);
  if (parts.length === 0 || !parts[0]) return 'AG';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}

export const AgentLayout: React.FC<AgentLayoutProps> = ({
  breadcrumb = 'Overview',
  roleTitle,
  hasNotifications = false,
  pendingVisitsCount,
  searchQuery = '',
  onSearchChange,
  onComingSoon,
  children,
}) => {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, profile, logout } = useAuth();
  const [fetchedBadgeCount, setFetchedBadgeCount] = useState<number>(0);

  useEffect(() => {
    if (typeof pendingVisitsCount === 'number') return;
    let active = true;
    agentApi
      .getFarmVisitRequests()
      .then((res) => {
        if (active) setFetchedBadgeCount(res.kpis.waitingCount);
      })
      .catch(() => {});
    return () => {
      active = false;
    };
  }, [pendingVisitsCount, location.pathname]);

  const badgeCount =
    typeof pendingVisitsCount === 'number' ? pendingVisitsCount : fetchedBadgeCount;

  const displayName = user?.fullName ?? 'Field Agent';
  const displaySubtitle = roleTitle ?? profile?.jobTitle ?? 'Field agent · Nyagatare';
  const initials = getInitials(displayName);

  const isOnDashboard = location.pathname === '/agent/dashboard';
  const isMyFarmersActive =
    location.pathname.startsWith('/agent/farmers') ||
    (isOnDashboard && location.hash === '#my-farmers-section');

  const formattedDate = new Date().toLocaleDateString('en-GB', {
    weekday: 'short',
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  });

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const handleMyFarmersClick = () => {
    if (isOnDashboard) {
      navigate('/agent/dashboard#my-farmers-section', { replace: true });
      document.getElementById('my-farmers-section')?.scrollIntoView({ behavior: 'smooth' });
    } else {
      navigate('/agent/dashboard#my-farmers-section');
    }
  };

  const handleBellClick = () => {
    if (isOnDashboard) {
      document.getElementById('attention-section')?.scrollIntoView({ behavior: 'smooth' });
    } else {
      navigate('/agent/dashboard#attention-section');
    }
  };

  return (
    <div className="min-h-screen flex bg-[#f6f8f6] text-slate-900">
      {/* Left Forest-Green Sidebar */}
      <aside className="w-60 bg-[#0d3520] text-white flex flex-col justify-between shrink-0 sticky top-0 h-screen">
        <div>
          {/* Brand Logo */}
          <div className="px-5 pt-6 pb-5 flex items-center gap-2.5">
            <Sprout className="w-6 h-6 text-emerald-300" />
            <span className="text-lg font-bold tracking-tight">AgriFlow</span>
          </div>

          {/* Navigation Group */}
          <div className="px-3 mt-2">
            <p className="px-3 text-[10px] font-semibold uppercase tracking-widest text-emerald-200/50 mb-2">
              Agent workspace
            </p>

            <nav className="space-y-1">
              <NavLink
                to="/agent/dashboard"
                end
                className={({ isActive }) =>
                  `flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs font-medium transition ${
                    isActive && !isMyFarmersActive
                      ? 'bg-white/12 text-white font-semibold'
                      : 'text-emerald-100/75 hover:bg-white/6 hover:text-white'
                  }`
                }
              >
                <Home className="w-4 h-4 shrink-0" />
                <span>Overview</span>
              </NavLink>

              <button
                type="button"
                onClick={handleMyFarmersClick}
                className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs font-medium transition cursor-pointer ${
                  isMyFarmersActive
                    ? 'bg-white/12 text-white font-semibold'
                    : 'text-emerald-100/75 hover:bg-white/6 hover:text-white'
                }`}
              >
                <Users className="w-4 h-4 shrink-0" />
                <span>My farmers</span>
              </button>

              <NavLink
                to="/agent/farm-visits"
                className={({ isActive }) =>
                  `flex items-center justify-between px-3 py-2.5 rounded-lg text-xs font-medium transition ${
                    isActive
                      ? 'bg-white/12 text-white font-semibold'
                      : 'text-emerald-100/75 hover:bg-white/6 hover:text-white'
                  }`
                }
              >
                <span className="flex items-center gap-3">
                  <MapPinCheck className="w-4 h-4 shrink-0" />
                  <span>Farm visits</span>
                </span>
                {badgeCount > 0 && (
                  <span className="rounded-full bg-amber-400/20 border border-amber-300/35 px-2 py-0.5 text-[10px] font-bold text-amber-200">
                    {badgeCount}
                  </span>
                )}
              </NavLink>

              <NavLink
                to="/agent/register-farmer"
                className={({ isActive }) =>
                  `flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs font-medium transition ${
                    isActive
                      ? 'bg-white/12 text-white font-semibold'
                      : 'text-emerald-100/75 hover:bg-white/6 hover:text-white'
                  }`
                }
              >
                <UserPlus className="w-4 h-4 shrink-0" />
                <span>Register farmer</span>
              </NavLink>

              <button
                type="button"
                onClick={() => onComingSoon?.('Produce marketplace')}
                className="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs font-medium text-emerald-100/75 hover:bg-white/6 hover:text-white transition cursor-pointer"
              >
                <Store className="w-4 h-4 shrink-0" />
                <span>Marketplace</span>
              </button>

              <NavLink
                to="/agent/audit-log"
                className={({ isActive }) =>
                  `flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs font-medium transition ${
                    isActive
                      ? 'bg-white/12 text-white font-semibold'
                      : 'text-emerald-100/75 hover:bg-white/6 hover:text-white'
                  }`
                }
              >
                <FileText className="w-4 h-4 shrink-0" />
                <span>Audit log</span>
              </NavLink>
            </nav>
          </div>
        </div>

        {/* Bottom Profile Card */}
        <div className="p-3">
          <div className="rounded-xl bg-white/8 border border-white/10 p-3 flex items-center justify-between gap-2">
            <div className="flex items-center gap-2.5 min-w-0">
              <div className="w-8 h-8 rounded-full bg-emerald-300/20 border border-emerald-300/30 text-emerald-200 flex items-center justify-center text-xs font-bold shrink-0">
                {initials}
              </div>
              <div className="min-w-0">
                <p className="text-xs font-semibold text-white truncate">{displayName}</p>
                <p className="text-[11px] text-emerald-200/65 truncate">{displaySubtitle}</p>
              </div>
            </div>

            <button
              type="button"
              onClick={handleLogout}
              title="Sign out"
              className="p-1.5 rounded-lg text-emerald-200/70 hover:text-white hover:bg-white/10 transition cursor-pointer shrink-0"
            >
              <LogOut className="w-4 h-4" />
            </button>
          </div>
        </div>
      </aside>

      {/* Right Main Area */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Top Utility Header */}
        <header className="h-14 bg-[#f6f8f6] border-b border-slate-200/80 px-6 lg:px-8 flex items-center justify-between gap-4 sticky top-0 z-20">
          <div className="text-xs text-slate-500 flex items-center gap-1.5 truncate">
            <span>Agent workspace</span>
            <span>/</span>
            <span className="font-semibold text-slate-800 truncate">{breadcrumb}</span>
          </div>

          <div className="flex items-center gap-3 shrink-0">
            <div className="relative hidden sm:block w-56 lg:w-64">
              <Search className="w-3.5 h-3.5 text-slate-400 absolute left-3 top-2.5" />
              <input
                type="text"
                placeholder="Search..."
                value={searchQuery}
                onChange={(e) => onSearchChange?.(e.target.value)}
                className="w-full rounded-lg border border-slate-200 bg-white pl-8 pr-3 py-1.5 text-xs text-slate-800 placeholder:text-slate-400 focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
              />
            </div>

            <button
              type="button"
              onClick={handleBellClick}
              aria-label="Needs your attention"
              className="relative w-8 h-8 rounded-lg border border-slate-200 bg-white flex items-center justify-center text-slate-600 hover:bg-slate-50 transition cursor-pointer"
            >
              <Bell className="w-4 h-4" />
              {hasNotifications && (
                <span className="absolute top-1.5 right-1.5 w-1.5 h-1.5 rounded-full bg-red-500" />
              )}
            </button>

            <span className="text-xs text-slate-500 hidden md:inline">{formattedDate}</span>
          </div>
        </header>

        {/* Page Content */}
        <main className="flex-1 px-6 lg:px-8 py-6 max-w-[1280px] w-full mx-auto">
          {children}
        </main>
      </div>
    </div>
  );
};
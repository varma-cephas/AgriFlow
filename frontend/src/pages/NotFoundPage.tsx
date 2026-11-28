import React from 'react';
import { Link } from 'react-router-dom';
import { Sprout, ArrowLeft } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { getDefaultRouteForUser } from '../components/ProtectedRoute';

export const NotFoundPage: React.FC = () => {
  const { user } = useAuth();
  const homePath = user
    ? getDefaultRouteForUser(user.role, user.verificationStatus)
    : '/login';

  return (
    <div className="min-h-screen flex flex-col items-center justify-center bg-slate-50 px-4 text-center">
      <div className="w-12 h-12 rounded-2xl bg-agri-50 border border-emerald-200 flex items-center justify-center text-agri-700 mb-4">
        <Sprout className="w-6 h-6" />
      </div>
      <p className="text-xs font-semibold uppercase tracking-wider text-agri-700">404 Error</p>
      <h1 className="text-2xl font-bold text-slate-900 mt-1">Page not found</h1>
      <p className="text-sm text-slate-500 mt-1.5 max-w-sm">
        The page you are looking for doesn&apos;t exist or may have been moved.
      </p>
      <Link
        to={homePath}
        className="mt-6 inline-flex items-center gap-2 rounded-lg bg-agri-700 hover:bg-agri-800 text-white font-semibold text-xs px-4 py-2.5 transition"
      >
        <ArrowLeft className="w-4 h-4" />
        {user ? 'Back to my workspace' : 'Back to sign in'}
      </Link>
    </div>
  );
};
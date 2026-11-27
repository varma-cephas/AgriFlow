import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Clock, Check, Info } from 'lucide-react';
import { AuthSplitLayout } from '../../components/AuthSplitLayout';
import { useAuth } from '../../context/AuthContext';

export const PendingVerificationPage: React.FC = () => {
  const navigate = useNavigate();
  const { user, profile } = useAuth();

  const isBuyer = user?.role === 'Buyer';
  const firstName = (user?.fullName ?? 'there').split(' ')[0];

  const entityTitle = isBuyer
    ? `We're verifying ${profile?.buyerBusiness?.name ?? 'your business'}`
    : `We're checking ${profile?.farmerProfile?.farmName ?? 'your farm'}`;

  const entitySubtitle = isBuyer
    ? `Thanks, ${firstName}. An administrator is checking your business registration. This usually takes less than one business day.`
    : `Thanks, ${firstName}. An administrator is checking your National ID and land UPI. This usually takes 1–2 days.`;

  const secondStepTitle = isBuyer
    ? 'Business details submitted'
    : 'National ID and land UPI submitted';

  const secondStepDetail = isBuyer
    ? `Registration ${profile?.buyerBusiness?.rdbCompanyCode ?? 'submitted'}`
    : `UPI ${profile?.farmerProfile?.landUpi ?? 'submitted'}${
        profile?.farmerProfile?.district ? ` · ${profile.farmerProfile.district}` : ''
      }`;

  const thirdStepTitle = isBuyer ? 'Admin review' : 'Farm check';
  const thirdStepDetail = isBuyer
    ? 'In progress · usually under 1 day'
    : 'In progress · usually 1–2 days';

  const fourthStepTitle = isBuyer
    ? 'Ready to reserve and order'
    : 'Listings visible to buyers';

  const fourthStepDetail = isBuyer
    ? `We'll email ${user?.email ?? 'you'}`
    : 'Your farm gets the Verified farm badge';

  return (
    <AuthSplitLayout
      headline={
        isBuyer
          ? 'Reliable bulk supply, direct from verified farms.'
          : 'Sell your harvest before you harvest it.'
      }
      thirdBulletText={
        isBuyer
          ? 'Every buyer business is checked before it can order.'
          : 'Every farm and every buyer business is checked.'
      }
    >
      <div className="max-w-[480px] mx-auto">
        {/* Amber Clock Icon */}
        <div className="w-11 h-11 rounded-full bg-amber-50 border border-amber-200/80 flex items-center justify-center text-amber-700 mb-5">
          <Clock className="w-5 h-5" />
        </div>

        <h2 className="text-2xl font-bold text-slate-900 tracking-tight">{entityTitle}</h2>
        <p className="text-sm text-slate-500 mt-1.5 leading-relaxed">{entitySubtitle}</p>

        {/* 4-Step Verification Card */}
        <div className="mt-6 rounded-2xl border border-slate-200 bg-white divide-y divide-slate-100">
          {/* Step 1: Account created */}
          <div className="p-4 flex items-center gap-3.5">
            <div className="w-6 h-6 rounded-full bg-agri-700 text-white flex items-center justify-center shrink-0">
              <Check className="w-3.5 h-3.5 stroke-[2.5]" />
            </div>
            <div>
              <div className="text-xs font-semibold text-slate-900">Account created</div>
              <div className="text-[11px] text-slate-500 mt-0.5">
                {new Date().toLocaleDateString('en-GB', {
                  day: '2-digit',
                  month: 'short',
                  year: 'numeric',
                })}
              </div>
            </div>
          </div>

          {/* Step 2: Details submitted */}
          <div className="p-4 flex items-center gap-3.5">
            <div className="w-6 h-6 rounded-full bg-agri-700 text-white flex items-center justify-center shrink-0">
              <Check className="w-3.5 h-3.5 stroke-[2.5]" />
            </div>
            <div>
              <div className="text-xs font-semibold text-slate-900">{secondStepTitle}</div>
              <div className="text-[11px] text-slate-500 mt-0.5">{secondStepDetail}</div>
            </div>
          </div>

          {/* Step 3: In review */}
          <div className="p-4 flex items-center justify-between gap-3">
            <div className="flex items-center gap-3.5">
              <div className="w-6 h-6 rounded-full bg-amber-50 border border-amber-200 text-amber-700 flex items-center justify-center shrink-0">
                <Clock className="w-3.5 h-3.5" />
              </div>
              <div>
                <div className="text-xs font-semibold text-slate-900">{thirdStepTitle}</div>
                <div className="text-[11px] text-slate-500 mt-0.5">{thirdStepDetail}</div>
              </div>
            </div>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-50 border border-amber-200 px-2.5 py-0.5 text-[11px] font-medium text-amber-800 shrink-0">
              <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
              In review
            </span>
          </div>

          {/* Step 4: Final unlock */}
          <div className="p-4 flex items-center gap-3.5">
            <div className="w-6 h-6 rounded-full bg-slate-100 shrink-0" />
            <div>
              <div className="text-xs font-semibold text-slate-700">{fourthStepTitle}</div>
              <div className="text-[11px] text-slate-400 mt-0.5">{fourthStepDetail}</div>
            </div>
          </div>
        </div>

        {/* Action Buttons */}
        <div className="mt-6 grid grid-cols-1 sm:grid-cols-2 gap-3">
          <button
            type="button"
            onClick={() =>
              navigate(isBuyer ? '/buyer/dashboard' : '/farmer/dashboard')
            }
            className="rounded-lg bg-agri-700 hover:bg-agri-800 text-white font-semibold text-xs py-2.5 px-4 transition cursor-pointer"
          >
            {isBuyer ? 'Browse marketplace' : 'Go to my farm workspace'}
          </button>

          <button
            type="button"
            onClick={() =>
              navigate(isBuyer ? '/buyer/dashboard' : '/farmer/dashboard', {
                state: { openEdit: true },
              })
            }
            className="rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 font-semibold text-xs py-2.5 px-4 transition cursor-pointer"
          >
            {isBuyer ? 'Edit business details' : 'Edit farm details'}
          </button>
        </div>

        {/* Bottom Info Note */}
        {isBuyer ? (
          <p className="text-[11px] text-slate-500 mt-4 leading-relaxed">
            You can browse listings now. Reserving and ordering unlock as soon as your business is
            verified. Questions? support@agriflow.example
          </p>
        ) : (
          <div className="mt-5 rounded-xl bg-blue-50/80 border border-blue-200/80 p-3.5 flex items-start gap-3">
            <Info className="w-4 h-4 text-blue-600 shrink-0 mt-0.5" />
            <div className="text-xs leading-relaxed">
              <p className="font-semibold text-slate-800">You can start now</p>
              <p className="text-slate-600 mt-0.5">
                Add seed stock and record plantings straight away. Buyers will see your listings
                once your farm is verified. Want it faster? Ask your farmer promoter or sector
                agronomist to visit.
              </p>
            </div>
          </div>
        )}
      </div>
    </AuthSplitLayout>
  );
};
import React from 'react';
import { Sprout, Store, ShieldCheck } from 'lucide-react';

interface AuthSplitLayoutProps {
  headline: string;
  subtitle?: string;
  thirdBulletText?: string;
  children: React.ReactNode;
}

export const AuthSplitLayout: React.FC<AuthSplitLayoutProps> = ({
  headline,
  subtitle = 'Farmers sell harvests in bulk, even before they are harvested. Verified businesses get reliable, traceable supply direct from the farm.',
  thirdBulletText = 'Every buyer business is checked before it can order.',
  children,
}) => {
  return (
    <div className="min-h-screen flex flex-col lg:flex-row bg-white">
      {/* Left Emerald Brand Panel */}
      <aside className="relative lg:w-[38%] xl:w-[36%] bg-gradient-to-b from-agri-700 via-agri-800 to-agri-900 text-white p-8 lg:p-12 flex flex-col justify-between overflow-hidden shrink-0">
        {/* Decorative subtle circles matching Figma */}
        <div className="pointer-events-none absolute -top-20 -right-20 w-80 h-80 rounded-full bg-white/[0.04]" />
        <div className="pointer-events-none absolute -bottom-24 -left-20 w-72 h-72 rounded-full bg-white/[0.04]" />

        <div className="relative z-10">
          {/* Logo */}
          <div className="flex items-center gap-2.5 text-lg font-semibold tracking-tight mb-12 lg:mb-16">
            <Sprout className="w-6 h-6 text-emerald-300" />
            <span>AgriFlow</span>
          </div>

          {/* Hero Copy */}
          <h1 className="text-3xl lg:text-[34px] font-bold leading-tight tracking-tight mb-4">
            {headline}
          </h1>
          <p className="text-sm text-emerald-100/85 leading-relaxed mb-10 max-w-md">
            {subtitle}
          </p>

          {/* 3 Feature Highlights */}
          <div className="space-y-5">
            <div className="flex items-start gap-3.5">
              <div className="w-9 h-9 rounded-lg bg-emerald-400/20 border border-emerald-300/20 flex items-center justify-center shrink-0 mt-0.5">
                <Sprout className="w-4 h-4 text-emerald-200" />
              </div>
              <div>
                <h3 className="text-sm font-semibold text-white">For farmers</h3>
                <p className="text-xs text-emerald-100/75 mt-0.5 leading-relaxed">
                  Record seed, plantings and harvests. Get buyers before you harvest.
                </p>
              </div>
            </div>

            <div className="flex items-start gap-3.5">
              <div className="w-9 h-9 rounded-lg bg-emerald-400/20 border border-emerald-300/20 flex items-center justify-center shrink-0 mt-0.5">
                <Store className="w-4 h-4 text-emerald-200" />
              </div>
              <div>
                <h3 className="text-sm font-semibold text-white">For businesses</h3>
                <p className="text-xs text-emerald-100/75 mt-0.5 leading-relaxed">
                  Supermarkets, hotels, schools and processors buy in bulk.
                </p>
              </div>
            </div>

            <div className="flex items-start gap-3.5">
              <div className="w-9 h-9 rounded-lg bg-emerald-400/20 border border-emerald-300/20 flex items-center justify-center shrink-0 mt-0.5">
                <ShieldCheck className="w-4 h-4 text-emerald-200" />
              </div>
              <div>
                <h3 className="text-sm font-semibold text-white">Verified on both sides</h3>
                <p className="text-xs text-emerald-100/75 mt-0.5 leading-relaxed">
                  {thirdBulletText}
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="relative z-10 pt-10 text-xs text-emerald-200/60">
          © 2026 AgriFlow · Rwanda
        </div>
      </aside>

      {/* Right Content Workspace */}
      <main className="flex-1 flex items-center justify-center p-6 sm:p-10 lg:p-14 overflow-y-auto">
        <div className="w-full max-w-[540px]">{children}</div>
      </main>
    </div>
  );
};
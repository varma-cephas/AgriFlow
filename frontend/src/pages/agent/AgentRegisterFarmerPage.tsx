import React, { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  UserPlus,
  Phone,
  MapPin,
  Users,
  Package,
  Truck,
  Lock,
  Info,
  X,
} from 'lucide-react';
import { AgentLayout } from '../../components/agent/AgentLayout';
import { FarmerRegisteredModal } from '../../components/agent/FarmerRegisteredModal';
import { agentApi } from '../../api/agentApi';
import type { RegisterNonDigitalFarmerResponse } from '../../types/agent';

const RWANDA_DISTRICTS: Record<string, string[]> = {
  'Eastern Province': [
    'Bugesera',
    'Gatsibo',
    'Kayonza',
    'Kirehe',
    'Ngoma',
    'Nyagatare',
    'Rwamagana',
  ],
  'Northern Province': ['Burera', 'Gakenke', 'Gicumbi', 'Musanze', 'Rulindo'],
  'Southern Province': [
    'Gisagara',
    'Huye',
    'Kamonyi',
    'Muhanga',
    'Nyamagabe',
    'Nyanza',
    'Nyaruguru',
    'Ruhango',
  ],
  'Western Province': [
    'Karongi',
    'Ngororero',
    'Nyabihu',
    'Nyamasheke',
    'Rubavu',
    'Rusizi',
    'Rutsiro',
  ],
  'Kigali City': ['Gasabo', 'Kicukiro', 'Nyarugenge'],
};

const LANGUAGES = ['Kinyarwanda', 'English', 'French', 'Swahili'];

export const AgentRegisterFarmerPage: React.FC = () => {
  const navigate = useNavigate();

  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const [village, setVillage] = useState('');
  const [preferredLanguage, setPreferredLanguage] = useState('Kinyarwanda');
  const [farmName, setFarmName] = useState('');
  const [province, setProvince] = useState('Eastern Province');
  const [district, setDistrict] = useState('Nyagatare');
  const [farmSizeAcres, setFarmSizeAcres] = useState('');
  const [consentConfirmed, setConsentConfirmed] = useState(false);

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [registeredResult, setRegisteredResult] =
    useState<RegisterNonDigitalFarmerResponse | null>(null);

  const districtsForProvince = useMemo(
    () => RWANDA_DISTRICTS[province] ?? [],
    [province]
  );

  const firstNameDisplay = useMemo(() => {
    const trimmed = fullName.trim();
    if (!trimmed) return 'The farmer';
    return trimmed.split(/\s+/)[0];
  }, [fullName]);

  const recordedOnLabel = useMemo(() => {
    const today = new Date().toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
    });
    const place = village.trim() || district || 'the farm';
    return `${today} · in person at ${place}`;
  }, [village, district]);

  const handleProvinceChange = (nextProvince: string) => {
    setProvince(nextProvince);
    const nextDistricts = RWANDA_DISTRICTS[nextProvince] ?? [];
    setDistrict(nextDistricts[0] ?? '');
  };

  const resetForm = () => {
    setFullName('');
    setPhone('');
    setVillage('');
    setPreferredLanguage('Kinyarwanda');
    setFarmName('');
    setProvince('Eastern Province');
    setDistrict('Nyagatare');
    setFarmSizeAcres('');
    setConsentConfirmed(false);
    setError(null);
    setRegisteredResult(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!fullName.trim() || !village.trim() || !farmName.trim() || !province || !district) {
      setError('Please complete all required fields.');
      return;
    }

    if (!consentConfirmed) {
      setError('Please confirm that the farmer agreed in person to let you manage their records.');
      return;
    }

    const parsedAcres = farmSizeAcres.trim() ? Number(farmSizeAcres) : undefined;
    if (parsedAcres !== undefined && (Number.isNaN(parsedAcres) || parsedAcres <= 0)) {
      setError('Farm size must be a positive number of acres.');
      return;
    }

    setSubmitting(true);
    try {
      const res = await agentApi.registerFarmer({
        fullName: fullName.trim(),
        phone: phone.trim() || undefined,
        village: village.trim(),
        preferredLanguage,
        farmName: farmName.trim(),
        province,
        district,
        farmSizeAcres: parsedAcres,
        consentConfirmed: true,
      });
      setRegisteredResult(res);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Unable to register farmer. Please try again.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AgentLayout
      breadcrumb="Register a farmer"
      onComingSoon={(label) =>
        setToastMessage(`${label} will be connected in the next implementation step.`)
      }
    >
      <div className="space-y-6">
        {toastMessage && (
          <div className="rounded-xl bg-blue-50 border border-blue-200 px-4 py-3 flex items-center justify-between gap-3 text-xs text-blue-900">
            <div className="flex items-center gap-2">
              <Info className="w-4 h-4 text-blue-600 shrink-0" />
              <span>{toastMessage}</span>
            </div>
            <button
              type="button"
              onClick={() => setToastMessage(null)}
              className="text-blue-600 hover:text-blue-800 cursor-pointer"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {/* Header */}
        <div className="flex items-center gap-3.5">
          <div className="w-10 h-10 rounded-xl bg-emerald-50 border border-emerald-200/80 flex items-center justify-center text-agri-700 shrink-0">
            <UserPlus className="w-5 h-5" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-slate-900 tracking-tight">Register a farmer</h1>
            <p className="text-xs text-slate-500 mt-0.5">
              For farmers without a smartphone or internet. They are assigned to you automatically.
            </p>
          </div>
        </div>

        {/* Two-Column Layout */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
          {/* Left Form Column (8 cols) */}
          <form onSubmit={handleSubmit} className="lg:col-span-8 space-y-5">
            {error && (
              <div className="rounded-xl bg-red-50 border border-red-200 p-3.5 text-xs text-red-700">
                {error}
              </div>
            )}

            {/* Card 1: Farmer details */}
            <section className="rounded-2xl bg-white border border-slate-200/90 p-6">
              <h2 className="text-sm font-bold text-slate-900">Farmer details</h2>

              <div className="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                    Full name <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    required
                    placeholder="Innocent Hakizimana"
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    className="w-full rounded-lg border border-slate-200 bg-white px-3.5 py-2 text-xs text-slate-800 placeholder:text-slate-400 focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                  />
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                    Phone <span className="text-slate-400 font-normal">(optional)</span>
                  </label>
                  <div className="relative">
                    <Phone className="w-3.5 h-3.5 text-slate-400 absolute left-3 top-2.5" />
                    <input
                      type="tel"
                      placeholder="If they have one"
                      value={phone}
                      onChange={(e) => setPhone(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 bg-white pl-9 pr-3.5 py-2 text-xs text-slate-800 placeholder:text-slate-400 focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                    Community / village <span className="text-red-500">*</span>
                  </label>
                  <div className="relative">
                    <MapPin className="w-3.5 h-3.5 text-slate-400 absolute left-3 top-2.5" />
                    <input
                      type="text"
                      required
                      placeholder="Rukomo"
                      value={village}
                      onChange={(e) => setVillage(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 bg-white pl-9 pr-3.5 py-2 text-xs text-slate-800 placeholder:text-slate-400 focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                    Preferred language
                  </label>
                  <select
                    value={preferredLanguage}
                    onChange={(e) => setPreferredLanguage(e.target.value)}
                    className="w-full rounded-lg border border-slate-200 bg-white px-3.5 py-2 text-xs text-slate-800 focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                  >
                    {LANGUAGES.map((lang) => (
                      <option key={lang} value={lang}>
                        {lang}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            </section>

            {/* Card 2: Farm */}
            <section className="rounded-2xl bg-white border border-slate-200/90 p-6">
              <h2 className="text-sm font-bold text-slate-900">Farm</h2>

              <div className="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                    Farm name <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    required
                    placeholder="Hakizimana Family Farm"
                    value={farmName}
                    onChange={(e) => setFarmName(e.target.value)}
                    className="w-full rounded-lg border border-slate-200 bg-white px-3.5 py-2 text-xs text-slate-800 placeholder:text-slate-400 focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                  />
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                    Province <span className="text-red-500">*</span>
                  </label>
                  <select
                    value={province}
                    onChange={(e) => handleProvinceChange(e.target.value)}
                    className="w-full rounded-lg border border-slate-200 bg-white px-3.5 py-2 text-xs text-slate-800 focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                  >
                    {Object.keys(RWANDA_DISTRICTS).map((prov) => (
                      <option key={prov} value={prov}>
                        {prov}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                    District <span className="text-red-500">*</span>
                  </label>
                  <select
                    value={district}
                    onChange={(e) => setDistrict(e.target.value)}
                    className="w-full rounded-lg border border-slate-200 bg-white px-3.5 py-2 text-xs text-slate-800 focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                  >
                    {districtsForProvince.map((dist) => (
                      <option key={dist} value={dist}>
                        {dist}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                    Farm size
                  </label>
                  <div className="relative">
                    <input
                      type="number"
                      step="0.1"
                      min="0.1"
                      placeholder="6"
                      value={farmSizeAcres}
                      onChange={(e) => setFarmSizeAcres(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 bg-white pl-3.5 pr-14 py-2 text-xs text-slate-800 placeholder:text-slate-400 focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                    <span className="absolute right-3.5 top-2 text-xs text-slate-400">acres</span>
                  </div>
                </div>
              </div>
            </section>

            {/* Card 3: Consent */}
            <section className="rounded-2xl bg-white border border-slate-200/90 p-6">
              <h2 className="text-sm font-bold text-slate-900">Consent</h2>
              <p className="text-xs text-slate-500 mt-0.5">
                Required. You manage this farmer&apos;s records on their behalf.
              </p>

              <label className="mt-4 flex items-center gap-2.5 cursor-pointer select-none">
                <input
                  type="checkbox"
                  checked={consentConfirmed}
                  onChange={(e) => setConsentConfirmed(e.target.checked)}
                  className="w-4 h-4 rounded border-slate-300 text-agri-700 focus:ring-agri-700 cursor-pointer"
                />
                <span className="text-xs font-semibold text-slate-800">
                  {firstNameDisplay} agreed, in person, that I may manage their records on AgriFlow
                </span>
              </label>

              <div className="mt-4">
                <label className="block text-[11px] font-semibold text-slate-600 mb-1.5">
                  Recorded on
                </label>
                <div className="rounded-lg bg-slate-100/90 border border-slate-200/80 px-3.5 py-2.5 text-xs text-slate-700">
                  {recordedOnLabel}
                </div>
              </div>
            </section>

            {/* Form Footer Buttons */}
            <div className="flex items-center justify-end gap-3 pt-1">
              <button
                type="button"
                onClick={() => navigate('/agent/dashboard')}
                className="rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 font-semibold text-xs px-4 py-2.5 transition cursor-pointer"
              >
                Cancel
              </button>

              <button
                type="submit"
                disabled={submitting}
                className="inline-flex items-center gap-2 rounded-lg bg-agri-700 hover:bg-agri-800 disabled:opacity-60 text-white font-semibold text-xs px-4 py-2.5 transition cursor-pointer"
              >
                <UserPlus className="w-3.5 h-3.5" />
                <span>{submitting ? 'Registering...' : 'Register farmer'}</span>
              </button>
            </div>
          </form>

          {/* Right Info Column (4 cols) */}
          <aside className="lg:col-span-4 space-y-4">
            {/* What happens next card */}
            <div className="rounded-2xl bg-white border border-slate-200/90 p-5">
              <h2 className="text-sm font-bold text-slate-900">What happens next</h2>

              <div className="mt-4 space-y-4">
                <div className="flex items-start gap-3">
                  <div className="w-8 h-8 rounded-lg bg-emerald-50 text-agri-700 flex items-center justify-center shrink-0">
                    <Users className="w-4 h-4" />
                  </div>
                  <div>
                    <p className="text-xs font-semibold text-slate-900">Assigned to you</p>
                    <p className="text-[11px] text-slate-500 mt-0.5 leading-snug">
                      {firstNameDisplay} appears in My farmers straight away.
                    </p>
                  </div>
                </div>

                <div className="flex items-start gap-3">
                  <div className="w-8 h-8 rounded-lg bg-emerald-50 text-agri-700 flex items-center justify-center shrink-0">
                    <Package className="w-4 h-4" />
                  </div>
                  <div>
                    <p className="text-xs font-semibold text-slate-900">You record for them</p>
                    <p className="text-[11px] text-slate-500 mt-0.5 leading-snug">
                      Seed stock, plantings and harvests — all under their name.
                    </p>
                  </div>
                </div>

                <div className="flex items-start gap-3">
                  <div className="w-8 h-8 rounded-lg bg-emerald-50 text-agri-700 flex items-center justify-center shrink-0">
                    <Truck className="w-4 h-4" />
                  </div>
                  <div>
                    <p className="text-xs font-semibold text-slate-900">They get buyers</p>
                    <p className="text-[11px] text-slate-500 mt-0.5 leading-snug">
                      Their harvests reach verified businesses on the marketplace.
                    </p>
                  </div>
                </div>
              </div>
            </div>

            {/* Your access is limited callout */}
            <div className="rounded-2xl bg-blue-50/75 border border-blue-200/80 p-4 flex items-start gap-3">
              <Lock className="w-4 h-4 text-blue-600 shrink-0 mt-0.5" />
              <div>
                <p className="text-xs font-semibold text-blue-950">Your access is limited</p>
                <p className="text-[11px] text-blue-800/85 mt-1 leading-relaxed">
                  You can only act for farmers assigned to you. Every action is logged with both
                  your name and the farmer&apos;s.
                </p>
              </div>
            </div>
          </aside>
        </div>
      </div>

      <FarmerRegisteredModal
        result={registeredResult}
        onRegisterAnother={resetForm}
        onOpenWorkspace={(farmerId) => navigate(`/agent/farmers/${farmerId}/seed-stock`)}
      />
    </AgentLayout>
  );
};
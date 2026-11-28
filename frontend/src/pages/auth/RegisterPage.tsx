import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { isAxiosError } from 'axios';
import {
  Sprout,
  Building2,
  Phone,
  Mail,
  CreditCard,
  Landmark,
  Lock,
  Eye,
  EyeOff,
  MapPin,
  Info,
  ShieldCheck,
} from 'lucide-react';
import { AuthSplitLayout } from '../../components/AuthSplitLayout';
import { authApi } from '../../api/authApi';
import {
  BUSINESS_TYPES,
  RWANDA_CITIES,
  RWANDA_LOCATIONS,
} from '../../data/rwandaLocations';
import type { BusinessType } from '../../types/auth';

type AccountRoleChoice = 'Farmer' | 'Buyer';

function formatNationalIdDisplay(raw: string): string {
  const digits = raw.replace(/\D/g, '').slice(0, 16);
  // Format as: 1 1985 8 0123456 0 31
  const parts = [
    digits.slice(0, 1),
    digits.slice(1, 5),
    digits.slice(5, 6),
    digits.slice(6, 13),
    digits.slice(13, 14),
    digits.slice(14, 16),
  ].filter(Boolean);
  return parts.join(' ');
}

export const RegisterPage: React.FC = () => {
  const navigate = useNavigate();
  const [roleChoice, setRoleChoice] = useState<AccountRoleChoice>('Farmer');
  const [showPassword, setShowPassword] = useState(false);
  const [agreedToTerms, setAgreedToTerms] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const errorRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (error) errorRef.current?.focus();
  }, [error]);

  // Farmer fields
  const [farmerFullName, setFarmerFullName] = useState('');
  const [farmerPhone, setFarmerPhone] = useState('');
  const [farmerEmail, setFarmerEmail] = useState('');
  const [nationalIdFormatted, setNationalIdFormatted] = useState('');
  const [farmName, setFarmName] = useState('');
  const [province, setProvince] = useState('Eastern Province');
  const [district, setDistrict] = useState('Nyagatare');
  const [sector, setSector] = useState('Rukomo');
  const [cell, setCell] = useState('Gahurura');
  const [village, setVillage] = useState('');
  const [landUpi, setLandUpi] = useState('');
  const [farmSizeHa, setFarmSizeHa] = useState('');
  const [mainCrops, setMainCrops] = useState('');
  const [cooperativeInput, setCooperativeInput] = useState('');
  const [farmerPassword, setFarmerPassword] = useState('');

  // Business buyer fields
  const [businessName, setBusinessName] = useState('');
  const [businessType, setBusinessType] = useState<BusinessType>('Supermarket');
  const [rdbCompanyCode, setRdbCompanyCode] = useState('');
  const [city, setCity] = useState('Kigali');
  const [businessAddress, setBusinessAddress] = useState('');
  const [buyerFullName, setBuyerFullName] = useState('');
  const [jobTitle, setJobTitle] = useState('');
  const [buyerEmail, setBuyerEmail] = useState('');
  const [buyerPhone, setBuyerPhone] = useState('');
  const [buyerPassword, setBuyerPassword] = useState('');

  // Cascading Rwanda location options
  const provinces = useMemo(() => Object.keys(RWANDA_LOCATIONS), []);
  const districts = useMemo(
    () => Object.keys(RWANDA_LOCATIONS[province] ?? {}),
    [province]
  );
  const sectors = useMemo(
    () => Object.keys(RWANDA_LOCATIONS[province]?.[district] ?? {}),
    [province, district]
  );
  const cells = useMemo(
    () => RWANDA_LOCATIONS[province]?.[district]?.[sector] ?? [],
    [province, district, sector]
  );

  const handleProvinceChange = (nextProvince: string) => {
    setProvince(nextProvince);
    const nextDistrict = Object.keys(RWANDA_LOCATIONS[nextProvince] ?? {})[0] ?? '';
    setDistrict(nextDistrict);
    const nextSector =
      Object.keys(RWANDA_LOCATIONS[nextProvince]?.[nextDistrict] ?? {})[0] ?? '';
    setSector(nextSector);
    const nextCell =
      RWANDA_LOCATIONS[nextProvince]?.[nextDistrict]?.[nextSector]?.[0] ?? '';
    setCell(nextCell);
  };

  const handleDistrictChange = (nextDistrict: string) => {
    setDistrict(nextDistrict);
    const nextSector =
      Object.keys(RWANDA_LOCATIONS[province]?.[nextDistrict] ?? {})[0] ?? '';
    setSector(nextSector);
    const nextCell =
      RWANDA_LOCATIONS[province]?.[nextDistrict]?.[nextSector]?.[0] ?? '';
    setCell(nextCell);
  };

  const handleSectorChange = (nextSector: string) => {
    setSector(nextSector);
    const nextCell = RWANDA_LOCATIONS[province]?.[district]?.[nextSector]?.[0] ?? '';
    setCell(nextCell);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (roleChoice === 'Farmer' && !agreedToTerms) {
      setError('Please agree to the Terms and Privacy Policy to continue.');
      return;
    }

    setSubmitting(true);
    try {
      if (roleChoice === 'Farmer') {
        const rawDigits = nationalIdFormatted.replace(/\D/g, '');
        if (rawDigits.length !== 16) {
          setError('National ID must be exactly 16 digits.');
          setSubmitting(false);
          return;
        }

        // Split optional "CooperativeName - member 0918" if formatted with separator
        const coopParts = cooperativeInput
          .split(/[-–·]/)
          .map((s) => s.trim())
          .filter(Boolean);

        const res = await authApi.registerFarmer({
          fullName: farmerFullName,
          email: farmerEmail,
          phone: farmerPhone,
          password: farmerPassword,
          farmName,
          nationalId: rawDigits,
          landUpi,
          province,
          district,
          sector,
          cell,
          village,
          mainCrops,
          farmSizeHa: farmSizeHa ? Number(farmSizeHa) : null,
          cooperativeName: coopParts[0] || null,
          cooperativeMemberNo: coopParts[1] || null,
        });

        navigate('/verify-otp', {
          state: { email: res.email, role: 'Farmer', purpose: 'Verification' },
        });
      } else {
        const res = await authApi.registerBuyer({
          fullName: buyerFullName.trim(),
          email: buyerEmail.trim(),
          phone: buyerPhone.trim(),
          password: buyerPassword,
          jobTitle: jobTitle || null,
          businessName: businessName.trim(),
          businessType,
          rdbCompanyCode: rdbCompanyCode.trim(),
          address: businessAddress.trim(),
          city,
        });

        navigate('/verify-otp', {
          state: { email: res.email, role: 'Buyer', purpose: 'Verification' },
        });
      }
    } catch (err: any) {
      if (isAxiosError(err) && err.code === 'ERR_NETWORK') {
        setError('Could not reach the AgriFlow API. Make sure the backend is running and try again.');
        return;
      }
      const apiMsg =
        err.response?.data?.message ||
        Object.values(err.response?.data?.errors ?? {})
          .flat()
          .join(' ') ||
        'Registration failed. Please check your details and try again.';
      setError(apiMsg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthSplitLayout
      headline={
        roleChoice === 'Farmer'
          ? 'Sell your harvest before you harvest it.'
          : 'Reliable bulk supply, direct from verified farms.'
      }
    >
      <div className="py-2">
        <h2 className="text-2xl font-bold text-slate-900 tracking-tight">
          Create your account
        </h2>
        <p className="text-sm text-slate-500 mt-1">
          Already have one?{' '}
          <Link to="/login" className="font-medium text-agri-700 hover:underline">
            Sign in.
          </Link>
        </p>

        {/* Role Selector: I AM A... */}
        <div className="mt-6">
          <span className="block text-[11px] font-semibold uppercase tracking-wider text-agri-700 mb-2.5">
            I AM A...
          </span>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            {/* Farmer Card */}
            <button
              type="button"
              onClick={() => {
                setRoleChoice('Farmer');
                setError(null);
              }}
              className={`flex items-center justify-between p-3.5 rounded-xl border text-left transition ${
                roleChoice === 'Farmer'
                  ? 'border-agri-700 bg-agri-50/70 ring-1 ring-agri-700'
                  : 'border-slate-200 bg-white hover:border-slate-300'
              }`}
            >
              <div className="flex items-center gap-3">
                <div
                  className={`w-9 h-9 rounded-lg flex items-center justify-center ${
                    roleChoice === 'Farmer'
                      ? 'bg-agri-100 text-agri-700'
                      : 'bg-slate-100 text-slate-500'
                  }`}
                >
                  <Sprout className="w-4 h-4" />
                </div>
                <div>
                  <div className="text-sm font-semibold text-slate-900">Farmer</div>
                  <div className="text-xs text-slate-500">I grow and sell produce</div>
                </div>
              </div>
              <span
                className={`w-4 h-4 rounded-full border flex items-center justify-center ${
                  roleChoice === 'Farmer'
                    ? 'border-agri-700 bg-agri-700'
                    : 'border-slate-300'
                }`}
              >
                {roleChoice === 'Farmer' && (
                  <span className="w-1.5 h-1.5 rounded-full bg-white" />
                )}
              </span>
            </button>

            {/* Business Buyer Card */}
            <button
              type="button"
              onClick={() => {
                setRoleChoice('Buyer');
                setError(null);
              }}
              className={`flex items-center justify-between p-3.5 rounded-xl border text-left transition ${
                roleChoice === 'Buyer'
                  ? 'border-agri-700 bg-agri-50/70 ring-1 ring-agri-700'
                  : 'border-slate-200 bg-white hover:border-slate-300'
              }`}
            >
              <div className="flex items-center gap-3">
                <div
                  className={`w-9 h-9 rounded-lg flex items-center justify-center ${
                    roleChoice === 'Buyer'
                      ? 'bg-agri-100 text-agri-700'
                      : 'bg-slate-100 text-slate-500'
                  }`}
                >
                  <Building2 className="w-4 h-4" />
                </div>
                <div>
                  <div className="text-sm font-semibold text-slate-900">
                    Business buyer
                  </div>
                  <div className="text-xs text-slate-500">I buy produce in bulk</div>
                </div>
              </div>
              <span
                className={`w-4 h-4 rounded-full border flex items-center justify-center ${
                  roleChoice === 'Buyer'
                    ? 'border-agri-700 bg-agri-700'
                    : 'border-slate-300'
                }`}
              >
                {roleChoice === 'Buyer' && (
                  <span className="w-1.5 h-1.5 rounded-full bg-white" />
                )}
              </span>
            </button>
          </div>
        </div>

        {error && (
          <div ref={errorRef} tabIndex={-1} role="alert" className="mt-5 p-3 rounded-lg bg-red-50 border border-red-200 text-sm text-red-700">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="mt-6 space-y-5">
          {roleChoice === 'Farmer' ? (
            <>
              {/* YOUR DETAILS */}
              <div>
                <h3 className="text-[11px] font-semibold uppercase tracking-wider text-agri-700 mb-3">
                  Your details
                </h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Full name <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      required
                      placeholder="Jean Bosco Habimana"
                      value={farmerFullName}
                      onChange={(e) => setFarmerFullName(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Phone <span className="text-red-500">*</span>
                    </label>
                    <div className="relative">
                      <Phone className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
                      <input
                        type="tel"
                        required
                        placeholder="078 555 0198"
                        value={farmerPhone}
                        onChange={(e) => setFarmerPhone(e.target.value)}
                        className="w-full rounded-lg border border-slate-200 pl-9 pr-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                      />
                    </div>
                  </div>
                </div>

                <div className="mt-3.5">
                  <label className="block text-xs font-medium text-slate-700 mb-1">
                    Email <span className="text-red-500">*</span>
                  </label>
                  <div className="relative">
                    <Mail className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
                    <input
                      type="email"
                      required
                      placeholder="jeanbosco@greenhills.example"
                      value={farmerEmail}
                      onChange={(e) => setFarmerEmail(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 pl-9 pr-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>
                </div>

                <div className="mt-3.5">
                  <label className="block text-xs font-medium text-slate-700 mb-1">
                    National ID number <span className="text-red-500">*</span>
                  </label>
                  <div className="relative">
                    <CreditCard className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
                    <input
                      type="text"
                      required
                      inputMode="numeric"
                      placeholder="1 1985 8 0123456 0 31"
                      value={nationalIdFormatted}
                      onChange={(e) =>
                        setNationalIdFormatted(formatNationalIdDisplay(e.target.value))
                      }
                      className="w-full rounded-lg border border-slate-200 pl-9 pr-3 py-2 text-sm tracking-wide focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>
                  <p className="text-[11px] text-slate-400 mt-1">
                    16 digits. Used only to verify you — never shown to buyers.
                  </p>
                </div>
              </div>

              {/* YOUR FARM */}
              <div>
                <h3 className="text-[11px] font-semibold uppercase tracking-wider text-agri-700 mb-3">
                  Your farm
                </h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Farm name <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      required
                      placeholder="Green Hills Farm"
                      value={farmName}
                      onChange={(e) => setFarmName(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Province <span className="text-red-500">*</span>
                    </label>
                    <select
                      value={province}
                      onChange={(e) => handleProvinceChange(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    >
                      {provinces.map((p) => (
                        <option key={p} value={p}>
                          {p}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      District <span className="text-red-500">*</span>
                    </label>
                    <select
                      value={district}
                      onChange={(e) => handleDistrictChange(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    >
                      {districts.map((d) => (
                        <option key={d} value={d}>
                          {d}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Sector <span className="text-red-500">*</span>
                    </label>
                    <select
                      value={sector}
                      onChange={(e) => handleSectorChange(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    >
                      {sectors.map((s) => (
                        <option key={s} value={s}>
                          {s}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Cell <span className="text-red-500">*</span>
                    </label>
                    <select
                      value={cell}
                      onChange={(e) => setCell(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    >
                      {cells.map((c) => (
                        <option key={c} value={c}>
                          {c}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Village <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      required
                      placeholder="Ryabega"
                      value={village}
                      onChange={(e) => setVillage(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Land UPI number <span className="text-red-500">*</span>
                    </label>
                    <div className="relative">
                      <Landmark className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
                      <input
                        type="text"
                        required
                        placeholder="3/03/10/04/1187"
                        value={landUpi}
                        onChange={(e) => setLandUpi(e.target.value)}
                        className="w-full rounded-lg border border-slate-200 pl-9 pr-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                      />
                    </div>
                    <p className="text-[11px] text-slate-400 mt-1">
                      From your land title or Smart Nkunganire registration.
                    </p>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Farm size
                    </label>
                    <div className="relative">
                      <input
                        type="number"
                        step="0.01"
                        min="0.01"
                        placeholder="3.5"
                        value={farmSizeHa}
                        onChange={(e) => setFarmSizeHa(e.target.value)}
                        className="w-full rounded-lg border border-slate-200 pl-3 pr-9 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                      />
                      <span className="text-xs text-slate-400 absolute right-3 top-2.5">
                        ha
                      </span>
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Main crops <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      required
                      placeholder="Maize, tomato, beans"
                      value={mainCrops}
                      onChange={(e) => setMainCrops(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Cooperative (optional)
                    </label>
                    <input
                      type="text"
                      placeholder="Twitezimbere · member 0918"
                      value={cooperativeInput}
                      onChange={(e) => setCooperativeInput(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>
                </div>
              </div>

              {/* Password */}
              <div>
                <label className="block text-xs font-medium text-slate-700 mb-1">
                  Password <span className="text-red-500">*</span>
                </label>
                <div className="relative">
                  <Lock className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
                  <input
                    type={showPassword ? 'text' : 'password'}
                    required
                    minLength={8}
                    placeholder="••••••••••"
                    value={farmerPassword}
                    onChange={(e) => setFarmerPassword(e.target.value)}
                    className="w-full rounded-lg border border-slate-200 pl-9 pr-10 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((v) => !v)}
                    className="absolute right-3 top-2.5 text-slate-400 hover:text-slate-600"
                  >
                    {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
                <p className="text-[11px] text-slate-400 mt-1">At least 8 characters.</p>
              </div>

              {/* Terms Checkbox */}
              <label className="flex items-center gap-2.5 cursor-pointer select-none">
                <input
                  type="checkbox"
                  checked={agreedToTerms}
                  onChange={(e) => setAgreedToTerms(e.target.checked)}
                  className="w-4 h-4 rounded border-slate-300 text-agri-700 focus:ring-agri-700 accent-agri-700"
                />
                <span className="text-xs text-slate-600">
                  I agree to the{' '}
                  <span className="font-medium text-slate-800">Terms and Privacy Policy</span>
                </span>
              </label>

              <button
                type="submit"
                disabled={submitting}
                className="w-full rounded-lg bg-agri-700 hover:bg-agri-800 disabled:opacity-60 text-white font-semibold text-sm py-2.5 transition cursor-pointer"
              >
                {submitting ? 'Creating farmer account...' : 'Create farmer account'}
              </button>

              {/* Farmer Verification Info Box */}
              <div className="rounded-xl bg-agri-50 border border-emerald-200/80 p-3.5 flex items-start gap-3">
                <Info className="w-4 h-4 text-agri-700 shrink-0 mt-0.5" />
                <div className="text-xs leading-relaxed">
                  <p className="font-semibold text-slate-800">
                    Your farm is checked before buyers see your listings
                  </p>
                  <p className="text-slate-600 mt-0.5">
                    You can start recording straight away. Farms registered by a farmer promoter or
                    sector agronomist are verified on the spot.
                  </p>
                </div>
              </div>
            </>
          ) : (
            <>
              {/* BUSINESS DETAILS */}
              <div>
                <h3 className="text-[11px] font-semibold uppercase tracking-wider text-agri-700 mb-3">
                  Business details
                </h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Business name <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      required
                      placeholder="FreshMart Supermarkets"
                      maxLength={150}
                      value={businessName}
                      onChange={(e) => setBusinessName(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Business type <span className="text-red-500">*</span>
                    </label>
                    <select
                      value={businessType}
                      onChange={(e) => setBusinessType(e.target.value as BusinessType)}
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    >
                      {BUSINESS_TYPES.map((t) => (
                        <option key={t} value={t}>
                          {t}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      RDB company code <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      required
                      placeholder="108452367"
                      maxLength={20}
                      value={rdbCompanyCode}
                      onChange={(e) => setRdbCompanyCode(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                    <p className="text-[11px] text-slate-400 mt-1">
                      From your RDB certificate.
                    </p>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      City <span className="text-red-500">*</span>
                    </label>
                    <select
                      value={city}
                      onChange={(e) => setCity(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    >
                      {RWANDA_CITIES.map((c) => (
                        <option key={c} value={c}>
                          {c}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                <div className="mt-3.5">
                  <label className="block text-xs font-medium text-slate-700 mb-1">
                    Business address <span className="text-red-500">*</span>
                  </label>
                  <div className="relative">
                    <MapPin className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
                    <input
                      type="text"
                      required
                      placeholder="Kimironko, Kigali"
                      maxLength={250}
                      value={businessAddress}
                      onChange={(e) => setBusinessAddress(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 pl-9 pr-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>
                </div>
              </div>

              {/* YOUR DETAILS */}
              <div>
                <h3 className="text-[11px] font-semibold uppercase tracking-wider text-agri-700 mb-3">
                  Your details
                </h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Full name <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      required
                      placeholder="Aline Uwase"
                      maxLength={150}
                      value={buyerFullName}
                      onChange={(e) => setBuyerFullName(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Role in business
                    </label>
                    <input
                      type="text"
                      placeholder="Procurement officer"
                      maxLength={100}
                      value={jobTitle}
                      onChange={(e) => setJobTitle(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Work email <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="email"
                      required
                      placeholder="aline.uwase@freshmart.example"
                      maxLength={256}
                      value={buyerEmail}
                      onChange={(e) => setBuyerEmail(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-700 mb-1">
                      Phone <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="tel"
                      required
                      placeholder="078 555 0142"
                      maxLength={20}
                      value={buyerPhone}
                      onChange={(e) => setBuyerPhone(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                  </div>
                </div>

                <div className="mt-3.5">
                  <label className="block text-xs font-medium text-slate-700 mb-1">
                    Password <span className="text-red-500">*</span>
                  </label>
                  <div className="relative">
                    <Lock className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
                    <input
                      type={showPassword ? 'text' : 'password'}
                      required
                      minLength={8}
                      placeholder="••••••••••"
                      value={buyerPassword}
                      maxLength={100}
                      onChange={(e) => setBuyerPassword(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 pl-9 pr-10 py-2 text-sm focus:border-agri-700 focus:outline-none focus:ring-1 focus:ring-agri-700"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword((v) => !v)}
                      className="absolute right-3 top-2.5 text-slate-400 hover:text-slate-600"
                    >
                      {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                  </div>
                  <p className="text-[11px] text-slate-400 mt-1">At least 8 characters.</p>
                </div>
              </div>

              {/* Business Verification Info Box */}
              <div className="rounded-xl bg-blue-50/80 border border-blue-200/80 p-3.5 flex items-start gap-3">
                <ShieldCheck className="w-4 h-4 text-blue-600 shrink-0 mt-0.5" />
                <div className="text-xs leading-relaxed">
                  <p className="font-semibold text-slate-800">
                    Business accounts are verified before ordering
                  </p>
                  <p className="text-slate-600 mt-0.5">
                    We check your registration within 1 business day. You can browse the
                    marketplace in the meantime.
                  </p>
                </div>
              </div>

              <button
                type="submit"
                disabled={submitting}
                className="w-full rounded-lg bg-agri-700 hover:bg-agri-800 disabled:opacity-60 text-white font-semibold text-sm py-2.5 transition cursor-pointer"
              >
                {submitting ? 'Creating business account...' : 'Create business account'}
              </button>
            </>
          )}
        </form>
      </div>
    </AuthSplitLayout>
  );
};

export type UserRole = 'Farmer' | 'Buyer' | 'Agent' | 'Admin';

export type VerificationStatus = 'Unverified' | 'Pending' | 'Verified' | 'Rejected';

export type BusinessType =
  | 'Supermarket'
  | 'Hotel'
  | 'Restaurant'
  | 'School'
  | 'Hospital'
  | 'Processor'
  | 'Exporter'
  | 'Other';

export interface RegisterFarmerRequest {
  fullName: string;
  email: string;
  phone: string;
  password: string;
  farmName: string;
  nationalId: string;
  landUpi: string;
  province: string;
  district: string;
  sector: string;
  cell: string;
  village: string;
  mainCrops: string;
  farmSizeHa?: number | null;
  cooperativeName?: string | null;
  cooperativeMemberNo?: string | null;
  preferredLanguage?: string;
}

export interface RegisterBuyerRequest {
  fullName: string;
  email: string;
  phone: string;
  password: string;
  jobTitle?: string | null;
  businessName: string;
  businessType: BusinessType;
  rdbCompanyCode: string;
  address: string;
  city: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RequestOtpRequest {
  email: string;
  purpose?: 'Verification' | 'Login' | 'PasswordReset';
}

export interface VerifyOtpRequest {
  email: string;
  code: string;
  purpose?: 'Verification' | 'Login';
}

export interface AuthResponse {
  token: string;
  userId: number;
  fullName: string;
  email: string;
  role: UserRole;
  verificationStatus: VerificationStatus;
  buyerBusinessId?: number | null;
}

export interface UserProfileResponse {
  id: number;
  fullName: string;
  email: string;
  phone?: string | null;
  role: UserRole;
  isActive: boolean;
  jobTitle?: string | null;
  verificationStatus: VerificationStatus;
  farmerProfile?: {
    farmName: string;
    landUpi: string;
    province: string;
    district: string;
    sector: string;
    cell: string;
    village: string;
    mainCrops: string;
    farmSizeHa?: number | null;
    verificationStatus: VerificationStatus;
    rejectionReason?: string | null;
  } | null;
  buyerBusiness?: {
    id: number;
    name: string;
    businessType: BusinessType;
    rdbCompanyCode: string;
    city: string;
    address: string;
    verificationStatus: VerificationStatus;
    rejectionReason?: string | null;
  } | null;
}
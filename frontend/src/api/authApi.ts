import axiosClient from './axiosClient';
import type {
  AuthResponse,
  LoginRequest,
  RegisterBuyerRequest,
  RegisterFarmerRequest,
  RequestOtpRequest,
  UserProfileResponse,
  VerifyOtpRequest,
} from '../types/auth';

export const authApi = {
  registerFarmer: async (payload: RegisterFarmerRequest) => {
    const { data } = await axiosClient.post<{ message: string; email: string }>(
      '/auth/register/farmer',
      payload
    );
    return data;
  },

  registerBuyer: async (payload: RegisterBuyerRequest) => {
    const { data } = await axiosClient.post<{ message: string; email: string }>(
      '/auth/register/buyer',
      payload
    );
    return data;
  },

  login: async (payload: LoginRequest) => {
    const { data } = await axiosClient.post<AuthResponse>('/auth/login', payload);
    return data;
  },

  requestOtp: async (payload: RequestOtpRequest) => {
    const { data } = await axiosClient.post<{ message: string }>('/auth/request-otp', payload);
    return data;
  },

  verifyOtp: async (payload: VerifyOtpRequest) => {
    const { data } = await axiosClient.post<AuthResponse>('/auth/verify-otp', payload);
    return data;
  },

  getMe: async () => {
    const { data } = await axiosClient.get<UserProfileResponse>('/auth/me');
    return data;
  },
};
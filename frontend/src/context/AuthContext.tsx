import React, { createContext, useContext, useEffect, useState } from 'react';
import { TOKEN_STORAGE_KEY, USER_STORAGE_KEY, getStoredToken } from '../api/axiosClient';
import { authApi } from '../api/authApi';
import type { AuthResponse, UserProfileResponse } from '../types/auth';

interface AuthContextValue {
  user: AuthResponse | null;
  profile: UserProfileResponse | null;
  token: string | null;
  isLoading: boolean;
  setSession: (auth: AuthResponse, rememberMe?: boolean) => void;
  refreshProfile: () => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function readStoredUser(): AuthResponse | null {
  const raw = localStorage.getItem(USER_STORAGE_KEY) || sessionStorage.getItem(USER_STORAGE_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as AuthResponse;
  } catch {
    return null;
  }
}

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [token, setToken] = useState<string | null>(() => getStoredToken());
  const [user, setUser] = useState<AuthResponse | null>(() => readStoredUser());
  const [profile, setProfile] = useState<UserProfileResponse | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(() => Boolean(getStoredToken()));

  const setSession = (auth: AuthResponse, rememberMe = true) => {
    localStorage.removeItem(TOKEN_STORAGE_KEY);
    localStorage.removeItem(USER_STORAGE_KEY);
    sessionStorage.removeItem(TOKEN_STORAGE_KEY);
    sessionStorage.removeItem(USER_STORAGE_KEY);

    const storage = rememberMe ? localStorage : sessionStorage;
    storage.setItem(TOKEN_STORAGE_KEY, auth.token);
    storage.setItem(USER_STORAGE_KEY, JSON.stringify(auth));

    setToken(auth.token);
    setUser(auth);
  };

  const logout = () => {
    localStorage.removeItem(TOKEN_STORAGE_KEY);
    localStorage.removeItem(USER_STORAGE_KEY);
    sessionStorage.removeItem(TOKEN_STORAGE_KEY);
    sessionStorage.removeItem(USER_STORAGE_KEY);
    setToken(null);
    setUser(null);
    setProfile(null);
  };

  const refreshProfile = async () => {
    if (!getStoredToken()) {
      setIsLoading(false);
      return;
    }
    try {
      const me = await authApi.getMe();
      setProfile(me);
      setUser((prev) =>
        prev
          ? {
              ...prev,
              fullName: me.fullName,
              role: me.role,
              verificationStatus: me.verificationStatus,
            }
          : prev
      );
    } catch {
      logout();
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (token) {
      refreshProfile();
    } else {
      setIsLoading(false);
    }
  }, [token]);

  return (
    <AuthContext.Provider
      value={{ user, profile, token, isLoading, setSession, refreshProfile, logout }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside an AuthProvider');
  return ctx;
}
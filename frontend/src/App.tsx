import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { ProtectedRoute } from './components/ProtectedRoute';
import { LoginPage } from './pages/auth/LoginPage';
import { RegisterPage } from './pages/auth/RegisterPage';
import { VerifyOtpPage } from './pages/auth/VerifyOtpPage';
import { PendingVerificationPage } from './pages/auth/PendingVerificationPage';
import { FarmerDashboard } from './pages/farmer/FarmerDashboard';
import { BuyerDashboard } from './pages/buyer/BuyerDashboard';
import { NotFoundPage } from './pages/NotFoundPage';

export function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<Navigate to="/login" replace />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/verify-otp" element={<VerifyOtpPage />} />

          <Route
            path="/pending"
            element={
              <ProtectedRoute>
                <PendingVerificationPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/farmer/dashboard"
            element={
              <ProtectedRoute allowedRoles={['Farmer']}>
                <FarmerDashboard />
              </ProtectedRoute>
            }
          />

          <Route
            path="/buyer/dashboard"
            element={
              <ProtectedRoute allowedRoles={['Buyer']}>
                <BuyerDashboard />
              </ProtectedRoute>
            }
          />

          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}

export default App;
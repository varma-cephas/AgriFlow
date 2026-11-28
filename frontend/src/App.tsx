import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { ProtectedRoute } from './components/ProtectedRoute';
import { LoginPage } from './pages/auth/LoginPage';
import { RegisterPage } from './pages/auth/RegisterPage';
import { VerifyOtpPage } from './pages/auth/VerifyOtpPage';
import { PendingVerificationPage } from './pages/auth/PendingVerificationPage';
import { FarmerDashboard } from './pages/farmer/FarmerDashboard';
import { BuyerDashboard } from './pages/buyer/BuyerDashboard';
import { AgentDashboard } from './pages/agent/AgentDashboard';
import { NotFoundPage } from './pages/NotFoundPage';
import { AgentFarmerSeedStockPage } from './pages/agent/AgentFarmerSeedStockPage';
import { AgentFarmVisitsPage } from './pages/agent/AgentVisitsFarmPage';
import { AgentRegisterFarmerPage } from './pages/agent/AgentRegisterFarmerPage';
import { AgentAuditLogPage } from './pages/agent/AgentAuditLogPage';

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

          <Route
            path="/agent/dashboard"
            element={
              <ProtectedRoute allowedRoles={['Agent']}>
                <AgentDashboard />
              </ProtectedRoute>
            }
          />

          <Route
            path="/agent/farmers/:farmerId/seed-stock"
            element={
              <ProtectedRoute allowedRoles={['Agent']}>
                <AgentFarmerSeedStockPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/agent/farm-visits"
            element={
              <ProtectedRoute allowedRoles={['Agent']}>
                <AgentFarmVisitsPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/agent/register-farmer"
            element={
              <ProtectedRoute allowedRoles={['Agent']}>
                <AgentRegisterFarmerPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/agent/audit-log"
            element={
              <ProtectedRoute allowedRoles={['Agent']}>
                <AgentAuditLogPage />
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
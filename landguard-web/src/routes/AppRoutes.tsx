import { Navigate, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from '../components/ProtectedRoute';
import { FullScreenLoader } from '../components/FullScreenLoader';
import { useAuth } from '../hooks/useAuth';
import { DASHBOARD_PATH_BY_ROLE } from '../types/auth';
import LoginPage from '../pages/auth/LoginPage';
import SellerDashboard from '../pages/seller/SellerDashboard';
import BuyerDashboard from '../pages/buyer/BuyerDashboard';
import AdminDashboard from '../pages/admin/AdminDashboard';
import SellerPropertiesPage from '../pages/seller/properties/SellerPropertiesPage';
import PropertyFormPage from '../pages/seller/properties/PropertyFormPage';
import SellerPropertyDetailsPage from '../pages/seller/properties/SellerPropertyDetailsPage';
import BrowsePropertiesPage from '../pages/buyer/properties/BrowsePropertiesPage';
import BuyerPropertyDetailsPage from '../pages/buyer/properties/BuyerPropertyDetailsPage';
import AdminPropertiesPage from '../pages/admin/properties/AdminPropertiesPage';
import AdminPropertyDetailsPage from '../pages/admin/properties/AdminPropertyDetailsPage';

/**
 * "/" itself: authenticated -> that user's own dashboard (via the single
 * DASHBOARD_PATH_BY_ROLE map - never a role === 'X' chain repeated per
 * file), unauthenticated -> /login. A tiny component of its own rather
 * than inlined in <Route element={...}>, purely so it can call useAuth()
 * the normal way.
 */
function RootRedirect() {
  const { isAuthenticated, isInitializing, user } = useAuth();

  if (isInitializing) {
    return <FullScreenLoader />;
  }

  if (isAuthenticated && user) {
    return <Navigate to={DASHBOARD_PATH_BY_ROLE[user.role]} replace />;
  }

  return <Navigate to="/login" replace />;
}

/**
 * The application's full route table. Every dashboard route is wrapped in
 * ProtectedRoute with its own allowedRoles - a Buyer can never even
 * briefly render <SellerDashboard>, since ProtectedRoute checks
 * authentication and role before this component tree mounts its children
 * at all. Unknown paths fall back to "/", which then resolves correctly
 * for both authenticated and unauthenticated visitors.
 */
export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<RootRedirect />} />
      <Route path="/login" element={<LoginPage />} />

      <Route
        path="/seller/dashboard"
        element={
          <ProtectedRoute allowedRoles={['Seller']}>
            <SellerDashboard />
          </ProtectedRoute>
        }
      />
      <Route
        path="/seller/properties"
        element={
          <ProtectedRoute allowedRoles={['Seller']}>
            <SellerPropertiesPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/seller/properties/new"
        element={
          <ProtectedRoute allowedRoles={['Seller']}>
            <PropertyFormPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/seller/properties/:id/edit"
        element={
          <ProtectedRoute allowedRoles={['Seller']}>
            <PropertyFormPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/seller/properties/:id"
        element={
          <ProtectedRoute allowedRoles={['Seller']}>
            <SellerPropertyDetailsPage />
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
        path="/buyer/properties"
        element={
          <ProtectedRoute allowedRoles={['Buyer']}>
            <BrowsePropertiesPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/buyer/properties/:id"
        element={
          <ProtectedRoute allowedRoles={['Buyer']}>
            <BuyerPropertyDetailsPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/dashboard"
        element={
          <ProtectedRoute allowedRoles={['Admin']}>
            <AdminDashboard />
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/properties"
        element={
          <ProtectedRoute allowedRoles={['Admin']}>
            <AdminPropertiesPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/properties/:id"
        element={
          <ProtectedRoute allowedRoles={['Admin']}>
            <AdminPropertyDetailsPage />
          </ProtectedRoute>
        }
      />

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

import { Navigate, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from '../components/ProtectedRoute';
import LoginPage from '../pages/auth/LoginPage';
import RegisterPage from '../pages/auth/RegisterPage';
import HomePage from '../pages/home/HomePage';
import PublicBrowsePropertiesPage from '../pages/properties/PublicBrowsePropertiesPage';
import PublicPropertyDetailsPage from '../pages/properties/PublicPropertyDetailsPage';
import SellerDashboard from '../pages/seller/SellerDashboard';
import BuyerDashboard from '../pages/buyer/BuyerDashboard';
import AdminDashboard from '../pages/admin/AdminDashboard';
import SellerPropertiesPage from '../pages/seller/properties/SellerPropertiesPage';
import PropertyFormPage from '../pages/seller/properties/PropertyFormPage';
import SellerPropertyDetailsPage from '../pages/seller/properties/SellerPropertyDetailsPage';
import BrowsePropertiesPage from '../pages/buyer/properties/BrowsePropertiesPage';
import BuyerPropertyDetailsPage from '../pages/buyer/properties/BuyerPropertyDetailsPage';
import AdminPropertiesPage from '../pages/admin/properties/AdminPropertiesPage';
import AdminPropertyReviewPage from '../pages/admin/properties/AdminPropertyReviewPage';
import AdminPropertyDetailsPage from '../pages/admin/properties/AdminPropertyDetailsPage';

/**
 * The application's full route table. "/" always renders the public
 * HomePage, for both authenticated and unauthenticated visitors - there is
 * deliberately no redirect-to-dashboard here anymore (previous behavior,
 * removed per explicit product decision: a signed-in user clicking "Home"
 * or landing on "/" should see the real homepage, not be bounced away from
 * it). Getting to a role's dashboard from here is now the role-aware
 * PublicNavbar's "Dashboard" button (see components/home/PublicNavbar),
 * which reads the same DASHBOARD_PATH_BY_ROLE map LoginPage's own
 * post-login redirect already uses.
 *
 * "/properties" and "/properties/:id" are new, additive, fully public
 * routes (PublicBrowsePropertiesPage / PublicPropertyDetailsPage) backed by
 * the same AllowAnonymous GET /api/properties[/:id] endpoints the
 * protected Buyer routes below already use - they do not replace or
 * weaken "/buyer/properties"/"/buyer/properties/:id", which remain
 * ProtectedRoute-gated to an authenticated Buyer exactly as before.
 *
 * Every dashboard route is wrapped in ProtectedRoute with its own
 * allowedRoles - a Buyer can never even briefly render <SellerDashboard>,
 * since ProtectedRoute checks authentication and role before this
 * component tree mounts its children at all. Unknown paths fall back to
 * "/", which now resolves to the real homepage for every visitor instead
 * of chaining through another redirect.
 */
export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/properties" element={<PublicBrowsePropertiesPage />} />
      <Route path="/properties/:id" element={<PublicPropertyDetailsPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

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
        path="/admin/properties/review"
        element={
          <ProtectedRoute allowedRoles={['Admin']}>
            <AdminPropertyReviewPage />
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

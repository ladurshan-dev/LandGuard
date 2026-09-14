import { useAuth } from '../../hooks/useAuth';

/**
 * Resolves where a "List Your Property" call-to-action (Hero, closing CTA)
 * should send the visitor - shared by every homepage section that offers
 * this action so the branching logic exists in exactly one place:
 *
 * - Not signed in: into the existing authentication flow (/login - which
 *   itself links to /register for a brand-new account), never straight to
 *   the protected Seller property form.
 * - Signed in as Seller: the real, existing "New Property" form
 *   (/seller/properties/new) - reused as-is, nothing duplicated here.
 * - Signed in as Buyer or Admin: `null`. Neither role can list a property,
 *   so callers must not render this action at all for them rather than
 *   sending it somewhere that 403s or is otherwise misleading.
 */
export function useListPropertyRoute(): string | null {
  const { isAuthenticated, user } = useAuth();

  if (!isAuthenticated || !user) {
    return '/login';
  }

  return user.role === 'Seller' ? '/seller/properties/new' : null;
}

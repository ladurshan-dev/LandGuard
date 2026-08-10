import { apiClient } from '../api/axios';
import { toApiError } from '../utils/apiError';
import type { PropertyListingResult } from '../types/property';
import type { ApprovePropertyRequest, PropertyReviewQueueItem, RejectPropertyRequest } from '../types/admin';

/**
 * The data-access layer for Admin Property Moderation - HTTP calls only,
 * the same boundary propertyService.ts/authService.ts already established.
 * Every endpoint here is one AdminController already exposes (Phase B2 +
 * this phase's review queue addition); AdminUserId is never accepted as a
 * parameter anywhere in this file - the backend always takes it from the
 * caller's JWT, never from the request body, exactly like sellerId in
 * propertyService.createProperty.
 */

/**
 * GET /api/admin/properties/review (RequireAdmin policy). Every property
 * genuinely awaiting manual attention - normally Status = Pending since
 * Phase C, plus any legacy Flagged rows or anything with an open
 * suspicious report.
 */
export async function getPropertyReviewQueue(): Promise<PropertyReviewQueueItem[]> {
  try {
    const response = await apiClient.get<PropertyReviewQueueItem[]>('/admin/properties/review');

    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}

/**
 * POST /api/admin/properties/{id}/approve (RequireAdmin policy). Calls
 * usp_Admin_ApproveProperty, which sets Property.Status = 'Approved',
 * writes the AdminAction history row and the seller Notification itself -
 * none of that is re-created here. Returns the refreshed listing.
 */
export async function approveProperty(
  propertyId: number,
  request: ApprovePropertyRequest = {},
): Promise<PropertyListingResult> {
  try {
    const response = await apiClient.post<PropertyListingResult>(
      `/admin/properties/${propertyId}/approve`,
      request,
    );

    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}

/**
 * POST /api/admin/properties/{id}/reject (RequireAdmin policy). Calls
 * usp_Admin_RejectProperty, which sets Property.Status = 'Rejected',
 * resolves open suspicious reports, writes the AdminAction history row and
 * the seller Notification itself - none of that is re-created here.
 * Returns the refreshed listing.
 */
export async function rejectProperty(
  propertyId: number,
  request: RejectPropertyRequest,
): Promise<PropertyListingResult> {
  try {
    const response = await apiClient.post<PropertyListingResult>(
      `/admin/properties/${propertyId}/reject`,
      request,
    );

    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}

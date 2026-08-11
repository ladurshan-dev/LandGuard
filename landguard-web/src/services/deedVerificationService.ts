import { apiClient } from '../api/axios';
import { toApiError } from '../utils/apiError';
import type { DeedVerificationResponse } from '../types/deedVerification';

/**
 * The data-access layer for Government Deed Verification - HTTP calls
 * only, the same boundary every other service.ts file in this project
 * uses. Two endpoints, both on DeedVerificationController
 * (RequireSellerOrAdmin policy - a Seller may only act on their own
 * property, an Admin may act on any, both enforced entirely server-side):
 * POST runs a brand-new verification against a freshly uploaded deed
 * (Phase 5C, unmodified here), GET reads back every already-persisted
 * verification run without running a new one (Phase D - the read
 * counterpart this project previously lacked).
 */

/**
 * POST /api/deed-verification/{propertyId} (RequireSellerOrAdmin policy -
 * a Seller may only verify their own property, an Admin may verify any,
 * both enforced entirely server-side). Runs the full comparison ->
 * classification -> persistence pipeline against the uploaded deed file
 * and returns the persisted verdict.
 */
export async function verifyDeed(propertyId: number, file: File): Promise<DeedVerificationResponse> {
  const formData = new FormData();
  formData.append('File', file);

  try {
    const response = await apiClient.post<DeedVerificationResponse>(
      `/deed-verification/${propertyId}`,
      formData,
      { headers: { 'Content-Type': undefined } },
    );

    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}

/**
 * GET /api/deed-verification/{propertyId} (RequireSellerOrAdmin policy -
 * same ownership rule as verifyDeed above). Every past verification run
 * for the property, newest first - empty when the seller hasn't uploaded
 * and verified a deed yet, which is a normal, expected state, not an
 * error.
 */
export async function getDeedVerificationHistory(propertyId: number): Promise<DeedVerificationResponse[]> {
  try {
    const response = await apiClient.get<DeedVerificationResponse[]>(`/deed-verification/${propertyId}`);

    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}

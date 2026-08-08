import { apiClient } from '../api/axios';
import { toApiError } from '../utils/apiError';
import type {
  CreatePropertyRequest,
  PropertyDetail,
  PropertyListingResult,
  PropertySearchRequest,
  PropertySearchResponse,
  UpdatePropertyRequest,
  UploadPropertyImageRequest,
} from '../types/property';

/**
 * The data-access layer for Property Management - HTTP calls only, same
 * boundary authService.ts already established (no React, no component
 * state, every thrown error normalized to the shared ApiError via
 * toApiError). Every endpoint here is one PropertyController already
 * exposes; nothing is invented, and sellerId is never accepted as a
 * parameter to createProperty - the backend always takes it from the
 * caller's JWT, never from the request body.
 */

/**
 * GET /api/properties (AllowAnonymous - visibility is enforced server-side
 * by usp_Property_Search itself, which only ever returns Approved rows to
 * this endpoint). Sent as a query string, matching the controller's
 * [FromQuery] binding - axios's default params serializer already omits
 * any key whose value is undefined, so callers can pass a sparse filter
 * object as-is without building the query string by hand.
 */
export async function searchProperties(
  request: PropertySearchRequest = {},
): Promise<PropertySearchResponse> {
  try {
    const response = await apiClient.get<PropertySearchResponse>('/properties', {
      params: request,
    });

    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}

/**
 * GET /api/properties/{id} (AllowAnonymous - PropertyService enforces
 * visibility itself: Approved listings are public, anything else 404s for
 * everyone except the owner or an Admin, indistinguishable from a
 * nonexistent id).
 */
export async function getPropertyById(propertyId: number): Promise<PropertyDetail> {
  try {
    const response = await apiClient.get<PropertyDetail>(`/properties/${propertyId}`);

    return response.data;
  } catch (error) {
    throw toApiError(error, { statusMessages: { 404: 'Property not found.' } });
  }
}

/**
 * GET /api/properties/seller/{sellerId} ([Authorize] - PropertyService
 * requires sellerId to match the caller's own id unless the caller is an
 * Admin, enforced entirely server-side).
 */
export async function getPropertiesBySeller(sellerId: number): Promise<PropertyListingResult[]> {
  try {
    const response = await apiClient.get<PropertyListingResult[]>(`/properties/seller/${sellerId}`);

    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}

/**
 * POST /api/properties (RequireSeller policy). No sellerId parameter here
 * on purpose - the backend takes the owner from the caller's JWT
 * (ICurrentUserService), so accepting one from the frontend would be
 * meaningless at best and misleading at worst.
 */
export async function createProperty(
  request: CreatePropertyRequest,
): Promise<PropertyListingResult> {
  try {
    const response = await apiClient.post<PropertyListingResult>('/properties', request);

    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}

/**
 * PUT /api/properties/{id} (RequireSeller policy - ownership is enforced
 * by usp_Property_Update itself, which raises a SqlException, surfaced by
 * ExceptionHandlingMiddleware as a 400, on a sellerId mismatch).
 */
export async function updateProperty(
  propertyId: number,
  request: UpdatePropertyRequest,
): Promise<PropertyListingResult> {
  try {
    const response = await apiClient.put<PropertyListingResult>(`/properties/${propertyId}`, request);

    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}

/**
 * DELETE /api/properties/{id} ([Authorize] - owner-or-Admin is enforced by
 * usp_Property_Delete itself). 204 No Content on success, nothing to
 * return.
 */
export async function deleteProperty(propertyId: number): Promise<void> {
  try {
    await apiClient.delete(`/properties/${propertyId}`);
  } catch (error) {
    throw toApiError(error);
  }
}

/**
 * POST /api/properties/{id}/images ([Authorize], [Consumes(
 * "multipart/form-data")], 6 MB request-size limit - PropertyService
 * separately rejects anything over 5 MB or outside
 * image/jpeg|png|webp). Ownership (owner-or-Admin) is enforced inside
 * PropertyService itself, not by the database, so a non-owner still gets
 * a clean 403-style failure message back through toApiError rather than a
 * raw SQL error.
 *
 * apiClient's default Content-Type header is "application/json". Axios's
 * transformRequest only sends a FormData body untouched when it does NOT
 * see a JSON content type on the request - otherwise it JSON.stringifies
 * the FormData object itself, silently breaking the upload. Overriding
 * Content-Type to `undefined` on this one call (verified against the
 * installed axios package before writing this) removes the JSON header
 * for this request only, so axios forwards the FormData as-is and the
 * browser sets its own multipart boundary.
 */
export async function uploadPropertyImage(
  propertyId: number,
  request: UploadPropertyImageRequest,
): Promise<PropertyDetail> {
  const formData = new FormData();
  formData.append('File', request.file);
  formData.append('IsPrimary', String(request.isPrimary));

  try {
    const response = await apiClient.post<PropertyDetail>(
      `/properties/${propertyId}/images`,
      formData,
      { headers: { 'Content-Type': undefined } },
    );

    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}

/**
 * DELETE /api/properties/{propertyId}/images/{imageId} ([Authorize] -
 * owner-or-Admin is enforced inside PropertyService, the same split
 * AddImage/uploadPropertyImage already uses for this sub-resource, not by
 * the database). Returns the refreshed PropertyDetail (images, primary
 * flag and fraud report all reflect the deletion) so callers can replace
 * their displayed gallery in one round trip, exactly like
 * uploadPropertyImage already does.
 */
export async function deletePropertyImage(propertyId: number, imageId: number): Promise<PropertyDetail> {
  try {
    const response = await apiClient.delete<PropertyDetail>(`/properties/${propertyId}/images/${imageId}`);

    return response.data;
  } catch (error) {
    throw toApiError(error, { statusMessages: { 404: 'Image not found.' } });
  }
}

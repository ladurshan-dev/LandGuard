/**
 * Types for Admin Property Moderation - direct, field-for-field mirrors of
 * the real backend shapes, the same convention types/property.ts and
 * types/auth.ts already established (nothing invented, nothing "nice to
 * have" added beyond what the backend actually returns).
 */

import type { FraudStatus, PropertyStatus, RiskLevel } from './property';

/**
 * Mirrors LandGuard.Domain.ReadModels.FlaggedProperty exactly - one row of
 * GET /api/admin/properties/review's response body
 * (dbo.vw_FlaggedProperty, read via AdminModerationService.GetReviewQueueAsync).
 * Despite the "Flagged" name (unchanged on the backend, since it's the
 * view's own established name), this includes every Status = 'Pending'
 * property too - the normal review state since Phase C - alongside any
 * legacy Flagged rows and anything with an open suspicious report.
 */
export interface PropertyReviewQueueItem {
  propertyId: number;
  title: string;
  location: string;
  district: string | null;
  price: number;
  size: number;
  deedReference: string | null;
  status: PropertyStatus;
  /** ISO 8601 timestamp. */
  uploadDate: string;
  sellerId: number;
  sellerName: string;
  sellerNicVerified: boolean;
  /** Supporting risk indicator only - see PropertyFraudPanel. Not an approval/rejection verdict. */
  riskScore: number | null;
  riskLevel: RiskLevel;
  fraudStatus: FraudStatus;
  riskSummary: string | null;
  /** Total suspicious reports ever filed against this property. */
  reportCount: number;
  /** Reports still Open or Under Review. */
  openReportCount: number;
  /** Days since uploadDate. */
  daysWaiting: number;
}

/**
 * Mirrors DTOs.Admin.ApprovePropertyRequest exactly - POST
 * /api/admin/properties/{id}/approve's request body. Whole body is
 * optional on the backend; remarks alone is optional too.
 */
export interface ApprovePropertyRequest {
  remarks?: string;
}

/**
 * Mirrors DTOs.Admin.RejectPropertyRequest exactly - POST
 * /api/admin/properties/{id}/reject's request body. Unlike the approve
 * request, reason is required - enforced by RejectPropertyRequestValidator
 * on the backend (an API-layer requirement, not the stored procedure's
 * own, which accepts a null @Remarks).
 */
export interface RejectPropertyRequest {
  reason: string;
}

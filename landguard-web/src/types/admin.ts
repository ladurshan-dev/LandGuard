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

/**
 * Types for the Admin Fraud Dashboard (GET /api/admin/dashboard) - direct
 * mirrors of LandGuard.Application.DTOs.Admin.AdminFraudDashboardResponse
 * and its nested sections, the same "nothing invented beyond what the
 * backend actually returns" convention every type in this file already
 * follows.
 *
 * Risk vs deed verification are deliberately separate sections below and
 * must never be combined in the UI - High Risk does not mean Fraudulent,
 * Low Risk does not mean Verified. See AdminFraudDashboardResponse's own
 * doc comment.
 */
export interface AdminDashboardUserStatistics {
  totalBuyers: number;
  totalSellers: number;
  verifiedSellers: number;
  suspendedUsers: number;
}

/** Current Property.Status counts (live, not the legacy vw_FraudStatistics figures - see the backend DTO's own doc comment). */
export interface AdminDashboardPropertyStatistics {
  total: number;
  approved: number;
  pending: number;
  flagged: number;
  rejected: number;
  withdrawn: number;
  disapproved: number;
}

/** Counts only the LATEST DeedVerification run per property - never raw historical-row counts. */
export interface AdminDashboardDeedVerificationStatistics {
  total: number;
  verified: number;
  formMismatch: number;
  fraudulent: number;
  priceAnomaly: number;
  duplicateProperty: number;
  unverified: number;
  unverifiedCancelled: number;
}

/** Legacy numeric fraud/risk-engine figures (dbo.vw_FraudStatistics) - a supporting indicator, independent of deed verification. */
export interface AdminDashboardRiskStatistics {
  low: number;
  medium: number;
  high: number;
  /** Null if no listing has been analysed yet. */
  averageRiskScore: number | null;
}

/** One row of the Fraud Rule Trigger Frequency table (dbo.vw_RuleTriggerFrequency) - nothing recalculated client-side. */
export interface AdminDashboardRuleTriggerItem {
  ruleCode: string;
  ruleName: string;
  weight: number;
  timesTriggered: number;
  timesEvaluated: number;
  /** Null if timesEvaluated is 0. */
  triggerRatePercent: number | null;
}

/**
 * One row of the "Requires Attention" table - a deliberately minimal
 * projection (top 10, RiskScore DESC then UploadDate ASC). Privacy:
 * carries no Seller NIC, Owner NIC, deed reference, OCR text, Government
 * Registry values, or contact information - only these seven fields.
 */
export interface AdminDashboardReviewPropertySummary {
  propertyId: number;
  title: string;
  status: PropertyStatus;
  riskScore: number | null;
  riskLevel: RiskLevel;
  daysWaiting: number;
  openReportCount: number;
}

/** GET /api/admin/dashboard's whole response body. */
export interface AdminFraudDashboardResponse {
  users: AdminDashboardUserStatistics;
  properties: AdminDashboardPropertyStatistics;
  deedVerification: AdminDashboardDeedVerificationStatistics;
  risk: AdminDashboardRiskStatistics;
  ruleTriggers: AdminDashboardRuleTriggerItem[];
  reviewProperties: AdminDashboardReviewPropertySummary[];
}

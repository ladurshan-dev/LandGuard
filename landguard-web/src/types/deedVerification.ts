/**
 * Types for Government Deed Verification - direct, field-for-field mirrors
 * of LandGuard.API.Models.DeedVerificationResponse and
 * LandGuard.Application.DTOs.DeedComparison.DeedFieldComparisonResult
 * (confirmed by reading DeedVerificationController/DeedVerificationResponse
 * directly), the same convention every other types/*.ts file in this
 * project follows.
 */

/** "Verified" | "Fraudulent" | "PriceAnomaly" | "Unverified" | "UnverifiedCancelled" - DeedVerificationStatus's exact string name. This is authoritative deed-authenticity evidence, kept separate from the legacy RiskScore/RiskLevel/FraudStatus supporting indicators. */
export type DeedVerificationStatus = 'Verified' | 'Fraudulent' | 'PriceAnomaly' | 'Unverified' | 'UnverifiedCancelled';

/** "Active" | "Cancelled" | "Suspended" | null. */
export type GovernmentRecordStatus = 'Active' | 'Cancelled' | 'Suspended';

/** Mirrors API.Models.DeedVerificationReasonEntry exactly. */
export interface DeedVerificationReasonEntry {
  /** DeedFraudReason's exact string name, e.g. "NicMismatch". */
  reason: string;
  description: string;
}

/** Mirrors Application.DTOs.DeedComparison.DeedFieldComparisonResult exactly - one compared field's outcome. */
export interface DeedFieldComparisonResult {
  /** e.g. "NIC", "DeedNumber", "LandSize", "Price". */
  fieldName: string;
  governmentValue: string | null;
  sellerValue: string | null;
  match: boolean;
  message: string;
}

/** Mirrors API.Models.DeedVerificationResponse exactly - POST /api/deed-verification/{propertyId}'s response body. */
export interface DeedVerificationResponse {
  /** The newly-created DeedVerification row's id. */
  deedVerificationId: number;
  propertyId: number;
  verificationStatus: DeedVerificationStatus;
  governmentRecordId: string | null;
  governmentRecordStatus: GovernmentRecordStatus | null;
  /** The authoritative, already-composed explanation - never re-derived on the frontend. */
  summary: string;
  reasons: DeedVerificationReasonEntry[];
  evidence: DeedFieldComparisonResult[];
  /** ISO 8601 timestamp. */
  generatedDate: string;
  /**
   * The seller's uploaded deed document's storage reference (Phase D) - a
   * storage key, never a raw filesystem path (see the backend's
   * StoredDocumentFile.StorageReference doc comment). No document-download
   * endpoint exists yet, so this is only used to confirm a deed was
   * uploaded, not to fetch/display the file itself.
   */
  sellerDocumentReference: string | null;
}

/**
 * Types for Property Management - direct, field-for-field mirrors of the
 * real backend DTOs (LandGuard.Application.Common.Models /
 * LandGuard.Application.DTOs.Property / LandGuard.API.Models), confirmed
 * by inspecting PropertyController, PropertyService and every DTO/model
 * file directly. Nothing here is invented, no field the backend doesn't
 * actually return is added "for convenience", and CreatePropertyRequest
 * deliberately has no sellerId field - the backend always takes the
 * owner from the caller's JWT, never from the request body.
 *
 * Response DTOs use `T | null` for every C# `T?` property (this backend's
 * JSON serialization always sends the key with a null value, never omits
 * it - the same convention types/auth.ts already established). Request
 * DTOs use TypeScript's `?:` optional instead, since omitting a key from
 * an outgoing request body/query string is exactly equivalent to sending
 * it as C#'s null/default on this backend's model binding.
 */

/**
 * dbo.Property.Status, set by usp_Property_Create/usp_Property_Update and
 * the fraud engine, and by usp_Admin_ApproveProperty/
 * usp_Admin_RejectProperty (POST /api/admin/properties/{id}/approve|reject)
 * for Approved/Rejected. 'Withdrawn' (Phase F, Property Withdrawal / Soft
 * Delete) is set only by usp_Property_Withdraw
 * (POST /api/properties/{id}/withdraw) - a Seller voluntarily removing
 * their own listing from active review/browsing. It is a listing lifecycle
 * state, not a fraud verdict: DeedVerification/FraudCheck/RiskReport
 * history stays attached and valid, and the property row is never deleted.
 * Not reachable through the normal edit flow (usp_Property_Update refuses
 * to touch a Withdrawn property) - there is no "Relist" action yet.
 */
/**
 * 'Disapproved' is a SYSTEM-AUTOMATED outcome (Mandatory Deed / Form-vs-
 * Deed Verification requirement) - the seller's own form data didn't match
 * their uploaded deed, or the uploaded deed didn't match the Government
 * Registry. Distinct from 'Rejected', which stays a manual Admin decision -
 * see LandGuard.Domain.Enums.PropertyStatus's own doc comment.
 */
export type PropertyStatus = 'Pending' | 'Approved' | 'Flagged' | 'Rejected' | 'Withdrawn' | 'Disapproved';

/** dbo.RiskReport.RiskLevel banding - "Low" until the fraud engine has run at least once. */
export type RiskLevel = 'Low' | 'Medium' | 'High';

/** dbo.FraudCheck.FraudStatus - "Clean" until the fraud engine has run at least once. */
export type FraudStatus = 'Clean' | 'Suspicious' | 'Fraudulent';

/** usp_Property_Search's @SortBy parameter - see PropertyValidationRules.ValidSortOptions on the backend. */
export type PropertySortOption = 'Newest' | 'Oldest' | 'PriceAsc' | 'PriceDesc' | 'RiskAsc';

/**
 * Mirrors Common.Models.PropertyListingResult exactly - returned by
 * Create/Update/GetBySeller, and as PropertyDetail.listing from GetById.
 */
export interface PropertyListingResult {
  propertyId: number;
  title: string;
  description: string | null;
  location: string;
  district: string | null;
  latitude: number | null;
  longitude: number | null;
  /** Land size in perches. */
  size: number;
  /** Asking price in LKR. */
  price: number;
  pricePerPerch: number | null;
  /**
   * A real legal-document identifier - null whenever the caller is neither
   * this listing's owner nor an Admin (Buyer-privacy fix: previously sent
   * unredacted to every caller, including a Buyer with no legitimate need
   * to see it - see PropertyService.RedactOwnerFields on the backend, the
   * one place this is actually enforced, alongside ownerName/ownerNic/
   * ownerAddress below).
   */
  deedReference: string | null;
  /**
   * The deed's registered owner name (Owner Name / Owner NIC / Owner
   * Address requirement - explicit deed-owner data, no longer substituted
   * with the Seller account's own name). Null whenever the caller is
   * neither this listing's owner nor an Admin - see ownerNic's doc
   * comment.
   */
  ownerName: string | null;
  /**
   * The deed's registered owner NIC. Sensitive PII: null for a
   * Buyer/anonymous/public caller viewing someone else's Approved listing,
   * exactly like riskScore - see PropertyService.RedactOwnerFields on the
   * backend, the one place this is actually enforced. Non-null only for
   * the owning Seller or an Admin.
   */
  ownerNic: string | null;
  /** The deed's registered owner address. Null for a Buyer/public caller - see ownerNic's doc comment. */
  ownerAddress: string | null;
  status: PropertyStatus;
  /** ISO 8601 timestamp. */
  uploadDate: string;
  sellerId: number;
  sellerName: string;
  /**
   * Contact Seller workflow: null whenever the caller is neither this
   * listing's owner nor an Admin - a Buyer no longer receives the Seller's
   * phone number as part of the general property read (previously sent
   * unredacted to every caller). The only way for a Buyer to obtain it is
   * the dedicated `getSellerContact` endpoint, gated to an Approved
   * property, requested explicitly via the "Contact Seller" action - see
   * PropertyService.RedactSellerContactFields on the backend.
   */
  sellerPhone: string | null;
  /** Safe to show a Buyer before they request contact - a simple "Verified Seller" badge, not the NIC itself. */
  sellerNicVerified: boolean;
  /**
   * Buyer privacy requirement: null whenever the caller is neither this
   * listing's owner nor an Admin (i.e. a Buyer, or anonymous, viewing
   * someone else's Approved listing) - internal fraud-engine output must
   * never reach a Buyer, even for an Approved listing. Non-null for the
   * owning Seller or an Admin. See PropertyService.SearchAsync/
   * GetByIdAsync's redaction logic on the backend.
   */
  riskScore: number | null;
  /** Null for a Buyer/public caller - see riskScore's doc comment. */
  riskLevel: RiskLevel | null;
  /** Null for a Buyer/public caller - see riskScore's doc comment. */
  fraudStatus: FraudStatus | null;
  riskSummary: string | null;
  /** ISO 8601 timestamp, null until the fraud engine has run at least once, or when redacted for a Buyer/public caller - see riskScore's doc comment. */
  riskGeneratedDate: string | null;
  coverImageUrl: string | null;
  imageCount: number;
  reportCount: number;
}

/**
 * Mirrors Common.Models.PropertySearchResult exactly - the same fields as
 * PropertyListingResult (usp_Property_Search reads the same published-
 * listing view) plus totalRecords, which the backend repeats on every row
 * so a paged response can read the grand total off any one row.
 */
export interface PropertySearchResult extends PropertyListingResult {
  /** Total rows matching the filter, ignoring paging - identical on every row of one response. */
  totalRecords: number;
}

/** Mirrors Common.Models.PropertyImageSummary exactly - one row of usp_Property_GetById's second result set. */
export interface PropertyImageSummary {
  imageId: number;
  imageUrl: string;
  imageHash: string | null;
  isPrimary: boolean;
  /** ISO 8601 timestamp. */
  uploadedDate: string;
}

/** Mirrors Common.Models.PropertyFraudRuleResult exactly - one row per fraud rule (7 once the engine has run at least once). */
export interface PropertyFraudRuleResult {
  /** e.g. "PRICE_ANOMALY" - matches dbo.FraudRuleWeight.RuleCode. */
  ruleCode: string;
  ruleName: string;
  triggered: boolean;
  /** The rule's weight if it fired, otherwise 0. */
  pointsAdded: number;
  /** The rule's configured weight regardless of whether it fired - for a "12 / 20" style bar. */
  maxPoints: number;
  description: string | null;
}

/** Mirrors Common.Models.PropertyDetail exactly - GET /api/properties/{id}'s response body. */
export interface PropertyDetail {
  listing: PropertyListingResult;
  images: PropertyImageSummary[];
  fraudReport: PropertyFraudRuleResult[];
}

/** Mirrors Common.Models.PropertySearchResponse exactly - GET /api/properties's response body. */
export interface PropertySearchResponse {
  items: PropertySearchResult[];
  totalRecords: number;
  pageNumber: number;
  pageSize: number;
}

/**
 * Mirrors DTOs.Property.CreatePropertyRequest exactly - POST
 * /api/properties's request body. No sellerId - the backend always takes
 * the owner from the caller's JWT (ICurrentUserService), never from this
 * body.
 */
export interface CreatePropertyRequest {
  title: string;
  description?: string;
  location: string;
  district?: string;
  latitude?: number;
  longitude?: number;
  /** Land size in perches. */
  size: number;
  /** Asking price in LKR. */
  price: number;
  /** Mandatory (Owner Name / Owner NIC / Owner Address requirement) - see CreatePropertyRequestValidator on the backend. */
  deedReference: string;
  /** The deed's registered owner name - mandatory, explicit deed-owner data distinct from the Seller account's own name. */
  ownerName: string;
  /** The deed's registered owner NIC - mandatory. Sri Lankan NIC format. */
  ownerNic: string;
  /** The deed's registered owner address - mandatory. */
  ownerAddress: string;
}

/**
 * Mirrors DTOs.Property.UpdatePropertyRequest exactly - PUT
 * /api/properties/{id}'s request body. Every field except
 * regeocodeLocation is optional, matching usp_Property_Update's
 * ISNULL(@Param, Column) pattern (only supplied fields change).
 */
export interface UpdatePropertyRequest {
  title?: string;
  description?: string;
  location?: string;
  district?: string;
  latitude?: number;
  longitude?: number;
  size?: number;
  price?: number;
  deedReference?: string;
  /** The deed's registered owner name. Optional here (omitting it on an edit leaves the existing value unchanged, it does not clear a mandatory field). */
  ownerName?: string;
  /** The deed's registered owner NIC. Optional here - see ownerName's doc comment. */
  ownerNic?: string;
  /** The deed's registered owner address. Optional here - see ownerName's doc comment. */
  ownerAddress?: string;
  /** True to re-geocode from the (possibly just-changed) location/district instead of keeping the existing coordinates - ignored if latitude/longitude are supplied explicitly. Always sent (the backend's own default is false, not omitted). */
  regeocodeLocation: boolean;
}

/**
 * Mirrors DTOs.Property.PropertySearchRequest - GET /api/properties's
 * query parameters ([FromQuery] on the backend, so this is sent as a
 * query string, never a JSON body - see propertyService.searchProperties).
 * Every field is optional here even though the backend DTO's sortBy/
 * pageNumber/pageSize have non-null C# defaults ("Newest"/1/12): omitting
 * a query key is exactly what causes ASP.NET Core model binding to fall
 * back to those defaults, so there is no need to duplicate that default
 * knowledge on the frontend.
 */
export interface PropertySearchRequest {
  keyword?: string;
  district?: string;
  minPrice?: number;
  maxPrice?: number;
  minSize?: number;
  maxSize?: number;
  riskLevel?: RiskLevel;
  sortBy?: PropertySortOption;
  pageNumber?: number;
  pageSize?: number;
}

/**
 * Mirrors API.Models.UploadPropertyImageRequest - POST
 * /api/properties/{id}/images's multipart/form-data body. `file` is a
 * browser File, not a backend IFormFile - propertyService builds the
 * actual FormData from this shape.
 */
export interface UploadPropertyImageRequest {
  file: File;
  isPrimary: boolean;
}

/**
 * Mirrors Common.Models.SellerContactInfo exactly - GET
 * /api/properties/{id}/seller-contact's response body (Contact Seller
 * workflow). Deliberately the smallest DTO in this file: no sellerId,
 * no NIC, no address, no deed/verification/fraud data - see that
 * endpoint's own doc comment on the backend for why. Buyer-only, and only
 * ever returned for a currently-Approved property.
 */
export interface SellerContactInfo {
  sellerName: string;
  phone: string | null;
  email: string | null;
  verifiedSeller: boolean;
}

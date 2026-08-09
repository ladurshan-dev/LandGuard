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

/** dbo.Property.Status, set by usp_Property_Create/usp_Property_Update and the fraud engine. Admin approve/reject/flag exist only as raw stored procedures today (usp_Admin_ApproveProperty/usp_Admin_RejectProperty) - no REST endpoint changes this value yet. */
export type PropertyStatus = 'Pending' | 'Approved' | 'Flagged' | 'Rejected';

/** dbo.RiskReport.RiskLevel banding - "Low" until the fraud engine has run at least once. */
export type RiskLevel = 'Low' | 'Medium' | 'High';

/** dbo.FraudCheck.FraudStatus - "Clean" until the fraud engine has run at least once. */
export type FraudStatus = 'Clean' | 'Suspicious' | 'Fraudulent';

/** usp_Property_Search's @SortBy parameter - see PropertyValidationRules.ValidSortOptions on the backend. */
export type PropertySortOption = 'Newest' | 'PriceAsc' | 'PriceDesc' | 'RiskAsc';

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
  deedReference: string | null;
  status: PropertyStatus;
  /** ISO 8601 timestamp. */
  uploadDate: string;
  sellerId: number;
  sellerName: string;
  sellerPhone: string | null;
  sellerNicVerified: boolean;
  riskScore: number | null;
  riskLevel: RiskLevel;
  fraudStatus: FraudStatus;
  riskSummary: string | null;
  /** ISO 8601 timestamp, null until the fraud engine has run at least once. */
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
  deedReference?: string;
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

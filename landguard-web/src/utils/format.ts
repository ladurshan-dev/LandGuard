/**
 * Small display-only formatting helpers shared across the property
 * screens. Purely presentational - nothing here encodes backend contract
 * knowledge, unlike types/property.ts.
 */

const CURRENCY_FORMATTER = new Intl.NumberFormat('en-LK', {
  style: 'currency',
  currency: 'LKR',
  maximumFractionDigits: 0,
});

const DATE_FORMATTER = new Intl.DateTimeFormat('en-LK', {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
});

/** Formats a price/amount in LKR, e.g. "LKR 4,500,000". */
export function formatCurrency(amount: number): string {
  return CURRENCY_FORMATTER.format(amount);
}

/** Formats an ISO 8601 timestamp as a short human-readable date, e.g. "8 Aug 2026". Returns "-" for null/invalid input rather than "Invalid Date". */
export function formatDate(isoDate: string | null | undefined): string {
  if (!isoDate) {
    return '-';
  }

  const parsed = new Date(isoDate);

  return Number.isNaN(parsed.getTime()) ? '-' : DATE_FORMATTER.format(parsed);
}

/** Formats land size in perches, e.g. "12.5 perches". */
export function formatSize(perches: number): string {
  return `${perches} perch${perches === 1 ? '' : 'es'}`;
}

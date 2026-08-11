using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.DTOs.GovernmentRegistry;

namespace LandGuard.Infrastructure.Services;

/// <inheritdoc cref="IGovernmentRegistryService" />
/// <summary>
/// Phase 1 implementation: a small, fixed, fully in-memory set of
/// fictional government land records (see <see cref="SeedRecords"/>) -
/// standing in for the real government registry integration this
/// academic project has no access to. No database table, no EF Core
/// migration, no new SQL Server schema - <c>Database/Scripts/*.sql</c>
/// remains the only owner of LandGuardDB's actual schema. A later phase
/// may replace this with a real, stored-procedure-backed or externally
/// integrated implementation without any change to
/// <see cref="IGovernmentRegistryService"/> or its callers.
/// </summary>
public class DummyGovernmentRegistryService : IGovernmentRegistryService
{
    /// <summary>
    /// Clearly fictional academic test data only - no real people's NICs,
    /// names, or government records. Deliberately covers the six
    /// demonstration scenarios a later phase's OCR-comparison engine is
    /// expected to exercise: (A) GR-000001 is a clean baseline a matching
    /// seller submission is expected to agree with in full; (B) GR-000002
    /// is the trusted-record half of a deliberate NIC mismatch; (C)
    /// GR-000003 is the trusted-record half of a deliberate deed-number
    /// mismatch; (D) GR-000004 is the trusted-record half of a deliberate
    /// land-size mismatch; (E) GR-000005 is the trusted-record half of a
    /// deliberate price anomaly; (F) GR-000006's Cancelled Status
    /// represents a government record that no longer backs a valid deed,
    /// and a genuinely missing record (an NIC/deed number/property
    /// reference not in this list at all) is already covered for free -
    /// every lookup method below returns null for it.
    /// </summary>
    private static readonly IReadOnlyList<GovernmentLandRecordDto> SeedRecords = new List<GovernmentLandRecordDto>
    {
        new()
        {
            RecordId = "GR-000001",
            Nic = "199012345678",
            OwnerName = "Nimal Perera",
            PropertyReference = "PROP-LK-0001",
            DeedNumber = "DEED-2026-0001",
            Address = "No. 12, Temple Road, Kandy",
            District = "Kandy",
            LandSize = 25.5,
            RegisteredPrice = 5_500_000m,
            RegistrationDate = new DateTime(2015, 6, 12),
            Status = "Active",
            DeedDocumentReference = "documents/government-registry/GR-000001/32a7cbd7754a47219ed1ff07e2db79db.pdf"
        },
        new()
        {
            RecordId = "GR-000002",
            Nic = "198522334455",
            OwnerName = "Kamala Silva",
            PropertyReference = "PROP-LK-0002",
            DeedNumber = "DEED-2026-0002",
            Address = "No. 45, Galle Road, Colombo",
            District = "Colombo",
            LandSize = 12.0,
            RegisteredPrice = 15_000_000m,
            RegistrationDate = new DateTime(2018, 3, 20),
            Status = "Active",
            DeedDocumentReference = "documents/government-registry/GR-000002/a1ae9a1608b64a9d8ed8fe511d49df0d.pdf"
        },
        new()
        {
            RecordId = "GR-000003",
            Nic = "197745566778",
            OwnerName = "Sunil Fernando",
            PropertyReference = "PROP-LK-0003",
            DeedNumber = "DEED-2026-0003",
            Address = "No. 8, Lake Road, Kurunegala",
            District = "Kurunegala",
            LandSize = 40.0,
            RegisteredPrice = 8_000_000m,
            RegistrationDate = new DateTime(2012, 11, 5),
            Status = "Active",
            DeedDocumentReference = "documents/government-registry/GR-000003/561de96a1d82462888ed93310997f8ac.pdf"
        },
        new()
        {
            RecordId = "GR-000004",
            Nic = "199234455667",
            OwnerName = "Chamari Jayasuriya",
            PropertyReference = "PROP-LK-0004",
            DeedNumber = "DEED-2026-0004",
            Address = "No. 21, Hill Street, Nuwara Eliya",
            District = "Nuwara Eliya",
            LandSize = 18.75,
            RegisteredPrice = 6_200_000m,
            RegistrationDate = new DateTime(2020, 1, 15),
            Status = "Active",
            DeedDocumentReference = "documents/government-registry/GR-000004/cccf81e351fd4eb88c9835ccf5fc0e1a.pdf"
        },
        new()
        {
            RecordId = "GR-000005",
            Nic = "198911223344",
            OwnerName = "Ruwan Bandara",
            PropertyReference = "PROP-LK-0005",
            DeedNumber = "DEED-2026-0005",
            Address = "No. 3, Station Road, Galle",
            District = "Galle",
            LandSize = 30.0,
            RegisteredPrice = 4_000_000m,
            RegistrationDate = new DateTime(2016, 8, 9),
            Status = "Active",
            DeedDocumentReference = "documents/government-registry/GR-000005/d4e53204b98b40aea84541b475196471.pdf"
        },
        new()
        {
            RecordId = "GR-000006",
            Nic = "199501122334",
            OwnerName = "Anusha Wickramasinghe",
            PropertyReference = "PROP-LK-0006",
            DeedNumber = "DEED-2026-0006",
            Address = "No. 55, Main Street, Matara",
            District = "Matara",
            LandSize = 22.0,
            RegisteredPrice = 5_000_000m,
            RegistrationDate = new DateTime(2010, 4, 22),
            Status = "Cancelled",
            DeedDocumentReference = "documents/government-registry/GR-000006/803aec164a73486b8806caac7fd01bd3.pdf"
        },

        // ------------------------------------------------------------------
        // SYNTHETIC MANUAL TEST DATASET (fictional, "-TEST-" namespaced so
        // they can never collide with the six demonstration records above).
        // Backs the 4-scenario deed-verification manual test matrix - see
        // the corresponding chat report and the new Section 9 of
        // Database/Scripts/05_SeedData.sql (which seeds the matching Seller
        // accounts only; Property listings and deed uploads are done by
        // hand through the UI, not seeded). Only Nic/DeedNumber/
        // PropertyReference/Status/DeedDocumentReference are ever read by
        // GovernmentDeedComparisonService (the other fields on this DTO are
        // documentary only - the actual compared values come from OCR'ing
        // the PDF at DeedDocumentReference) - each PDF below was generated
        // to print exactly what its own record's fields state here, plus
        // the deliberate GR-TEST-0003 deed-number mismatch. GR-TEST-0002
        // (Case 2, Form-vs-Deed mismatch) is deliberately NOT added here -
        // that scenario short-circuits in GovernmentDeedComparisonService
        // before any Government Registry lookup is attempted, so a record
        // would never be reached.
        new()
        {
            RecordId = "GR-TEST-0001",
            Nic = "199012345678",
            OwnerName = "Nimal Perera",
            PropertyReference = "PROP-TEST-0001",
            DeedNumber = "DEED-TEST-0001",
            Address = "No. 12, Temple Road, Kandy",
            District = "Kandy",
            LandSize = 25.5,
            RegisteredPrice = 5_500_000m,
            RegistrationDate = new DateTime(2019, 3, 10),
            Status = "Active",
            DeedDocumentReference = "documents/government-registry/GR-TEST-0001/registry.pdf"
        },
        new()
        {
            RecordId = "GR-TEST-0003",
            Nic = "199211122233",
            OwnerName = "Kasun Rathnayake",
            PropertyReference = "PROP-TEST-0003",
            // Deliberately different from the seller's own deed/listing
            // DeedReference ("DEED-TEST-0003") - the whole point of Case 3.
            // This is also why GetByDeedNumberAsync("DEED-TEST-0003") never
            // resolves this record; ResolveGovernmentRecordAsync only finds
            // it via the NIC fallback, exactly like a real registry lookup
            // that then reveals a conflicting deed number on file.
            DeedNumber = "DEED-TEST-9999",
            Address = "No. 7, Canal Road, Ratnapura",
            District = "Ratnapura",
            LandSize = 20.0,
            RegisteredPrice = 5_000_000m,
            RegistrationDate = new DateTime(2017, 9, 14),
            Status = "Active",
            DeedDocumentReference = "documents/government-registry/GR-TEST-0003/registry.pdf"
        },
        new()
        {
            RecordId = "GR-TEST-0004",
            Nic = "199355566677",
            OwnerName = "Dilani Gunawardena",
            PropertyReference = "PROP-TEST-0004",
            DeedNumber = "DEED-TEST-0004",
            Address = "No. 9, Fort Road, Galle",
            District = "Galle",
            LandSize = 20.0,
            // Deliberately >50% below the intended listing price (see the
            // matching Property.Price the tester enters, 15,000,000) -
            // DeedFieldComparer.ComparePrice's PriceAnomalyThreshold is 0.50m,
            // so a 4,000,000 vs 15,000,000 comparison (275% deviation) is
            // guaranteed anomalous regardless of OCR rounding.
            RegisteredPrice = 4_000_000m,
            RegistrationDate = new DateTime(2016, 11, 20),
            Status = "Active",
            DeedDocumentReference = "documents/government-registry/GR-TEST-0004/registry.pdf"
        },
        new()
        {
            // Fresh, fully-unique full-match fixture (manual-testing
            // request): DEED-TEST-0001/PROP-TEST-0001/GR-TEST-0001 now
            // belongs to a Withdrawn Property and must stay reserved, so
            // this is an entirely new -0005 identity, not a reuse of any
            // existing RecordId/PropertyReference/DeedNumber above.
            RecordId = "GR-TEST-0005",
            Nic = "199499911223",
            OwnerName = "Kamal Wijesinghe",
            PropertyReference = "PROP-TEST-0005",
            DeedNumber = "DEED-TEST-0005",
            Address = "No. 45, Lake Road, Kandy",
            District = "Kandy",
            LandSize = 30.0,
            RegisteredPrice = 7_500_000m,
            RegistrationDate = new DateTime(2026, 8, 1),
            Status = "Active",
            DeedDocumentReference = "documents/government-registry/GR-TEST-0005/registry.pdf"
        },
        new()
        {
            // Independent price-anomaly fixture for the Pending -> Admin
            // Reject -> Rejected -> Buyer-hidden manual test flow. All
            // authoritative fields match the seller deed exactly; the
            // only deliberate mismatch is the business comparison between
            // this RegisteredPrice (4,000,000) and the Property's own
            // typed Price (12,000,000 - a >50% deviation, same
            // GR-TEST-0004 convention), which
            // DeedFieldComparer.ComparePrice's PriceAnomalyThreshold
            // guarantees resolves to PriceAnomaly regardless of OCR
            // rounding - never DuplicateProperty/FormMismatch/other, so
            // the resulting Property.Status lands on Pending (not
            // auto-Approved/auto-Disapproved) for a human Admin to
            // manually Reject.
            RecordId = "GR-TEST-0006",
            Nic = "198877766655",
            OwnerName = "Sunil Jayawardena",
            PropertyReference = "PROP-TEST-0006",
            DeedNumber = "DEED-TEST-0006",
            Address = "No. 88, Main Street, Matara",
            District = "Matara",
            LandSize = 22.0,
            RegisteredPrice = 4_000_000m,
            RegistrationDate = new DateTime(2026, 8, 2),
            Status = "Active",
            DeedDocumentReference = "documents/government-registry/GR-TEST-0006/registry.pdf"
        },
        new()
        {
            // Duplicate GovernmentPropertyReference fixture (manual-testing
            // request): a brand-new RecordId/DeedNumber, but deliberately
            // the SAME PropertyReference ("PROP-TEST-0005") already held by
            // Property 38 (Withdrawn, but a Withdrawn property still counts
            // as the existing LandGuard representation of that parcel per
            // this project's duplicate-detection rule - see
            // usp_Property_ApplyDeedVerificationOutcome's EXISTS check,
            // untouched here). DEED-TEST-0007 is a fresh, unique deed
            // number so the early Create-time DeedReference duplicate
            // check (a separate, earlier check - see
            // GovernmentDeedComparisonService's own DeedReference-first
            // resolution order) never blocks this test; resolution finds
            // this record via GetByDeedNumberAsync("DEED-TEST-0007")
            // before any PropertyReference-based lookup would ever run, so
            // this record's shared PropertyReference with GR-TEST-0005
            // never causes a GetByPropertyReferenceAsync ambiguity in the
            // expected flow. All authoritative fields otherwise match
            // GR-TEST-0005 (same Owner/NIC/Address/District/LandSize/
            // RegisteredPrice) - the intentional duplicate is
            // PropertyReference only, exactly as requested.
            RecordId = "GR-TEST-0007",
            Nic = "199499911223",
            OwnerName = "Kamal Wijesinghe",
            PropertyReference = "PROP-TEST-0005",
            DeedNumber = "DEED-TEST-0007",
            Address = "No. 45, Lake Road, Kandy",
            District = "Kandy",
            LandSize = 30.0,
            RegisteredPrice = 7_500_000m,
            RegistrationDate = new DateTime(2026, 8, 5),
            Status = "Active",
            DeedDocumentReference = "documents/government-registry/GR-TEST-0007/registry.pdf"
        }
    };

    public Task<GovernmentLandRecordDto?> GetByNicAsync(string nic, CancellationToken cancellationToken = default)
    {
        var match = SeedRecords.FirstOrDefault(r => string.Equals(r.Nic, nic, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(match);
    }

    public Task<GovernmentLandRecordDto?> GetByDeedNumberAsync(string deedNumber, CancellationToken cancellationToken = default)
    {
        var match = SeedRecords.FirstOrDefault(r => string.Equals(r.DeedNumber, deedNumber, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(match);
    }

    public Task<GovernmentLandRecordDto?> GetByPropertyReferenceAsync(string propertyReference, CancellationToken cancellationToken = default)
    {
        var match = SeedRecords.FirstOrDefault(r => string.Equals(r.PropertyReference, propertyReference, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(match);
    }
}

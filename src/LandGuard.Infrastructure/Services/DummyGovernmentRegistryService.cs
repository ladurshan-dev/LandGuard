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

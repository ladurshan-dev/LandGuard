using LandGuard.Domain.Enums;

namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.Users</c> in LandGuardDB. The ER diagram calls this
/// entity USER; the physical table is <c>Users</c> because <c>USER</c> is
/// a reserved T-SQL keyword.
///
/// Plain POCO, not <see cref="Common.BaseEntity"/> - see the Module 2
/// integration notes in the solution README for why entities backed by
/// this externally-designed schema use their own natural-named primary
/// key instead of Module 1's generic <c>int Id</c> convention.
///
/// All writes to this table go through stored procedures
/// (<c>usp_User_Register</c>, <c>usp_Admin_SetUserActive</c>,
/// <c>usp_Admin_VerifyNIC</c>, ...), never through EF Core's
/// <c>SaveChanges</c> - the NIC/role/email validation rules and the
/// welcome-notification side effect live in T-SQL.
/// </summary>
public class User
{
    public int UserId { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    /// <summary>Sri Lankan NIC. Required for Seller, optional for Buyer (CK_Users_Seller_NIC).</summary>
    public string? Nic { get; set; }

    public string? Phone { get; set; }

    public UserRole Role { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>False when an admin has suspended the account (usp_Admin_SetUserActive).</summary>
    public bool IsActive { get; set; }

    /// <summary>[ext] FR02 - manual seller identity verification status.</summary>
    public bool NicVerified { get; set; }

    // Navigation properties -------------------------------------------------

    /// <summary>Listings this user has submitted as a Seller.</summary>
    public ICollection<Property> Properties { get; set; } = new List<Property>();

    /// <summary>Suspicious-listing reports this user has filed as a Buyer (FR12).</summary>
    public ICollection<SuspiciousReport> SuspiciousReportsFiled { get; set; } = new List<SuspiciousReport>();

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    /// <summary>Fraud-awareness podcasts this user has uploaded as an Admin (FR11).</summary>
    public ICollection<Podcast> PodcastsUploaded { get; set; } = new List<Podcast>();

    /// <summary>Listings this user has saved as a Buyer (FR07).</summary>
    public ICollection<SavedProperty> SavedProperties { get; set; } = new List<SavedProperty>();

    /// <summary>Admin actions this user performed as an Admin (FR09/NFR02).</summary>
    public ICollection<AdminAction> AdminActionsPerformed { get; set; } = new List<AdminAction>();

    /// <summary>Admin actions taken against this user's account (e.g. SuspendUser, VerifyNIC).</summary>
    public ICollection<AdminAction> AdminActionsReceived { get; set; } = new List<AdminAction>();
}

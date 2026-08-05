namespace LandGuard.Domain.Common;

/// <summary>
/// Base class for every domain entity (as opposed to a Value Object).
/// Entities are compared and tracked by identity (Id), not by the values
/// of their properties - two Property rows with identical fields but
/// different Ids are still different properties. Every entity in the
/// system (User, Property, FraudReport, LandRegistryRecord, ...)
/// ultimately derives from this so repositories, EF Core configuration,
/// and the generic Repository&lt;T&gt; can all operate on a single
/// well-known identity contract instead of each entity reinventing one.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
}

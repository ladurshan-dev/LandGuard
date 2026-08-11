using FluentValidation;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Admin;
using LandGuard.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace LandGuard.Application.Services;

/// <summary>
/// Implements <see cref="IAdminModerationService"/> - Phase B2 (Admin
/// Property Moderation API). Validates the request DTO (FluentValidation,
/// the same <c>ValidateAndThrowAsync</c> pattern <c>PropertyService</c>
/// already uses for Create/Update) and then calls straight through to
/// <see cref="IAdminStoredProcedures"/> - no ownership/role/existence
/// check is duplicated here, since <c>usp_Admin_ApproveProperty</c>/
/// <c>usp_Admin_RejectProperty</c> already perform all of that themselves
/// (see <see cref="IAdminModerationService"/>'s own doc comment).
/// </summary>
public class AdminModerationService : IAdminModerationService
{
    private readonly IAdminStoredProcedures _adminStoredProcedures;
    private readonly IApplicationDbContext _context;
    private readonly IValidator<ApprovePropertyRequest> _approveValidator;
    private readonly IValidator<RejectPropertyRequest> _rejectValidator;

    public AdminModerationService(
        IAdminStoredProcedures adminStoredProcedures,
        IApplicationDbContext context,
        IValidator<ApprovePropertyRequest> approveValidator,
        IValidator<RejectPropertyRequest> rejectValidator)
    {
        _adminStoredProcedures = adminStoredProcedures;
        _context = context;
        _approveValidator = approveValidator;
        _rejectValidator = rejectValidator;
    }

    public async Task<Result<PropertyListingResult>> ApprovePropertyAsync(
        int propertyId, int adminId, ApprovePropertyRequest? request, CancellationToken cancellationToken = default)
    {
        // The request body is optional (see ApprovePropertyRequest's own
        // doc comment) - only validate it when the caller actually sent
        // one.
        if (request is not null)
        {
            await _approveValidator.ValidateAndThrowAsync(request, cancellationToken);
        }

        // usp_Admin_ApproveProperty itself validates adminId is an active
        // Admin and that propertyId exists (RAISERROR either way, mapped
        // to a clean 400 by ExceptionHandlingMiddleware) - not
        // re-validated here.
        var listing = await _adminStoredProcedures.ApprovePropertyAsync(
            adminId, propertyId, request?.Remarks, cancellationToken);

        return Result<PropertyListingResult>.Success(listing);
    }

    public async Task<Result<PropertyListingResult>> RejectPropertyAsync(
        int propertyId, int adminId, RejectPropertyRequest request, CancellationToken cancellationToken = default)
    {
        await _rejectValidator.ValidateAndThrowAsync(request, cancellationToken);

        var listing = await _adminStoredProcedures.RejectPropertyAsync(
            adminId, propertyId, request.Reason, cancellationToken);

        return Result<PropertyListingResult>.Success(listing);
    }

    public async Task<Result<IReadOnlyList<FlaggedProperty>>> GetReviewQueueAsync(
        CancellationToken cancellationToken = default)
    {
        // Read-only projection over dbo.vw_FlaggedProperty via EF Core -
        // no stored-procedure wrapper needed (see this method's own doc
        // comment on IAdminModerationService). Same ordering
        // usp_Admin_GetFlagged itself uses (ORDER BY RiskScore DESC,
        // UploadDate ASC), so the highest-risk, longest-waiting listings
        // surface first regardless of which path a caller reaches this
        // data through.
        var items = await _context.FlaggedProperties
            .AsNoTracking()
            .OrderByDescending(property => property.RiskScore)
            .ThenBy(property => property.UploadDate)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<FlaggedProperty>>.Success(items);
    }
}

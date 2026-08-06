using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Auth;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Service Layer contract for authentication and account self-service.
/// The API layer's AuthController depends only on this interface, never
/// on <c>AuthService</c> directly or on any of the stored-procedure/
/// hashing/JWT abstractions it composes.
/// Every method returns a <see cref="Result"/>/<see cref="Result{T}"/> for
/// expected outcomes (wrong password, suspended account, user not found);
/// genuinely exceptional conditions (malformed request shape, a database
/// constraint violation such as a duplicate email) surface as exceptions
/// and are translated by <c>ExceptionHandlingMiddleware</c> instead - the
/// same split Module 1 established for every other service.
/// </summary>
public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterBuyerAsync(BuyerRegisterRequest request, CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> RegisterSellerAsync(SellerRegisterRequest request, CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<Result<UserProfile>> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<Result> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}

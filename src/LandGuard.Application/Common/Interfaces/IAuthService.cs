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

    /// <summary>
    /// POST /api/auth/register - the single public self-registration entry
    /// point (Buyer or Seller, chosen by <see cref="RegisterRequest.Role"/>,
    /// never Admin). Dispatches to <see cref="RegisterBuyerAsync"/>/
    /// <see cref="RegisterSellerAsync"/> rather than duplicating their
    /// hashing/persistence/JWT logic - see AuthService's implementation.
    /// </summary>
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<Result<UserProfile>> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<Result> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/auth/identity/reverify (Seller Government Identity
    /// Verification requirement). <paramref name="callerId"/> must be the
    /// caller's own id from the JWT - never trust a client-supplied
    /// UserID. See AuthService's own implementation for exactly how a
    /// Pending vs a Failed Seller's retry is handled.
    /// </summary>
    Task<Result<UserProfile>> ReverifyIdentityAsync(int callerId, CancellationToken cancellationToken = default);
}

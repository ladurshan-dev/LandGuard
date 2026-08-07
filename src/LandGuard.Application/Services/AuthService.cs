using FluentValidation;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Auth;
using LandGuard.Domain.Enums;

namespace LandGuard.Application.Services;

/// <summary>
/// Orchestrates registration, login, profile lookup and password changes.
/// Contains no SQL and no HTTP - it composes <see cref="IUserStoredProcedures"/>
/// (data access), <see cref="IPasswordHasher"/> and
/// <see cref="IJwtTokenGenerator"/> (both Infrastructure concerns reached
/// only through their Application-defined interfaces), which is exactly
/// what makes this class unit-testable with fakes for all three and zero
/// database involved.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserStoredProcedures _userStoredProcedures;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IValidator<BuyerRegisterRequest> _buyerRegisterValidator;
    private readonly IValidator<SellerRegisterRequest> _sellerRegisterValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;

    public AuthService(
        IUserStoredProcedures userStoredProcedures,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IValidator<BuyerRegisterRequest> buyerRegisterValidator,
        IValidator<SellerRegisterRequest> sellerRegisterValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<ChangePasswordRequest> changePasswordValidator)
    {
        _userStoredProcedures = userStoredProcedures;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _buyerRegisterValidator = buyerRegisterValidator;
        _sellerRegisterValidator = sellerRegisterValidator;
        _loginValidator = loginValidator;
        _changePasswordValidator = changePasswordValidator;
    }

    public async Task<Result<AuthResponse>> RegisterBuyerAsync(BuyerRegisterRequest request, CancellationToken cancellationToken = default)
    {
        await _buyerRegisterValidator.ValidateAndThrowAsync(request, cancellationToken);

        var passwordHash = _passwordHasher.Hash(request.Password);

        // usp_User_Register throws (as a SqlException, via RAISERROR) for a
        // duplicate email or a duplicate/invalid seller NIC - that is an
        // exceptional, database-enforced condition, not a Result.Failure,
        // and is left to propagate to ExceptionHandlingMiddleware.
        var profile = await _userStoredProcedures.RegisterAsync(
            request.Name, request.Email, passwordHash, UserRole.Buyer, request.Nic, request.Phone, cancellationToken);

        return Result<AuthResponse>.Success(BuildAuthResponse(profile));
    }

    public async Task<Result<AuthResponse>> RegisterSellerAsync(SellerRegisterRequest request, CancellationToken cancellationToken = default)
    {
        await _sellerRegisterValidator.ValidateAndThrowAsync(request, cancellationToken);

        var passwordHash = _passwordHasher.Hash(request.Password);

        var profile = await _userStoredProcedures.RegisterAsync(
            request.Name, request.Email, passwordHash, UserRole.Seller, request.Nic, request.Phone, cancellationToken);

        return Result<AuthResponse>.Success(BuildAuthResponse(profile));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var credential = await _userStoredProcedures.FindByEmailAsync(request.Email, cancellationToken);
   
        // Same generic message whether the email doesn't exist or the
        // password is wrong - never confirm to a caller whether an email
        // is registered (standard practice against account enumeration).
        if (credential is null || !_passwordHasher.Verify(request.Password, credential.PasswordHash))
        {
            return Result<AuthResponse>.Failure("Invalid email or password.");
        }

        if (!credential.IsActive)
        {
            return Result<AuthResponse>.Failure("Your account has been suspended. Contact an administrator.");
        }

        // usp_User_Login doesn't return CreatedAt (it only returns what
        // login needs), so the full profile - including CreatedAt - comes
        // from usp_User_GetById instead of being approximated here.
        var profile = await _userStoredProcedures.GetByIdAsync(credential.UserId, cancellationToken);

        if (profile is null)
        {
            // Should not happen (the row we just authenticated against
            // can't disappear mid-request) - treated as a failure rather
            // than throwing, since the caller's request itself was valid.
            return Result<AuthResponse>.Failure("Unable to load your account. Please try again.");
        }

        return Result<AuthResponse>.Success(BuildAuthResponse(profile));
    }

    public async Task<Result<UserProfile>> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var profile = await _userStoredProcedures.GetByIdAsync(userId, cancellationToken);

        return profile is null
            ? Result<UserProfile>.Failure("User not found.")
            : Result<UserProfile>.Success(profile);
    }

    public async Task<Result> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        await _changePasswordValidator.ValidateAndThrowAsync(request, cancellationToken);

        var profile = await _userStoredProcedures.GetByIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure("User not found.");
        }

        // usp_User_GetById never returns PasswordHash by design (it's a
        // "safe" profile projection) - re-fetch via FindByEmailAsync
        // (usp_User_Login) to get the hash needed to verify CurrentPassword,
        // rather than adding yet another procedure just to read one column.
        var credential = await _userStoredProcedures.FindByEmailAsync(profile.Email, cancellationToken);
        if (credential is null || !_passwordHasher.Verify(request.CurrentPassword, credential.PasswordHash))
        {
            return Result.Failure("Current password is incorrect.");
        }

        var newHash = _passwordHasher.Hash(request.NewPassword);
        await _userStoredProcedures.ChangePasswordAsync(userId, newHash, cancellationToken);

        return Result.Success();
    }

    private AuthResponse BuildAuthResponse(UserProfile profile)
    {
        var role = UserRoleExtensions.FromDbValue(profile.Role);
        var accessToken = _tokenGenerator.GenerateToken(profile.UserId, profile.Email, profile.Name, role);

        return new AuthResponse
        {
            AccessToken = accessToken.Token,
            ExpiresAtUtc = accessToken.ExpiresAtUtc,
            User = profile
        };
    }
}

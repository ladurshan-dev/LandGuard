using System.Text;
using LandGuard.API.Middleware;
using LandGuard.Application;
using LandGuard.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------
// Layer registration. Each layer owns its own DI wiring (see
// LandGuard.Application.DependencyInjection and
// LandGuard.Infrastructure.DependencyInjection) so Program.cs stays a
// thin composition root - a short, readable list of "what this app is
// made of" - instead of accumulating every individual service
// registration as more modules (Auth, Property, Fraud Detection, Admin)
// are added.
// ---------------------------------------------------------------------
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LandGuard API",
        Version = "v1",
        Description = "Land Transaction System with Rule-Based Fraud Detection"
    });

    // Lets Swagger UI attach a JWT bearer token to requests, since every
    // Buyer/Seller/Administrator endpoint beyond Health will require an
    // Authorization header once the Auth module ships.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT access token. Example: \"Bearer eyJhbGciOi...\""
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ---------------------------------------------------------------------
// CORS. The frontend is a separate static HTML/CSS/JS site (not served
// from this project's wwwroot), so browsers treat every call to this API
// as cross-origin. Allowed origins come from configuration rather than
// being hardcoded, so local dev, staging and production can each point
// at a different frontend host without a code change.
// ---------------------------------------------------------------------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("LandGuardClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ---------------------------------------------------------------------
// JWT Bearer authentication. This wiring lives in the composition root
// (not a dedicated "Auth module" file) because it is ASP.NET Core
// *pipeline configuration*, not business logic - it has to exist before
// any [Authorize] attribute can work, the same way the DbContext has to
// be registered before any repository can work. Token *issuance*
// (validating credentials, generating a signed token on login) is
// business logic and will live in an AuthService in the Application/
// Infrastructure layers when the Auth module is implemented next.
// ---------------------------------------------------------------------
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
             ?? throw new InvalidOperationException("Jwt:Key is not configured. Set it in appsettings.json or user-secrets.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Registered first so it wraps every other middleware and converts any
// unhandled exception - including ones from authentication/authorization
// or MVC model binding - into a consistent JSON error response instead of
// leaking a stack trace to a Buyer, Seller or Admin client.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "LandGuard API v1");
    });
}

app.UseHttpsRedirection();

app.UseCors("LandGuardClient");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program>-based integration tests can
// bootstrap the same host configuration in a future test project.
public partial class Program
{
}

# LandGuard — API Endpoint → Database Mapping

Every endpoint in the API Development plan, with the stored procedure or view
behind it. The C# service layer should call these rather than build SQL inline.

Connection string (`appsettings.json`):

```json
"ConnectionStrings": {
  "LandGuardDB": "Server=localhost\\SQLEXPRESS;Database=LandGuardDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
}
```

`MultipleActiveResultSets=True` is required — several procedures return more
than one result set.

---

## Phase 1 — PropertyController

| Method | Endpoint | Procedure / view | Notes |
|---|---|---|---|
| GET | `/api/properties` | `usp_Property_Search` | Paged. Params: `@Keyword`, `@District`, `@MinPrice`, `@MaxPrice`, `@MinSize`, `@MaxSize`, `@RiskLevel`, `@SortBy`, `@PageNumber`, `@PageSize`. Every row carries `TotalRecords` for the pager. |
| GET | `/api/properties/{id}` | `usp_Property_GetById` | **3 result sets:** listing, images, rule-by-rule fraud report |
| POST | `/api/properties` | `usp_Property_Create` → `usp_PropertyImage_Add` → `usp_Fraud_AnalyseProperty` | See the submission sequence below |
| PUT | `/api/properties/{id}` | `usp_Property_Update` | Resets status to `Pending` and re-runs the engine |
| DELETE | `/api/properties/{id}` | `usp_Property_Delete` | Authorises owner or admin; cascades to images, checks, reports |
| GET | `/api/properties/seller/{id}` | `usp_Property_GetBySeller` | Seller dashboard grid (FR08) |

### Property submission sequence

```csharp
// 1. Geocode first so fraud rule 6 has coordinates to check
var (lat, lng) = await _nominatim.GeocodeAsync(dto.Location);

// 2. Insert. This runs the engine once, before images exist.
var propertyId = await _propertyService.CreateAsync(dto, lat, lng);

// 3. Attach images with their hashes
foreach (var img in dto.Images)
    await _propertyService.AddImageAsync(propertyId, img.Url, Sha256(img.Bytes), img.IsPrimary);

// 4. Re-run so the duplicate-image and missing-info rules see the uploads
await _fraudService.AnalyseAsync(propertyId);
```

Step 4 matters. Without it a listing is scored before its images exist, so
rule 2 can never fire and rule 7 always fires.

---

## Phase 2 — FraudDetectionService / RiskReportService

| Purpose | Procedure / view |
|---|---|
| Run all 7 rules + generate the score | `usp_Fraud_AnalyseProperty @PropertyID` |
| Generate the risk report only | `usp_Risk_GenerateReport @FraudCheckID` |
| Re-score every listing after retuning | `usp_Fraud_ReanalyseAll` |
| Current risk of a property | `vw_PropertyLatestRisk` |
| Rule-by-rule breakdown for FR06 | `vw_FraudCheckDetail` |
| Rule trigger statistics | `vw_RuleTriggerFrequency` |
| Change a rule weight or threshold | `usp_Admin_UpdateRuleWeight` |

Helper functions available to the API: `fn_IsValidNIC`, `fn_RiskLevelFromScore`,
`fn_GetRuleWeight`.

---

## Phase 3 — AuthController

| Method | Endpoint | Procedure | Notes |
|---|---|---|---|
| POST | `/api/auth/register` | `usp_User_Register` | Hash the password with BCrypt **before** calling. Rejects duplicate email, invalid or duplicate seller NIC. |
| POST | `/api/auth/login` | `usp_User_Login` | Returns `PasswordHash`; verify it in C#, then issue the JWT |
| GET | `/api/auth/me` | `usp_User_GetById` | |

### JWT claims

Put `UserID` and `Role` in the token and authorise controllers with
`[Authorize(Roles = "Seller")]` / `"Admin"` / `"Buyer"`. The database enforces
role rules independently — `usp_Property_Create` rejects a non-seller,
`usp_Admin_*` procedures reject a non-admin — so a bug in the API layer cannot
bypass them.

### Return codes

`usp_User_Register` returns: `0` success · `-1` invalid role · `-2` email taken ·
`-3` invalid seller NIC · `-4` NIC already linked. It also raises an error with
a message suitable for showing to the user.

---

## Phase 4 — AdminController

| Method | Endpoint | Procedure | Notes |
|---|---|---|---|
| GET | `/api/admin/flagged` | `usp_Admin_GetFlagged` | Review queue, worst first |
| PUT | `/api/admin/approve/{id}` | `usp_Admin_ApproveProperty` | Publishes, audits, notifies the seller |
| PUT | `/api/admin/reject/{id}` | `usp_Admin_RejectProperty` | Rejects, resolves open reports, notifies |
| GET | `/api/admin/dashboard` | `usp_Admin_GetDashboard` | **3 result sets:** stats, rule frequency, top 20 flagged |
| PUT | `/api/admin/users/{id}/status` | `usp_Admin_SetUserActive` | Suspend or reactivate |
| PUT | `/api/admin/users/{id}/verify-nic` | `usp_Admin_VerifyNIC` | FR02 manual verification |
| PUT | `/api/admin/reports/{id}` | `usp_Admin_ResolveReport` | Close a buyer report and notify them |
| PUT | `/api/admin/rules/{code}` | `usp_Admin_UpdateRuleWeight` | Retune the engine |

---

## Buyer features

| Method | Endpoint | Procedure | FR |
|---|---|---|---|
| POST | `/api/reports` | `usp_SuspiciousReport_Create` | FR12 |
| POST | `/api/saved` | `usp_SavedProperty_Add` | FR07 |
| DELETE | `/api/saved/{propertyId}` | `usp_SavedProperty_Remove` | FR07 |
| GET | `/api/saved` | `usp_SavedProperty_GetByBuyer` | FR07 |
| GET | `/api/notifications` | `usp_Notification_GetByUser` | FR07 |
| PUT | `/api/notifications/{id}/read` | `usp_Notification_MarkRead` | FR07 |
| GET | `/api/podcasts` | `usp_Podcast_GetAll` | FR11 |
| POST | `/api/podcasts` | `usp_Podcast_Add` | FR11 |

---

## Calling the procedures from C#

### Dapper

```csharp
var p = new DynamicParameters();
p.Add("@District",   district);
p.Add("@RiskLevel",  riskLevel);
p.Add("@PageNumber", page);
p.Add("@PageSize",   size);

var results = await conn.QueryAsync<PropertyListingDto>(
    "dbo.usp_Property_Search", p, commandType: CommandType.StoredProcedure);
```

Multiple result sets:

```csharp
using var multi = await conn.QueryMultipleAsync(
    "dbo.usp_Property_GetById",
    new { PropertyID = id },
    commandType: CommandType.StoredProcedure);

var property   = await multi.ReadFirstOrDefaultAsync<PropertyListingDto>();
var images     = (await multi.ReadAsync<PropertyImageDto>()).ToList();
var fraudRules = (await multi.ReadAsync<FraudRuleResultDto>()).ToList();
```

### EF Core

```csharp
var propertyId = new SqlParameter("@NewPropertyID", SqlDbType.Int)
                 { Direction = ParameterDirection.Output };

await _ctx.Database.ExecuteSqlRawAsync(
    "EXEC dbo.usp_Property_Create @SellerID, @Title, @Description, @Location, " +
    "@District, @Latitude, @Longitude, @Size, @Price, @DeedReference, " +
    "@NewPropertyID OUTPUT",
    sellerId, title, description, location, district, lat, lng, size, price,
    deedRef, propertyId);
```

Map views as keyless entities for read models:

```csharp
modelBuilder.Entity<PropertyListing>().HasNoKey().ToView("vw_PropertyListing");
modelBuilder.Entity<FraudStatistics>().HasNoKey().ToView("vw_FraudStatistics");
```

---

## Suggested C# models

```csharp
public class User          // dbo.Users
{
    public int      UserId       { get; set; }
    public string   Name         { get; set; }
    public string   Email        { get; set; }
    public string   PasswordHash { get; set; }
    public string?  Nic          { get; set; }
    public string?  Phone        { get; set; }
    public string   Role         { get; set; }   // Buyer | Seller | Admin
    public DateTime CreatedAt    { get; set; }
    public bool     IsActive     { get; set; }
    public bool     NicVerified  { get; set; }
}

public class Property      // dbo.Property
{
    public int      PropertyId    { get; set; }
    public int      SellerId      { get; set; }
    public string   Title         { get; set; }
    public string?  Description   { get; set; }
    public string   Location      { get; set; }
    public string?  District      { get; set; }
    public decimal? Latitude      { get; set; }
    public decimal? Longitude     { get; set; }
    public double   Size          { get; set; }
    public decimal  Price         { get; set; }
    public string?  DeedReference { get; set; }
    public string   Status        { get; set; }  // Pending|Approved|Flagged|Rejected
    public DateTime UploadDate    { get; set; }
}

public class FraudCheck    // dbo.FraudCheck  (true = indicator DETECTED)
{
    public int      FraudCheckId       { get; set; }
    public int      PropertyId         { get; set; }
    public bool     PriceCheck         { get; set; }
    public bool     DuplicateCheck     { get; set; }
    public bool     NicCheck           { get; set; }
    public bool     DeedCheck          { get; set; }
    public bool     SellerHistoryCheck { get; set; }
    public bool     LocationCheck      { get; set; }
    public bool     MissingInfoCheck   { get; set; }
    public string   FraudStatus        { get; set; }
    public DateTime CheckDate          { get; set; }
}

public class RiskReport    // dbo.RiskReport
{
    public int      ReportId      { get; set; }
    public int      FraudCheckId  { get; set; }
    public int      RiskScore     { get; set; }   // 0-100
    public string   RiskLevel     { get; set; }   // Low | Medium | High
    public string?  Summary       { get; set; }
    public DateTime GeneratedDate { get; set; }
}
```

---

## Test accounts

All seeded passwords are BCrypt hashes of `Test@123` — local testing only.

| Role | Email | Useful for |
|---|---|---|
| Admin | `abilasha@landguard.lk` | Admin endpoints |
| Admin | `ladhurshan@landguard.lk` | Admin endpoints |
| Seller | `chathura@example.com` | Clean submissions |
| Seller | `rajitha.bandara@example.com` | Unverified NIC path |
| Seller | `malith.j@example.com` | Seller history rule |
| Seller | `priyantha.alwis@example.com` | Suspended account (login should fail) |
| Buyer | `sanduni.r@example.com` | Saved properties, reports |

using ITAssets.Api.Models;
using Microsoft.Data.SqlClient;
using static ITAssets.Api.Data.Sql;

namespace ITAssets.Api.Data;

// ============================ AUTH ============================
public interface IAuthRepository
{
    Task<UserRecord?> GetByUsernameAsync(string username, CancellationToken ct);
    Task RegisterFailedAsync(int userId, CancellationToken ct);
    Task ResetAttemptsAsync(int userId, CancellationToken ct);
}

public class AuthRepository : IAuthRepository
{
    private readonly ISqlExecutor _db;
    public AuthRepository(ISqlExecutor db) => _db = db;

    public async Task<UserRecord?> GetByUsernameAsync(string username, CancellationToken ct)
    {
        var rows = await _db.QueryAsync("dbo.sp_GetUserByUsername", r => new UserRecord
        {
            Id = r.Int("Id"), Username = r.Str("Username"), PasswordHash = r.Str("PasswordHash"),
            RoleName = r.Str("RoleName"), IsActive = r.Bool("IsActive"),
            FailedAttempts = r.Int("FailedAttempts"), LockoutEnd = r.DtN("LockoutEnd")
        }, new[] { Text("@Username", username, 100) }, ct);
        return rows.FirstOrDefault();
    }

    public Task RegisterFailedAsync(int userId, CancellationToken ct) =>
        _db.ExecuteAsync("dbo.sp_RegisterFailedLogin", new[] { P("@UserId", userId) }, ct);

    public Task ResetAttemptsAsync(int userId, CancellationToken ct) =>
        _db.ExecuteAsync("dbo.sp_RegisterSuccessfulLogin", new[] { P("@UserId", userId) }, ct);
}

// ============================ ASSETS ============================
public interface IAssetRepository
{
    Task<int> CreateAsync(CreateAssetRequest req, int userId, CancellationToken ct);
    Task UpdateAsync(int id, UpdateAssetRequest req, int userId, CancellationToken ct);
    Task<AssetDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<PagedResult<AssetDto>> GetPageAsync(AssetQuery q, CancellationToken ct);
    Task<int> AssignAsync(int assetId, AssignAssetRequest req, int userId, CancellationToken ct);
    Task<int> ReturnAsync(int assetId, ReturnAssetRequest req, int userId, CancellationToken ct);
    Task<PagedResult<AssetMovementDto>> GetHistoryAsync(int assetId, int page, int pageSize, CancellationToken ct);
}

public class AssetRepository : IAssetRepository
{
    private readonly ISqlExecutor _db;
    public AssetRepository(ISqlExecutor db) => _db = db;

    public Task<int> CreateAsync(CreateAssetRequest r, int userId, CancellationToken ct) =>
        _db.ScalarIntAsync("dbo.sp_CreateAsset", new[]
        {
            Text("@AssetCode", r.AssetCode.Trim(), 30), Text("@SerialNumber", Clean(r.SerialNumber), 100),
            Text("@Category", r.Category.Trim(), 50), Text("@Brand", r.Brand.Trim(), 50), Text("@Model", r.Model.Trim(), 80),
            Text("@OwnershipType", r.OwnershipType, 20), P("@SupplierId", r.SupplierId),
            Text("@CurrentLocation", Clean(r.CurrentLocation), 100),
            Date("@PurchaseDate", r.PurchaseDate), Date("@RentalEndDate", r.RentalEndDate),
            P("@UserId", userId)
        }, ct);

    public Task UpdateAsync(int id, UpdateAssetRequest r, int userId, CancellationToken ct) =>
        _db.ExecuteAsync("dbo.sp_UpdateAsset", new[]
        {
            P("@Id", id), Text("@SerialNumber", Clean(r.SerialNumber), 100),
            Text("@Category", r.Category.Trim(), 50), Text("@Brand", r.Brand.Trim(), 50), Text("@Model", r.Model.Trim(), 80),
            Text("@OwnershipType", r.OwnershipType, 20), P("@SupplierId", r.SupplierId),
            Text("@Status", r.Status, 20), Text("@CurrentLocation", Clean(r.CurrentLocation), 100),
            Date("@PurchaseDate", r.PurchaseDate), Date("@RentalEndDate", r.RentalEndDate),
            P("@UserId", userId), Text("@Notes", Clean(r.Notes), 500),
            Binary8("@RowVer", r.RowVer is null ? null : Convert.FromHexString(r.RowVer[2..]))
        }, ct);

    public async Task<AssetDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var rows = await _db.QueryAsync("dbo.sp_GetAssetById", r => Map(r, includeRowVer: true),
            new[] { P("@Id", id) }, ct);
        return rows.FirstOrDefault();
    }

    public async Task<PagedResult<AssetDto>> GetPageAsync(AssetQuery q, CancellationToken ct)
    {
        var total = 0;
        var items = await _db.QueryAsync("dbo.sp_GetAssets", r => { total = r.Int("TotalCount"); return Map(r, false); }, new[]
        {
            Text("@Search", Clean(q.Search), 100), Text("@Status", Clean(q.Status), 20),
            Text("@Category", Clean(q.Category), 50), P("@Page", q.Page), P("@PageSize", q.PageSize)
        }, ct);
        return PagedResult<AssetDto>.Create(items, q.Page, q.PageSize, total);
    }

    public Task<int> AssignAsync(int assetId, AssignAssetRequest r, int userId, CancellationToken ct) =>
        _db.ScalarIntAsync("dbo.sp_AssignAsset", new[]
        {
            P("@AssetId", assetId), P("@EmployeeId", r.EmployeeId), P("@UserId", userId),
            Text("@Notes", Clean(r.Notes), 500)
        }, ct);

    public Task<int> ReturnAsync(int assetId, ReturnAssetRequest r, int userId, CancellationToken ct) =>
        _db.ScalarIntAsync("dbo.sp_ReturnAsset", new[]
        {
            P("@AssetId", assetId), P("@UserId", userId),
            Text("@ReturnCondition", r.ReturnCondition.Trim(), 200), Text("@Notes", Clean(r.Notes), 500)
        }, ct);

    public async Task<PagedResult<AssetMovementDto>> GetHistoryAsync(int assetId, int page, int pageSize, CancellationToken ct)
    {
        var total = 0;
        var items = await _db.QueryAsync("dbo.sp_GetAssetHistory", r =>
        {
            total = r.Int("TotalCount");
            return new AssetMovementDto
            {
                Id = r.Long("Id"), AssetId = r.Int("AssetId"), MovementType = r.Str("MovementType"),
                PreviousValue = r.StrN("PreviousValue"), NewValue = r.StrN("NewValue"),
                EmployeeId = r.IntN("EmployeeId"), EmployeeName = r.StrN("EmployeeName"),
                PerformedByUserId = r.Int("PerformedByUserId"), PerformedBy = r.Str("PerformedBy"),
                PerformedAt = r.Dt("PerformedAt"), Notes = r.StrN("Notes")
            };
        }, new[] { P("@AssetId", assetId), P("@Page", page), P("@PageSize", pageSize) }, ct);
        return PagedResult<AssetMovementDto>.Create(items, page, pageSize, total);
    }

    private static AssetDto Map(SqlDataReader r, bool includeRowVer) => new()
    {
        Id = r.Int("Id"), AssetCode = r.Str("AssetCode"), SerialNumber = r.StrN("SerialNumber"),
        Category = r.Str("Category"), Brand = r.Str("Brand"), Model = r.Str("Model"),
        OwnershipType = r.Str("OwnershipType"), SupplierId = r.IntN("SupplierId"), SupplierName = r.StrN("SupplierName"),
        Status = r.Str("Status"), CurrentLocation = r.StrN("CurrentLocation"),
        PurchaseDate = r.DtN("PurchaseDate"), RentalEndDate = r.DtN("RentalEndDate"),
        CreatedAt = r.Dt("CreatedAt"), UpdatedAt = r.Dt("UpdatedAt"),
        RowVer = includeRowVer ? r.StrN("RowVer") : null,
        AssignedEmployeeId = r.IntN("AssignedEmployeeId"), AssignedEmployeeName = r.StrN("AssignedEmployeeName")
    };
}

// ============================ EMPLOYEES ============================
public interface IEmployeeRepository
{
    Task<int> CreateAsync(CreateEmployeeRequest req, CancellationToken ct);
    Task<EmployeeDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<PagedResult<EmployeeDto>> GetPageAsync(EmployeeQuery q, CancellationToken ct);
    Task SetActiveAsync(int id, bool isActive, CancellationToken ct);
}

public class EmployeeRepository : IEmployeeRepository
{
    private readonly ISqlExecutor _db;
    public EmployeeRepository(ISqlExecutor db) => _db = db;

    public Task<int> CreateAsync(CreateEmployeeRequest r, CancellationToken ct) =>
        _db.ScalarIntAsync("dbo.sp_CreateEmployee", new[]
        {
            Text("@EmployeeNumber", r.EmployeeNumber.Trim(), 30),
            Text("@FullName", r.FullName.Trim(), 150), Text("@Email", r.Email.Trim(), 150)
        }, ct);

    public async Task<EmployeeDto?> GetByIdAsync(int id, CancellationToken ct) =>
        (await _db.QueryAsync("dbo.sp_GetEmployeeById", Map, new[] { P("@Id", id) }, ct)).FirstOrDefault();

    public async Task<PagedResult<EmployeeDto>> GetPageAsync(EmployeeQuery q, CancellationToken ct)
    {
        var total = 0;
        var items = await _db.QueryAsync("dbo.sp_GetEmployees", r => { total = r.Int("TotalCount"); return Map(r); }, new[]
        {
            Text("@Search", Clean(q.Search), 100), P("@IsActive", q.IsActive),
            P("@Page", q.Page), P("@PageSize", q.PageSize)
        }, ct);
        return PagedResult<EmployeeDto>.Create(items, q.Page, q.PageSize, total);
    }

    public Task SetActiveAsync(int id, bool isActive, CancellationToken ct) =>
        _db.ExecuteAsync("dbo.sp_SetEmployeeActive", new[] { P("@Id", id), P("@IsActive", isActive) }, ct);

    private static EmployeeDto Map(SqlDataReader r) => new()
    {
        Id = r.Int("Id"), EmployeeNumber = r.Str("EmployeeNumber"), FullName = r.Str("FullName"),
        Email = r.Str("Email"), IsActive = r.Bool("IsActive")
    };
}

// ============================ SUPPLIERS ============================
public interface ISupplierRepository
{
    Task<int> CreateAsync(CreateSupplierRequest req, CancellationToken ct);
    Task<SupplierDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<PagedResult<SupplierDto>> GetPageAsync(SupplierQuery q, CancellationToken ct);
}

public class SupplierRepository : ISupplierRepository
{
    private readonly ISqlExecutor _db;
    public SupplierRepository(ISqlExecutor db) => _db = db;

    public Task<int> CreateAsync(CreateSupplierRequest r, CancellationToken ct) =>
        _db.ScalarIntAsync("dbo.sp_CreateSupplier", new[]
        {
            Text("@Name", r.Name.Trim(), 150), Text("@Email", Clean(r.Email), 150), Text("@Phone", Clean(r.Phone), 30),
            Text("@Services", r.Services.Count == 0 ? null : string.Join(",", r.Services), 200)
        }, ct);

    public async Task<SupplierDto?> GetByIdAsync(int id, CancellationToken ct) =>
        (await _db.QueryAsync("dbo.sp_GetSupplierById", Map, new[] { P("@Id", id) }, ct)).FirstOrDefault();

    public async Task<PagedResult<SupplierDto>> GetPageAsync(SupplierQuery q, CancellationToken ct)
    {
        var total = 0;
        var items = await _db.QueryAsync("dbo.sp_GetSuppliers", r => { total = r.Int("TotalCount"); return Map(r); }, new[]
        {
            Text("@Search", Clean(q.Search), 100), P("@Page", q.Page), P("@PageSize", q.PageSize)
        }, ct);
        return PagedResult<SupplierDto>.Create(items, q.Page, q.PageSize, total);
    }

    private static SupplierDto Map(SqlDataReader r) => new()
    {
        Id = r.Int("Id"), Name = r.Str("Name"), Email = r.StrN("Email"), Phone = r.StrN("Phone"),
        IsActive = r.Bool("IsActive"),
        Services = r.Str("Services").Split(',', StringSplitOptions.RemoveEmptyEntries)
    };
}

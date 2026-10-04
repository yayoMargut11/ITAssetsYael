using ITAssets.Api.Data;
using ITAssets.Api.Models;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ITAssets.Tests.Integration;

/// <summary>
/// Requieren SQL Server con los scripts 01, 02 y 03 aplicados y la variable ConnectionStrings__Default.
/// Cada prueba crea sus propios datos con códigos únicos, así que son repetibles.
/// </summary>
public class AssignmentConcurrencyTests
{
    private readonly IAssetRepository _assets;
    private readonly IEmployeeRepository _employees;
    private readonly IAuthRepository _auth;

    public AssignmentConcurrencyTests()
    {
        var cs = TestDb.ConnectionString ?? "Server=unused";
        var db = new SqlExecutor(cs);
        _assets = new AssetRepository(db);
        _employees = new EmployeeRepository(db);
        _auth = new AuthRepository(db);
    }

    private async Task<int> AdminIdAsync() => (await _auth.GetByUsernameAsync("admin", default))!.Id;

    private async Task<int> NewAssetAsync(int userId) =>
        await _assets.CreateAsync(new CreateAssetRequest
        {
            AssetCode = "T-" + Guid.NewGuid().ToString("N")[..12], Category = "Laptop", Brand = "TEST",
            Model = "Concurrency", OwnershipType = OwnershipTypes.Owned
        }, userId, default);

    private async Task<int> NewEmployeeAsync(bool active = true)
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        var id = await _employees.CreateAsync(new CreateEmployeeRequest
        {
            EmployeeNumber = "T-" + tag, FullName = "Test " + tag, Email = $"{tag}@test.example.com"
        }, default);
        if (!active) await _employees.SetActiveAsync(id, false, default);
        return id;
    }

    [FactRequiresDb]
    public async Task Ten_simultaneous_assignments_only_one_succeeds()
    {
        var admin = await AdminIdAsync();
        var assetId = await NewAssetAsync(admin);
        var employeeIds = new List<int>();
        for (var i = 0; i < 10; i++) employeeIds.Add(await NewEmployeeAsync());

        var attempts = employeeIds.Select(async emp =>
        {
            try
            {
                await _assets.AssignAsync(assetId, new AssignAssetRequest { EmployeeId = emp }, admin, default);
                return (Ok: true, Error: 0);
            }
            catch (SqlException ex) { return (Ok: false, Error: ex.Number); }
        });
        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(r => r.Ok));
        // Los perdedores deben recibir un error de negocio controlado, no uno inesperado
        Assert.All(results.Where(r => !r.Ok), r => Assert.Contains(r.Error, new[] { 50002, 2601, 2627, 1205 }));

        var asset = await _assets.GetByIdAsync(assetId, default);
        Assert.Equal(AssetStatuses.Assigned, asset!.Status);
        var history = await _assets.GetHistoryAsync(assetId, 1, 50, default);
        Assert.Equal(1, history.Items.Count(h => h.MovementType == "Asignacion"));
    }

    [FactRequiresDb]
    public async Task Cannot_assign_to_inactive_employee()
    {
        var admin = await AdminIdAsync();
        var assetId = await NewAssetAsync(admin);
        var inactive = await NewEmployeeAsync(active: false);

        var ex = await Assert.ThrowsAsync<SqlException>(() =>
            _assets.AssignAsync(assetId, new AssignAssetRequest { EmployeeId = inactive }, admin, default));
        Assert.Equal(50004, ex.Number);
        Assert.Equal(AssetStatuses.Available, (await _assets.GetByIdAsync(assetId, default))!.Status);
    }

    [FactRequiresDb]
    public async Task Return_without_active_assignment_fails()
    {
        var admin = await AdminIdAsync();
        var assetId = await NewAssetAsync(admin);

        var ex = await Assert.ThrowsAsync<SqlException>(() =>
            _assets.ReturnAsync(assetId, new ReturnAssetRequest { ReturnCondition = "Buena" }, admin, default));
        Assert.Equal(50030, ex.Number);
    }

    [FactRequiresDb]
    public async Task Full_cycle_assign_return_reassign_records_history()
    {
        var admin = await AdminIdAsync();
        var assetId = await NewAssetAsync(admin);
        var e1 = await NewEmployeeAsync();
        var e2 = await NewEmployeeAsync();

        await _assets.AssignAsync(assetId, new AssignAssetRequest { EmployeeId = e1 }, admin, default);
        await _assets.ReturnAsync(assetId, new ReturnAssetRequest { ReturnCondition = "Rayones leves" }, admin, default);
        await _assets.AssignAsync(assetId, new AssignAssetRequest { EmployeeId = e2 }, admin, default);

        var history = await _assets.GetHistoryAsync(assetId, 1, 50, default);
        var types = history.Items.Select(h => h.MovementType).OrderBy(t => t, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "Alta", "Asignacion", "Asignacion", "Devolucion" }, types);
        Assert.All(history.Items, h => Assert.Equal("admin", h.PerformedBy));
    }

    [FactRequiresDb]
    public async Task Retired_asset_cannot_be_assigned()
    {
        var admin = await AdminIdAsync();
        var assetId = await NewAssetAsync(admin);
        var emp = await NewEmployeeAsync();
        var asset = (await _assets.GetByIdAsync(assetId, default))!;
        await _assets.UpdateAsync(assetId, new UpdateAssetRequest
        {
            Category = asset.Category, Brand = asset.Brand, Model = asset.Model, OwnershipType = asset.OwnershipType,
            Status = AssetStatuses.Retired, Notes = "Baja de prueba"
        }, admin, default);

        var ex = await Assert.ThrowsAsync<SqlException>(() =>
            _assets.AssignAsync(assetId, new AssignAssetRequest { EmployeeId = emp }, admin, default));
        Assert.Equal(50002, ex.Number);
    }
}

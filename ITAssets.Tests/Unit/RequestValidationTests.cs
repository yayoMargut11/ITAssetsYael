using System.ComponentModel.DataAnnotations;
using ITAssets.Api.Models;
using Xunit;

namespace ITAssets.Tests.Unit;

public class RequestValidationTests
{
    private static List<ValidationResult> Validate(object o)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(o, new ValidationContext(o), results, validateAllProperties: true);
        return results;
    }

    private static CreateAssetRequest ValidAsset() => new()
    {
        AssetCode = "TI-0009001", Category = "Laptop", Brand = "DELL", Model = "Latitude",
        OwnershipType = OwnershipTypes.Owned
    };

    [Fact] public void Valid_asset_passes() => Assert.Empty(Validate(ValidAsset()));

    [Fact]
    public void Rented_asset_without_supplier_is_invalid()
    {
        var a = ValidAsset();
        a.OwnershipType = OwnershipTypes.Rented;
        Assert.Contains(Validate(a), r => r.MemberNames.Contains(nameof(CreateAssetRequest.SupplierId)));
    }

    [Fact]
    public void Rented_asset_with_supplier_is_valid()
    {
        var a = ValidAsset();
        a.OwnershipType = OwnershipTypes.Rented;
        a.SupplierId = 1;
        Assert.Empty(Validate(a));
    }

    [Fact]
    public void Unknown_ownership_type_is_invalid()
    {
        var a = ValidAsset();
        a.OwnershipType = "Prestado";
        Assert.NotEmpty(Validate(a));
    }

    [Fact]
    public void Rental_end_before_purchase_is_invalid()
    {
        var a = ValidAsset();
        a.OwnershipType = OwnershipTypes.Rented; a.SupplierId = 1;
        a.PurchaseDate = new DateTime(2025, 6, 1); a.RentalEndDate = new DateTime(2025, 1, 1);
        Assert.NotEmpty(Validate(a));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TI 0001")]       // espacios
    [InlineData("TI;DROP TABLE")] // caracteres no permitidos
    public void Invalid_asset_codes_are_rejected(string code)
    {
        var a = ValidAsset();
        a.AssetCode = code;
        Assert.NotEmpty(Validate(a));
    }

    [Fact]
    public void Too_long_fields_are_rejected()
    {
        var a = ValidAsset();
        a.Brand = new string('x', 51);
        Assert.NotEmpty(Validate(a));
    }

    [Theory]
    [InlineData("ana@empresa.com", true)]
    [InlineData("ana@empresa", false)]
    [InlineData("ana empresa.com", false)]
    [InlineData("@empresa.com", false)]
    [InlineData("", false)]
    public void Employee_email_format(string email, bool valid)
    {
        var e = new CreateEmployeeRequest { EmployeeNumber = "E-1", FullName = "Ana", Email = email };
        Assert.Equal(valid, Validate(e).Count == 0);
    }

    [Fact]
    public void Update_with_invalid_status_or_rowver_is_invalid()
    {
        var u = new UpdateAssetRequest
        {
            Category = "Laptop", Brand = "DELL", Model = "X", OwnershipType = OwnershipTypes.Owned,
            Status = "Roto", RowVer = "abc"
        };
        var errors = Validate(u);
        Assert.Contains(errors, r => r.MemberNames.Contains(nameof(UpdateAssetRequest.Status)));
        Assert.Contains(errors, r => r.MemberNames.Contains(nameof(UpdateAssetRequest.RowVer)));
    }

    [Fact]
    public void Asset_query_rejects_unknown_status_and_bad_paging()
    {
        Assert.NotEmpty(Validate(new AssetQuery { Status = "Perdido" }));
        Assert.NotEmpty(Validate(new AssetQuery { Page = 0 }));
        Assert.NotEmpty(Validate(new AssetQuery { PageSize = 1000 }));
        Assert.Empty(Validate(new AssetQuery { Status = "Disponible", Search = "DELL", Category = "Laptop" }));
    }

    [Fact]
    public void Supplier_rejects_unknown_service_type()
    {
        var s = new CreateSupplierRequest { Name = "Proveedor", Services = new() { "Compra", "Magia" } };
        Assert.NotEmpty(Validate(s));
        s.Services = new() { "Compra", "Mantenimiento" };
        Assert.Empty(Validate(s));
    }

    [Fact]
    public void Assign_requires_positive_employee_id() =>
        Assert.NotEmpty(Validate(new AssignAssetRequest { EmployeeId = 0 }));

    [Fact]
    public void Return_requires_condition() =>
        Assert.NotEmpty(Validate(new ReturnAssetRequest { ReturnCondition = " " }));
}

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ITAssets.Api.Models;

// ---------- Comunes ----------
public class PageQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }

    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int total) => new()
    {
        Items = items, Page = page, PageSize = pageSize, TotalCount = total,
        TotalPages = (int)Math.Ceiling(total / (double)pageSize)
    };
}

public class IdResponse { public int Id { get; init; } }

// ---------- Auth ----------
public class LoginRequest
{
    [Required, StringLength(100)] public string Username { get; set; } = "";
    [Required, StringLength(200)] public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; init; } = "";
    public DateTime ExpiresAtUtc { get; init; }
    public string Username { get; init; } = "";
    public string Role { get; init; } = "";
}

public class UserRecord
{
    public int Id { get; init; }
    public string Username { get; init; } = "";
    public string PasswordHash { get; init; } = "";
    public string RoleName { get; init; } = "";
    public bool IsActive { get; init; }
    public int FailedAttempts { get; init; }
    public DateTime? LockoutEnd { get; init; }
}

// ---------- Activos ----------
public abstract class AssetInputBase : IValidatableObject
{
    [StringLength(100)] public string? SerialNumber { get; set; }
    [Required, StringLength(50)] public string Category { get; set; } = "";
    [Required, StringLength(50)] public string Brand { get; set; } = "";
    [Required, StringLength(80)] public string Model { get; set; } = "";
    [Required] public string OwnershipType { get; set; } = "";
    public int? SupplierId { get; set; }
    [StringLength(100)] public string? CurrentLocation { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? RentalEndDate { get; set; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!OwnershipTypes.All.Contains(OwnershipType))
            yield return new ValidationResult("OwnershipType debe ser 'Propio' o 'Arrendado'.", new[] { nameof(OwnershipType) });
        else
        {
            if (OwnershipType == OwnershipTypes.Rented && SupplierId is null)
                yield return new ValidationResult("Un activo arrendado debe tener proveedor.", new[] { nameof(SupplierId) });
            if (OwnershipType == OwnershipTypes.Owned && RentalEndDate.HasValue)
                yield return new ValidationResult("Solo los activos arrendados tienen fecha fin de renta.", new[] { nameof(RentalEndDate) });
        }
        if (SupplierId is <= 0)
            yield return new ValidationResult("SupplierId no es válido.", new[] { nameof(SupplierId) });
        if (PurchaseDate.HasValue && RentalEndDate.HasValue && RentalEndDate < PurchaseDate)
            yield return new ValidationResult("RentalEndDate no puede ser anterior a PurchaseDate.", new[] { nameof(RentalEndDate) });
    }
}

public class CreateAssetRequest : AssetInputBase
{
    [Required, StringLength(30), RegularExpression(@"^[A-Za-z0-9_\-]+$", ErrorMessage = "AssetCode solo admite letras, números, '-' y '_'.")]
    public string AssetCode { get; set; } = "";
}

public class UpdateAssetRequest : AssetInputBase
{
    private static readonly Regex RowVerFormat = new(@"^0x[0-9A-Fa-f]{16}$", RegexOptions.Compiled);

    [Required] public string Status { get; set; } = "";
    [StringLength(500)] public string? Notes { get; set; }
    /// <summary>Opcional. Valor RowVer devuelto por GET /api/assets/{id}; activa concurrencia optimista.</summary>
    public string? RowVer { get; set; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        foreach (var r in base.Validate(ctx)) yield return r;
        if (!AssetStatuses.All.Contains(Status))
            yield return new ValidationResult("Status no es válido.", new[] { nameof(Status) });
        if (RowVer is not null && !RowVerFormat.IsMatch(RowVer))
            yield return new ValidationResult("RowVer no tiene un formato válido.", new[] { nameof(RowVer) });
    }
}

public class AssetQuery : PageQuery, IValidatableObject
{
    [StringLength(100)] public string? Search { get; set; }
    public string? Status { get; set; }
    [StringLength(50)] public string? Category { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(Status) && !AssetStatuses.All.Contains(Status))
            yield return new ValidationResult("Status no es válido.", new[] { nameof(Status) });
    }
}

public class AssetDto
{
    public int Id { get; init; }
    public string AssetCode { get; init; } = "";
    public string? SerialNumber { get; init; }
    public string Category { get; init; } = "";
    public string Brand { get; init; } = "";
    public string Model { get; init; } = "";
    public string OwnershipType { get; init; } = "";
    public int? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public string Status { get; init; } = "";
    public string? CurrentLocation { get; init; }
    public DateTime? PurchaseDate { get; init; }
    public DateTime? RentalEndDate { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? RowVer { get; init; }
    public int? AssignedEmployeeId { get; init; }
    public string? AssignedEmployeeName { get; init; }
}

public class AssignAssetRequest
{
    [Range(1, int.MaxValue)] public int EmployeeId { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
}

public class ReturnAssetRequest
{
    [Required, StringLength(200)] public string ReturnCondition { get; set; } = "";
    [StringLength(500)] public string? Notes { get; set; }
}

public class AssignmentResponse { public int AssignmentId { get; init; } }

public class AssetMovementDto
{
    public long Id { get; init; }
    public int AssetId { get; init; }
    public string MovementType { get; init; } = "";
    public string? PreviousValue { get; init; }
    public string? NewValue { get; init; }
    public int? EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public int PerformedByUserId { get; init; }
    public string PerformedBy { get; init; } = "";
    public DateTime PerformedAt { get; init; }
    public string? Notes { get; init; }
}

// ---------- Colaboradores ----------
public class CreateEmployeeRequest
{
    [Required, StringLength(30)] public string EmployeeNumber { get; set; } = "";
    [Required, StringLength(150)] public string FullName { get; set; } = "";
    [Required, StringLength(150), RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "El correo electrónico no tiene un formato válido.")]
    public string Email { get; set; } = "";
}

public class SetActiveRequest { public bool IsActive { get; set; } }

public class EmployeeQuery : PageQuery
{
    [StringLength(100)] public string? Search { get; set; }
    public bool? IsActive { get; set; }
}

public class EmployeeDto
{
    public int Id { get; init; }
    public string EmployeeNumber { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public bool IsActive { get; init; }
}

// ---------- Proveedores ----------
public class CreateSupplierRequest : IValidatableObject
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [StringLength(150), RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "El correo electrónico no tiene un formato válido.")]
    public string? Email { get; set; }
    [StringLength(30), RegularExpression(@"^[0-9+()\-\s]{7,30}$", ErrorMessage = "El teléfono no tiene un formato válido.")]
    public string? Phone { get; set; }
    public List<string> Services { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (Services.Any(s => !ServiceTypes.All.Contains(s)))
            yield return new ValidationResult("Servicios permitidos: Compra, Mantenimiento, Arrendamiento.", new[] { nameof(Services) });
    }
}

public class SupplierQuery : PageQuery
{
    [StringLength(100)] public string? Search { get; set; }
}

public class SupplierDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<string> Services { get; init; } = Array.Empty<string>();
}

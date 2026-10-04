using ITAssets.Api.Data;
using ITAssets.Api.Middleware;
using ITAssets.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAssets.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierRepository _suppliers;
    public SuppliersController(ISupplierRepository suppliers) => _suppliers = suppliers;

    [HttpGet]
    public async Task<ActionResult<PagedResult<SupplierDto>>> GetAll([FromQuery] SupplierQuery query, CancellationToken ct) =>
        Ok(await _suppliers.GetPageAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SupplierDto>> GetById(int id, CancellationToken ct) =>
        Ok(await GetOrThrowAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<SupplierDto>> Create([FromBody] CreateSupplierRequest request, CancellationToken ct)
    {
        var id = await _suppliers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id }, await GetOrThrowAsync(id, ct));
    }

    private async Task<SupplierDto> GetOrThrowAsync(int id, CancellationToken ct) =>
        await _suppliers.GetByIdAsync(id, ct)
        ?? throw new AppException(404, "SUPPLIER_NOT_FOUND", "El proveedor no existe.");
}

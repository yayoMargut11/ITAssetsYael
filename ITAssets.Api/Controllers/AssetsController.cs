using ITAssets.Api.Data;
using ITAssets.Api.Middleware;
using ITAssets.Api.Models;
using ITAssets.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAssets.Api.Controllers;

[ApiController]
[Route("api/assets")]
[Authorize]
public class AssetsController : ControllerBase
{
    private readonly IAssetRepository _assets;
    public AssetsController(IAssetRepository assets) => _assets = assets;

    /// <summary>Lista activos. Ej: GET /api/assets?search=DELL&amp;status=Disponible&amp;category=Laptop&amp;page=1&amp;pageSize=20</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AssetDto>>> GetAll([FromQuery] AssetQuery query, CancellationToken ct) =>
        Ok(await _assets.GetPageAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AssetDto>> GetById(int id, CancellationToken ct) =>
        Ok(await GetOrThrowAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AssetDto>> Create([FromBody] CreateAssetRequest request, CancellationToken ct)
    {
        var id = await _assets.CreateAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(GetById), new { id }, await GetOrThrowAsync(id, ct));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AssetDto>> Update(int id, [FromBody] UpdateAssetRequest request, CancellationToken ct)
    {
        await _assets.UpdateAsync(id, request, User.GetUserId(), ct);
        return Ok(await GetOrThrowAsync(id, ct));
    }

    /// <summary>Asigna el activo a un colaborador. Solo una asignación activa por activo (control de concurrencia en BD).</summary>
    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = Roles.AdminOrOperator)]
    public async Task<ActionResult<AssignmentResponse>> Assign(int id, [FromBody] AssignAssetRequest request, CancellationToken ct)
    {
        var assignmentId = await _assets.AssignAsync(id, request, User.GetUserId(), ct);
        return StatusCode(StatusCodes.Status201Created, new AssignmentResponse { AssignmentId = assignmentId });
    }

    [HttpPost("{id:int}/return")]
    [Authorize(Roles = Roles.AdminOrOperator)]
    public async Task<ActionResult<AssignmentResponse>> Return(int id, [FromBody] ReturnAssetRequest request, CancellationToken ct) =>
        Ok(new AssignmentResponse { AssignmentId = await _assets.ReturnAsync(id, request, User.GetUserId(), ct) });

    /// <summary>Historial (trazabilidad) del activo, del más reciente al más antiguo.</summary>
    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<PagedResult<AssetMovementDto>>> History(
        int id, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await _assets.GetHistoryAsync(id, paging.Page, paging.PageSize, ct));

    private async Task<AssetDto> GetOrThrowAsync(int id, CancellationToken ct) =>
        await _assets.GetByIdAsync(id, ct)
        ?? throw new AppException(404, "ASSET_NOT_FOUND", "El activo no existe.");
}

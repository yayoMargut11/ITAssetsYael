using ITAssets.Api.Data;
using ITAssets.Api.Middleware;
using ITAssets.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAssets.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeRepository _employees;
    public EmployeesController(IEmployeeRepository employees) => _employees = employees;

    [HttpGet]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> GetAll([FromQuery] EmployeeQuery query, CancellationToken ct) =>
        Ok(await _employees.GetPageAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> GetById(int id, CancellationToken ct) =>
        Ok(await GetOrThrowAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.AdminOrOperator)]
    public async Task<ActionResult<EmployeeDto>> Create([FromBody] CreateEmployeeRequest request, CancellationToken ct)
    {
        var id = await _employees.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id }, await GetOrThrowAsync(id, ct));
    }

    /// <summary>Activa o desactiva un colaborador (un colaborador inactivo no puede recibir activos).</summary>
    [HttpPatch("{id:int}/active")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<EmployeeDto>> SetActive(int id, [FromBody] SetActiveRequest request, CancellationToken ct)
    {
        await _employees.SetActiveAsync(id, request.IsActive, ct);
        return Ok(await GetOrThrowAsync(id, ct));
    }

    private async Task<EmployeeDto> GetOrThrowAsync(int id, CancellationToken ct) =>
        await _employees.GetByIdAsync(id, ct)
        ?? throw new AppException(404, "EMPLOYEE_NOT_FOUND", "El colaborador no existe.");
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Auth;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Drugs;

namespace PharmaCore.Api.Controllers;

[ApiController, Route("api/drug-reference")]
public sealed class DrugReferenceController(DrugReferenceService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissions.DrugRefRead)]
    public Task<PagedResult<DrugReferenceDto>> Search([FromQuery] string? search, [FromQuery] bool drugsOnly = true,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => service.SearchAsync(search, drugsOnly, page, pageSize, ct);

    /// <summary>Suggestions only: same normalized active ingredients, same parsed strength, same route. A pharmacist must verify.</summary>
    [HttpGet("{id:guid}/alternatives"), Authorize(Policy = Permissions.DrugRefRead)]
    public Task<IReadOnlyList<DrugReferenceDto>> Alternatives(Guid id, CancellationToken ct)
        => service.GetAlternativesAsync(id, ct);

    [HttpPost("import"), Authorize(Policy = Permissions.DrugRefImport), RequestSizeLimit(26_214_400)]
    public async Task<ActionResult<DrugImportReport>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return Problem("A CSV file is required.", statusCode: 400);
        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return Problem("Only .csv files are supported for now.", statusCode: 400);
        var userId = Guid.Parse(User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
        await using var stream = file.OpenReadStream();
        return await service.ImportCsvAsync(userId, file.FileName, stream, ct);
    }
}

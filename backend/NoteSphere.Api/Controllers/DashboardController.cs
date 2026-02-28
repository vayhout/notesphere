using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoteSphere.Api.Dtos;
using NoteSphere.Api.Services;

namespace NoteSphere.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly NoteService _notes;

    public DashboardController(NoteService notes)
    {
        _notes = notes;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
    {
        var userId = GetUserId();
        return Ok(await _notes.GetStatsAsync(userId));
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (sub is null) throw new UnauthorizedAccessException("Missing user id claim.");
        return Guid.Parse(sub);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoteSphere.Api.Dtos;
using NoteSphere.Api.Models;
using NoteSphere.Api.Services;

namespace NoteSphere.Api.Controllers;

[ApiController]
[Route("api/notes")]
[Authorize]
public sealed class NotesController : ControllerBase
{
    private readonly NoteService _notes;

    public NotesController(NoteService notes)
    {
        _notes = notes;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<NoteDto>>> Search([FromQuery] NoteQuery query)
    {
        var userId = GetUserId();
        var res = await _notes.SearchAsync(userId, query);
        return Ok(res);
    }

// Trash: list only soft-deleted notes (for restore/purge)
[HttpGet("trash")]
public async Task<ActionResult<PagedResult<NoteDto>>> Trash([FromQuery] NoteQuery query)
{
    var userId = GetUserId();
    query.OnlyDeleted = true;
    query.IncludeDeleted = true;
    var res = await _notes.SearchAsync(userId, query);
    return Ok(res);
}

[HttpPost("{id:guid}/restore")]
public async Task<IActionResult> Restore(Guid id)
{
    var userId = GetUserId();
    await _notes.RestoreAsync(userId, id, GetAuditContext());
    return NoContent();
}

[HttpDelete("{id:guid}/purge")]
public async Task<IActionResult> Purge(Guid id)
{
    var userId = GetUserId();
    await _notes.PurgeAsync(userId, id, GetAuditContext());
    return NoContent();
}

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoteDto>> Get(Guid id)
    {
        try
        {
            var userId = GetUserId();
            return Ok(await _notes.GetAsync(userId, id));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<NoteDto>> Create([FromBody] NoteCreateRequest req)
    {
        try
        {
            var userId = GetUserId();
            var created = await _notes.CreateAsync(userId, req, GetAuditContext());
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NoteDto>> Update(Guid id, [FromBody] NoteUpdateRequest req)
    {
        try
        {
            var userId = GetUserId();
            return Ok(await _notes.UpdateAsync(userId, id, req, GetAuditContext()));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        await _notes.DeleteAsync(userId, id, GetAuditContext());
        return NoContent();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (sub is null) throw new UnauthorizedAccessException("Missing user id claim.");
        return Guid.Parse(sub);
    }

    private AuditContext GetAuditContext()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        return new AuditContext(ip, string.IsNullOrWhiteSpace(ua) ? null : ua);
    }
}

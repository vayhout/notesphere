using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoteSphere.Api.Dtos;

namespace NoteSphere.Api.Controllers;

[ApiController]
[Route("api/uploads")]
[Authorize]
public sealed class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public UploadsController(IWebHostEnvironment env)
    {
        _env = env;
    }

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp", "image/gif"
    };

    [HttpPost("image")]
    [RequestSizeLimit(10_000_000)] // 10MB
    public async Task<ActionResult<UploadResultDto>> UploadImage([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        if (!Allowed.Contains(file.ContentType))
            return BadRequest(new { message = "Only images are allowed (png, jpg, webp, gif)." });

        if (file.Length > 10_000_000)
            return BadRequest(new { message = "Max file size is 10MB." });

        var userId = GetUserId();

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = file.ContentType switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".img"
            };
        }

        var safeName = $"{Guid.NewGuid():N}{ext}".ToLowerInvariant();

        // Always write under wwwroot/uploads/{userIdN}
        var userFolder = userId.ToString("N");
        var relDir = Path.Combine("uploads", userFolder);

        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");

        var root = Path.Combine(webRoot, relDir);
        Directory.CreateDirectory(root);

        var fullPath = Path.Combine(root, safeName);
        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        // URL that matches static files middleware
        var url = $"/uploads/{userFolder}/{safeName}";
        return Ok(new UploadResultDto(url, file.FileName, file.Length));
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (sub is null) throw new UnauthorizedAccessException("Missing user id claim.");
        return Guid.Parse(sub);
    }
}

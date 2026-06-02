using GBILET.Core.DTOs.Blog;
using GBILET.Core.Service.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GBILET.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/blog")]
public class AdminBlogController(
    IBlogService blogService,
    IImageProcessor imageProcessor,
    ILogger<AdminBlogController> logger,
    IWebHostEnvironment env) : ControllerBase
{
    // ============================================================
    // PUBLIC — Frontend blog sayfaları buradan okur (auth gerekmez)
    // ============================================================

    [HttpGet("public")]
    [EnableRateLimiting("fixed")]
    public async Task<IActionResult> GetPublished(CancellationToken ct)
    {
        var posts = await blogService.GetAllAsync(publishedOnly: true, ct);
        return Ok(posts);
    }

    [HttpGet("public/{slug}")]
    [EnableRateLimiting("fixed")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        var post = await blogService.GetBySlugAsync(slug, ct);
        if (post is null)
            return NotFound(new { error = "Blog yazısı bulunamadı." });
        return Ok(post);
    }

    // ============================================================
    // ADMIN — SuperAdmin veya BlogEditor
    // ============================================================

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,BlogEditor")]
    [EnableRateLimiting("admin-general")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var posts = await blogService.GetAllAsync(publishedOnly: false, ct);
        return Ok(posts);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "SuperAdmin,BlogEditor")]
    [EnableRateLimiting("admin-general")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var post = await blogService.GetByIdAsync(id, ct);
        if (post is null)
            return NotFound(new { error = "Blog yazısı bulunamadı." });
        return Ok(post);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,BlogEditor")]
    [EnableRateLimiting("admin-general")]
    public async Task<IActionResult> Create([FromBody] CreateBlogPostRequest request, CancellationToken ct)
    {
        try
        {
            var post = await blogService.CreateAsync(request, ct);
            return Ok(post);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "SuperAdmin,BlogEditor")]
    [EnableRateLimiting("admin-general")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBlogPostRequest request, CancellationToken ct)
    {
        try
        {
            var post = await blogService.UpdateAsync(id, request, ct);
            return Ok(post);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "SuperAdmin,BlogEditor")]
    [EnableRateLimiting("admin-general")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await blogService.DeleteAsync(id, ct);
            return Ok(new { message = "Blog yazısı silindi." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ============================================================
    // Görsel yükleme
    // ============================================================

    [HttpPost("upload-image")]
    [Authorize(Roles = "SuperAdmin,BlogEditor")]
    [EnableRateLimiting("admin-general")]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Dosya seçilmedi." });

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return BadRequest(new { error = "Sadece JPG, PNG ve WebP desteklenir." });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "Dosya 5MB'den büyük olamaz." });

        try
        {
            var webRoot = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var uploadPath = Path.Combine(webRoot, "uploads", "blog");
            Directory.CreateDirectory(uploadPath);

            // Downscale to max 1600px wide and re-encode as compressed WebP so the
            // stored file is a few hundred KB instead of the raw multi-MB upload.
            await using var source = file.OpenReadStream();
            var (data, outExt) = await imageProcessor.ProcessAsync(source, maxWidth: 1600, quality: 80, ct);

            var fileName = $"{Guid.NewGuid()}{outExt}";
            var fullPath = Path.Combine(uploadPath, fileName);

            await System.IO.File.WriteAllBytesAsync(fullPath, data, ct);

            return Ok(new { url = $"/uploads/blog/{fileName}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Görsel yükleme hatası");
            return StatusCode(500, new { error = "Görsel yüklenemedi." });
        }
    }
}

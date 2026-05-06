using Microsoft.AspNetCore.Mvc;

namespace TourGuideApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public UploadsController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile upload)
    {
        if (upload == null || upload.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(upload.FileName)}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await upload.CopyToAsync(stream);
        }

        var url = $"/uploads/{fileName}";

        // CKEditor 5 expects a specific response format for its SimpleUploadAdapter
        return Ok(new
        {
            url = url
        });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using TouristGuideWeb.Models;

namespace TouristGuideWeb.Controllers;

[AllowAnonymous]
public sealed class ApkController : Controller
{
    private const string RelativeLatestApkPath = "apk/latest.apk";
    private const string RelativeCurrentMetadataPath = "apk/current.json";
    private const string RelativeReleaseDirectoryPath = "apk/releases";
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public ApkController(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        try
        {
            var githubUrl = _configuration["DownloadSettings:GithubApkUrl"];
            bool isGithub = !string.IsNullOrWhiteSpace(githubUrl);

            ApkDownloadViewModel model;

            if (isGithub)
            {
                model = new ApkDownloadViewModel
                {
                    LandingUrl = BuildPublicUrl("/apk"),
                    DirectApkUrl = githubUrl!,
                    QrImageUrl = BuildPublicUrl("/apk/qr.png"),
                    HasApk = true,
                    LastUpdatedUtc = null,
                    SizeBytes = 0,
                    CurrentFileName = "Tải từ GitHub"
                };
            }
            else
            {
                var current = ReadCurrentRelease();
                var apkFile = current?.FileName is not null
                    ? GetReleaseApkFileInfo(current.FileName)
                    : GetLatestApkFileInfo();

                var directPath = current?.FileName is not null
                    ? BuildDirectDownloadPath(current.FileName)
                    : "/apk/latest";

                model = new ApkDownloadViewModel
                {
                    LandingUrl = BuildPublicUrl("/apk"),
                    DirectApkUrl = BuildPublicUrl(directPath),
                    QrImageUrl = BuildPublicUrl("/apk/qr.png"),
                    HasApk = apkFile is not null,
                    LastUpdatedUtc = apkFile?.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
                    SizeBytes = apkFile?.Length ?? 0,
                    CurrentFileName = current?.FileName,
                };
            }

            return View(model);
        }
        catch (Exception ex)
        {
            var fallbackHtml = $@"<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <title>Tải APK - Vĩnh Khánh</title>
    <style>
        body {{ margin:0; font-family: Arial, sans-serif; background:#f8fafc; color:#111827; }}
        .wrap {{ max-width:900px; margin:0 auto; padding:32px 16px; }}
        .card {{ background:#fff; border:1px solid #e5e7eb; border-radius:18px; box-shadow:0 10px 24px rgba(15,23,42,.08); padding:24px; }}
        .btn {{ display:inline-block; padding:12px 18px; border-radius:999px; text-decoration:none; color:#fff; background:linear-gradient(90deg,#16a34a,#0ea5e9); font-weight:700; }}
        .muted {{ color:#6b7280; }}
        .title {{ font-size:28px; font-weight:800; margin:0 0 8px; }}
    </style>
</head>
<body>
    <div class='wrap'>
        <div class='card'>
            <h1 class='title'>Tải APK bằng QR động</h1>
            <p class='muted'>Trang đang ở chế độ fallback để tránh lỗi 500. Hãy tải APK bằng link trực tiếp bên dưới.</p>
            <p><a class='btn' href='/apk/latest'>Tải APK mới nhất</a></p>
            <p class='muted' style='word-break:break-word;'>Lỗi chi tiết: {System.Net.WebUtility.HtmlEncode(ex.Message)}</p>
        </div>
    </div>
</body>
</html>";

            return Content(fallbackHtml, "text/html; charset=utf-8");
        }
    }

    public IActionResult DownloadLatest()
    {
        var githubUrl = _configuration["DownloadSettings:GithubApkUrl"];
        if (!string.IsNullOrWhiteSpace(githubUrl))
        {
            return Redirect(githubUrl);
        }

        var current = ReadCurrentRelease();
        if (current?.FileName is not null)
        {
            return RedirectToAction(nameof(DownloadByFileName), new { fileName = current.FileName });
        }

        var apkFile = GetLatestApkFileInfo();
        if (apkFile is null)
        {
            return NotFound("Latest APK is not uploaded yet. Run publish-apk.ps1 to publish one.");
        }

        return PhysicalFile(
            apkFile.FullName,
            "application/vnd.android.package-archive",
            "TouristGuideApp-latest.apk");
    }

    public IActionResult DownloadByFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("fileName is required.");
        }

        var safeFileName = Path.GetFileName(fileName);
        if (!string.Equals(safeFileName, fileName, StringComparison.Ordinal) ||
            !safeFileName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid APK file name.");
        }

        var apkFile = GetReleaseApkFileInfo(safeFileName);
        if (apkFile is null)
        {
            return NotFound("Requested APK file not found.");
        }

        return PhysicalFile(
            apkFile.FullName,
            "application/vnd.android.package-archive",
            safeFileName);
    }

    public IActionResult QrPng()
    {
        string qrTarget;
        var githubUrl = _configuration["DownloadSettings:GithubApkUrl"];
        
        if (!string.IsNullOrWhiteSpace(githubUrl))
        {
            qrTarget = githubUrl;
        }
        else
        {
            var current = ReadCurrentRelease();
            qrTarget = current?.FileName is not null
                ? BuildPublicUrl(BuildDirectDownloadPath(current.FileName))
                : BuildPublicUrl("/apk/latest");
        }

        using var qrGenerator = new QRCodeGenerator();
        using var codeData = qrGenerator.CreateQrCode(qrTarget, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(codeData);
        var bytes = qrCode.GetGraphic(20);

        return File(bytes, "image/png");
    }

    private FileInfo? GetLatestApkFileInfo()
    {
        var webRootPath = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            var contentPath = _environment.ContentRootPath;
            webRootPath = Path.Combine(contentPath, "wwwroot");
            if (!Directory.Exists(webRootPath))
            {
                return null;
            }
        }

        var fullPath = Path.Combine(webRootPath, RelativeLatestApkPath);
        try
        {
            if (!System.IO.File.Exists(fullPath))
            {
                return null;
            }

            return new FileInfo(fullPath);
        }
        catch
        {
            return null;
        }
    }

    private FileInfo? GetReleaseApkFileInfo(string fileName)
    {
        var webRootPath = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            var contentPath = _environment.ContentRootPath;
            webRootPath = Path.Combine(contentPath, "wwwroot");
            if (!Directory.Exists(webRootPath))
            {
                return null;
            }
        }

        var safeFileName = Path.GetFileName(fileName);
        var fullPath = Path.Combine(webRootPath, RelativeReleaseDirectoryPath, safeFileName);
        try
        {
            if (!System.IO.File.Exists(fullPath))
            {
                return null;
            }

            return new FileInfo(fullPath);
        }
        catch
        {
            return null;
        }
    }

    private CurrentReleaseMetadata? ReadCurrentRelease()
    {
        var webRootPath = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            var contentPath = _environment.ContentRootPath;
            webRootPath = Path.Combine(contentPath, "wwwroot");
            if (!Directory.Exists(webRootPath))
            {
                return null;
            }
        }

        var metadataPath = Path.Combine(webRootPath, RelativeCurrentMetadataPath);
        try
        {
            if (!System.IO.File.Exists(metadataPath))
            {
                return null;
            }

            var json = System.IO.File.ReadAllText(metadataPath);
            return System.Text.Json.JsonSerializer.Deserialize<CurrentReleaseMetadata>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildDirectDownloadPath(string fileName)
    {
        var encodedFileName = Uri.EscapeDataString(fileName);
        return $"/apk/direct/{encodedFileName}";
    }

    private static string BuildIndexHtml(ApkDownloadViewModel model)
    {
        var fileSizeMb = model.SizeBytes > 0
            ? (model.SizeBytes / (1024d * 1024d)).ToString("0.00")
            : "0.00";

        var apkStatusHtml = model.HasApk
            ? $@"<div style='padding:16px;border:1px solid #b7e4c7;background:#ecfdf3;border-radius:12px;margin-top:16px;'>
                    <div><strong>Sẵn sàng tải:</strong> Có bản APK publish mới nhất trên server.</div>
                    <div><strong>Dung lượng:</strong> {fileSizeMb} MB</div>
                    <div><strong>Cập nhật lúc:</strong> {System.Net.WebUtility.HtmlEncode(model.LastUpdatedUtc ?? string.Empty)}</div>
                </div>"
            : "<div style='padding:16px;border:1px solid #fde68a;background:#fffbeb;border-radius:12px;margin-top:16px;'>Chưa có APK được publish. Hãy publish APK bằng script ở bên dưới.</div>";

        var currentFileHtml = string.IsNullOrWhiteSpace(model.CurrentFileName)
            ? string.Empty
            : $"<div style='margin-top:12px;color:#6b7280;font-size:14px;'>File hiện tại: <strong>{System.Net.WebUtility.HtmlEncode(model.CurrentFileName)}</strong></div>";

        return $@"<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <title>Tải APK - Vĩnh Khánh</title>
    <style>
        body {{ margin:0; font-family: Arial, sans-serif; background:#f8fafc; color:#111827; }}
        .wrap {{ max-width:1100px; margin:0 auto; padding:32px 16px; }}
        .card {{ background:#fff; border:1px solid #e5e7eb; border-radius:18px; box-shadow:0 10px 24px rgba(15,23,42,.08); padding:24px; margin-bottom:20px; }}
        .grid {{ display:grid; grid-template-columns: 1fr 2fr; gap:20px; }}
        .btn {{ display:inline-block; padding:12px 18px; border-radius:999px; text-decoration:none; color:#fff; background:linear-gradient(90deg,#16a34a,#0ea5e9); font-weight:700; }}
        .muted {{ color:#6b7280; }}
        .title {{ font-size:28px; font-weight:800; margin:0 0 8px; }}
        .sub {{ color:#4b5563; margin:0; }}
        .qr {{ max-width:260px; width:100%; border:1px solid #e5e7eb; border-radius:12px; }}
        @media (max-width: 900px) {{ .grid {{ grid-template-columns: 1fr; }} }}
    </style>
</head>
<body>
    <div class='wrap'>
        <div class='card'>
            <div style='display:flex;justify-content:space-between;gap:16px;align-items:center;flex-wrap:wrap;'>
                <div>
                    <h1 class='title'>Tải APK bằng QR động</h1>
                    <p class='sub'>Mỗi lần publish APK mới, QR được cập nhật theo link tải trực tiếp của bản phát hành mới nhất.</p>
                </div>
                <a class='btn' href='{System.Net.WebUtility.HtmlEncode(model.DirectApkUrl)}'>Tải APK mới nhất</a>
            </div>
        </div>

        <div class='grid'>
            <div class='card' style='text-align:center;'>
                <h2 style='margin-top:0;'>QR tải APK</h2>
                <img class='qr' src='{System.Net.WebUtility.HtmlEncode(model.QrImageUrl)}' alt='QR tải APK' />
                <div class='muted' style='margin-top:12px;'>QR hiện tại trỏ tới link tải trực tiếp:</div>
                <div style='word-break:break-all; margin-top:8px;'><a href='{System.Net.WebUtility.HtmlEncode(model.DirectApkUrl)}'>{System.Net.WebUtility.HtmlEncode(model.DirectApkUrl)}</a></div>
                {currentFileHtml}
            </div>

            <div class='card'>
                <h2 style='margin-top:0;'>Trạng thái bản APK hiện tại</h2>
                {apkStatusHtml}
                <h3>Quy trình publish QR động</h3>
                <ol>
                    <li>Build APK mới từ project MAUI.</li>
                    <li>Chạy script publish, hệ thống tạo bản phát hành mới và cập nhật QR theo bản đó.</li>
                    <li>Người dùng quét QR hiện tại sẽ tải trực tiếp APK của bản publish gần nhất.</li>
                </ol>
                <div style='margin-top:16px;padding:16px;border:1px solid #e5e7eb;border-radius:12px;background:#f9fafb;'>
                    <div style='font-weight:700;margin-bottom:8px;'>Lệnh publish:</div>
                    <code>powershell -ExecutionPolicy Bypass -File .\publish-apk.ps1 -ApkPath &quot;C:\duong-dan-that\TouristGuideApp.apk&quot;</code>
                </div>
            </div>
        </div>
    </div>
</body>
</html>";
    }
    private string BuildPublicUrl(string relativePath)
    {
        var configuredBaseUrl = _configuration["DownloadSettings:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl) &&
            Uri.TryCreate(configuredBaseUrl.Trim(), UriKind.Absolute, out var baseUri))
        {
            var normalizedBase = EnsureTrailingSlash(baseUri);
            return new Uri(normalizedBase, relativePath.TrimStart('/')).AbsoluteUri;
        }

        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}{relativePath}";
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var absoluteUri = uri.AbsoluteUri;
        return absoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(absoluteUri + "/");
    }

    private sealed class CurrentReleaseMetadata
    {
        public string? FileName { get; set; }
    }
}

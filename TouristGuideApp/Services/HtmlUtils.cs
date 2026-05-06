using System.Text.RegularExpressions;
using System.Net;
using System.Linq;

namespace TouristGuideApp.Services;

public static class HtmlUtils
{
    public static string EnsureAbsoluteUrl(string? url, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var trimmedUrl = url.Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return trimmedUrl;
        }

        // If it's already an absolute URL, check if it's pointing to a loopback address
        if (Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var absoluteUri))
        {
            var host = absoluteUri.Host.ToLowerInvariant();
            if (host == "localhost" || host == "127.0.0.1" || host == "::1" || host == "10.0.2.2")
            {
                // Rewrite loopback to the reachable API host
                var builder = new UriBuilder(absoluteUri)
                {
                    Host = baseUri.Host,
                    Port = baseUri.Port,
                    Scheme = baseUri.Scheme
                };
                return builder.Uri.AbsoluteUri;
            }
            return trimmedUrl;
        }

        if (Uri.TryCreate(baseUri, trimmedUrl, out var resolvedUri))
        {
            return RewriteLoopbackToApiHost(resolvedUri, baseUrl);
        }

        return trimmedUrl;
    }

    /// <summary>
    /// Processes HTML content and ensures all image sources are absolute.
    /// This handles src, srcset, data-src, and CSS background-image.
    /// </summary>
    public static string FixImageUrls(string html, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        // 1. Handle src, data-src, and srcset attributes
        // Regex matches (attr)="url" or (attr)='url'
        string attrPattern = @"(src|data-src|srcset)\s*=\s*([""'])(.*?)\2";
        
        var processed = Regex.Replace(html, attrPattern, match =>
        {
            var attrName = match.Groups[1].Value.ToLowerInvariant();
            var quote = match.Groups[2].Value;
            var urlValue = match.Groups[3].Value;

            if (string.IsNullOrWhiteSpace(urlValue)) return match.Value;

            if (attrName == "srcset")
            {
                // srcset can contain multiple comma-separated URLs with descriptors (e.g., "img1.jpg 320w, img2.jpg 480w")
                var parts = urlValue.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i].Trim();
                    // Each part is "url descriptor" or just "url"
                    var subParts = part.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (subParts.Length > 0)
                    {
                        var absoluteUrl = EnsureAbsoluteUrl(subParts[0], baseUrl);
                        parts[i] = absoluteUrl + (subParts.Length > 1 ? " " + string.Join(" ", subParts.Skip(1)) : "");
                    }
                }
                return $"{attrName}={quote}{string.Join(", ", parts)}{quote}";
            }
            else
            {
                var absoluteUrl = EnsureAbsoluteUrl(urlValue, baseUrl);
                return $"{attrName}={quote}{absoluteUrl}{quote}";
            }
        }, RegexOptions.IgnoreCase);

        // 2. Handle background-image: url(...) in style attributes or style tags
        string cssPattern = @"url\s*\(\s*([""']?)(.*?)\1\s*\)";
        processed = Regex.Replace(processed, cssPattern, match =>
        {
            var quote = match.Groups[1].Value;
            var urlValue = match.Groups[2].Value;

            if (string.IsNullOrWhiteSpace(urlValue) || urlValue.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) 
                return match.Value;

            var absoluteUrl = EnsureAbsoluteUrl(urlValue, baseUrl);
            return $"url({quote}{absoluteUrl}{quote})";
        }, RegexOptions.IgnoreCase);

        return processed;
    }

    public static string NormalizeDescriptionHtml(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "<p>Không có mô tả.</p>";
        }

        var decoded = WebUtility.HtmlDecode(content).Trim();
        var looksLikeHtml = decoded.Contains('<') && decoded.Contains('>');
        if (looksLikeHtml)
        {
            return decoded;
        }

        var escaped = WebUtility.HtmlEncode(decoded).Replace("\n", "<br/>");
        return $"<p>{escaped}</p>";
    }

    private static string RewriteLoopbackToApiHost(Uri originalUri, string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return originalUri.AbsoluteUri;
        }

        var host = originalUri.Host.ToLowerInvariant();
        var isLoopbackHost = host is "localhost" or "127.0.0.1" or "::1" or "10.0.2.2";
        if (!isLoopbackHost)
        {
            return originalUri.AbsoluteUri;
        }

        var builder = new UriBuilder(originalUri)
        {
            Scheme = baseUri.Scheme,
            Host = baseUri.Host,
            Port = baseUri.IsDefaultPort ? -1 : baseUri.Port
        };

        return builder.Uri.AbsoluteUri;
    }

    /// <summary>
    /// Wraps HTML content in a robust structure for high-quality mobile rendering.
    /// Includes auto-scaling images, smooth fonts, and a script to handle image errors.
    /// </summary>
    public static string WrapInMobileLayout(string content)
    {
        return $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='utf-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=2.0, user-scalable=yes'>
            <style>
                :root {{
                    color-scheme: light;
                }}
                body {{
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
                    font-size: 16px;
                    line-height: 1.6;
                    color: #333333;
                    margin: 0;
                    padding: 4px;
                    background-color: transparent;
                    word-wrap: break-word;
                }}
                img {{
                    max-width: 100% !important;
                    height: auto !important;
                    border-radius: 12px;
                    margin: 16px 0;
                    display: block;
                    box-shadow: 0 4px 12px rgba(0,0,0,0.08);
                    background-color: #f0f0f0; /* Placeholder while loading */
                }}
                p {{ margin-bottom: 16px; }}
                h1, h2, h3 {{ color: #2C4C3B; margin-top: 24px; }}
                a {{ color: #B84A39; text-decoration: none; }}
            </style>
        </head>
        <body>
            {content}
            <script>
                // Auto-fix for images that fail to load
                document.querySelectorAll('img').forEach(img => {{
                    img.onerror = function() {{
                        console.log('Image failed to load:', this.src);
                        this.style.display = 'none'; // Hide broken images instead of showing icon
                    }};
                    
                    // Force refresh images if they are still loading after 2 seconds
                    if (!img.complete) {{
                        setTimeout(() => {{
                            if (!img.complete) img.src = img.src; 
                        }}, 2000);
                    }}
                }});
            </script>
        </body>
        </html>";
    }
}

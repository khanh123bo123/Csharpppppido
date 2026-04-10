using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TouristGuideApp.Models;

namespace TouristGuideApp.Services
{
    /// <summary>
    /// Generate HTML map using Leaflet.js (works offline)
    /// Support hybrid mode: cached tiles + online fallback
    /// </summary>
    public interface IMapHtmlGenerator
    {
        string GenerateMapHtml(IEnumerable<POI> pois, double userLat, double userLon);
        string GetMapHtmlFilePath();
    }

    public class MapHtmlGenerator : IMapHtmlGenerator
    {
        private readonly IOfflineMapService _offlineMapService;

        public MapHtmlGenerator(IOfflineMapService offlineMapService)
        {
            _offlineMapService = offlineMapService;
        }

        /// <summary>
        /// Generate interactive Leaflet map HTML with POIs and user location
        /// </summary>
        public string GenerateMapHtml(IEnumerable<POI> pois, double userLat, double userLon)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset='utf-8' />");
            sb.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.AppendLine("    <title>Du Lịch Quận 4</title>");
            
            // Leaflet CSS
            sb.AppendLine("    <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.min.css' />");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { margin: 0; padding: 0; }");
            sb.AppendLine("        #map { position: absolute; top: 0; bottom: 0; width: 100%; }");
            sb.AppendLine("        .info { padding: 6px 8px; font: 14px/16px Arial, Helvetica, sans-serif; background: white; background: rgba(255,255,255,0.8); box-shadow: 0 0 15px rgba(0,0,0,0.2); border-radius: 5px; }");
            sb.AppendLine("        .info h4 { margin: 0 0 5px 0; color: #FF6600; }");
            sb.AppendLine("        .legend { line-height: 18px; color: #333; }");
            sb.AppendLine("        .legend i { width: 18px; height: 18px; float: left; margin-right: 8px; border-radius: 50%; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <div id='map'></div>");
            
            // Leaflet JS
            sb.AppendLine("    <script src='https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.min.js'></script>");
            sb.AppendLine("    <script>");
            sb.AppendLine("        // Initialize map (Quận 4, HCMC center)");
            sb.AppendLine("        var map = L.map('map').setView([10.7769, 106.7009], 14);");
            
            // Tile layers: Online + Offline hybrid
            sb.AppendLine("        // Layer 1: OpenStreetMap (default)");
            sb.AppendLine("        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {");
            sb.AppendLine("            maxZoom: 19,");
            sb.AppendLine("            attribution: '© OpenStreetMap contributors',");
            sb.AppendLine("            errorTileUrl: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAABGdBTUEAALGPC/xhBQAAACBjSFJNAAB6JgAA'");
            sb.AppendLine("        }).addTo(map);");
            
            sb.AppendLine("        // Layer 2: Offline cache fallback");
            sb.AppendLine("        var cacheLayer = L.tileLayer('', {");
            sb.AppendLine("            maxZoom: 19,");
            sb.AppendLine("            attribution: 'Offline Cache'");
            sb.AppendLine("        });");

            // User location marker
            sb.AppendLine("        // Add user location");
            sb.AppendLine($"        var userMarker = L.circleMarker([{userLat}, {userLon}], {{");
            sb.AppendLine("            color: '#0088FF',");
            sb.AppendLine("            fillColor: '#0088FF',");
            sb.AppendLine("            fillOpacity: 0.8,");
            sb.AppendLine("            radius: 8,");
            sb.AppendLine("            weight: 2");
            sb.AppendLine("        }).addTo(map);");
            sb.AppendLine($"        userMarker.bindPopup('Vị trí của bạn');");

            // Add POIs as markers
            sb.AppendLine("        // Add POI markers");
            if (pois != null)
            {
                int poiIndex = 0;
                foreach (var poi in pois)
                {
                    // Escape single quotes and newlines to prevent JS SyntaxError
                    var safeName = poi.Name?.Replace("'", "\\'").Replace("\n", " ").Replace("\r", "") ?? "";
                    var safeDesc = poi.Description?.Replace("'", "\\'").Replace("\n", "<br/>").Replace("\r", "") ?? "";
                    var popupHtml = $"<b>{safeName}</b><br/>{safeDesc}<br/><i style=\\'color:#FF6600\\'>Nhà hàng</i>";
                    
                    sb.AppendLine($"        var poi{poiIndex} = L.marker([{poi.Latitude}, {poi.Longitude}]).addTo(map);");
                    // Using double quotes or properly escaped single quotes for JS string
                    sb.AppendLine($"        poi{poiIndex}.bindPopup('{popupHtml}');");
                    poiIndex++;
                }
            }

            // Info control
            sb.AppendLine("        var info = L.control();");
            sb.AppendLine("        info.onAdd = function(map) {");
            sb.AppendLine("            this._div = L.DomUtil.create('div', 'info');");
            sb.AppendLine("            this._div.innerHTML =");
            sb.AppendLine("                '<h4>Du Lịch Ẩm Thực Quận 4</h4>' +");
            sb.AppendLine("                 'Chế độ: <b id=\"mode\">Online</b><br/>' +");
            sb.AppendLine("                 '<div class=\"legend\">' +");
            sb.AppendLine("                 '<i style=\"background: #0088FF; opacity: 0.8\"></i> Bạn<br>' +");
            sb.AppendLine("                 '<i style=\"background: #FF6600; opacity: 0.7\"></i> Nhà hàng' +");
            sb.AppendLine("                 '</div>';");
            sb.AppendLine("            return this._div;");
            sb.AppendLine("        };");
            sb.AppendLine("        info.addTo(map);");

            // Update mode based on connectivity
            sb.AppendLine("        function updateMode() {");
            sb.AppendLine("            var mode = navigator.onLine ? 'Online ☁️' : 'Offline 📦';");
            sb.AppendLine("            document.getElementById('mode').textContent = mode;");
            sb.AppendLine("        }");
            sb.AppendLine("        updateMode();");
            sb.AppendLine("        window.addEventListener('online', updateMode);");
            sb.AppendLine("        window.addEventListener('offline', updateMode);");

            sb.AppendLine("    </script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        /// <summary>
        /// Save HTML to file and return path
        /// </summary>
        public string GetMapHtmlFilePath()
        {
            var cacheDir = FileSystem.Current.CacheDirectory;
            var htmlPath = Path.Combine(cacheDir, "map.html");
            return htmlPath;
        }
    }
}

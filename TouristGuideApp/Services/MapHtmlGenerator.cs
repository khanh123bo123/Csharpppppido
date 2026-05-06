using System;
using System.Collections.Generic;
using System.Text;
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
        string GenerateTourMapHtml(string tourName, IEnumerable<POI> tourPois, double userLat, double userLon);
        string GetMapHtmlFilePath();
    }

    public class MapHtmlGenerator : IMapHtmlGenerator
    {
        private readonly IOfflineMapService _offlineMapService;

        public MapHtmlGenerator(IOfflineMapService offlineMapService)
        {
            _offlineMapService = offlineMapService;
        }

        public string GenerateMapHtml(IEnumerable<POI> pois, double userLat, double userLon)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("    <meta charset='utf-8' />");
            sb.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.AppendLine("    <title>Du L\u1ecbch Qu\u1eadn 4</title>");
            sb.AppendLine("    <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.min.css' />");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { margin:0; padding:0; }");
            sb.AppendLine("        #map { position:absolute; top:0; bottom:0; width:100%; }");
            sb.AppendLine("        .custom-tooltip {");
            sb.AppendLine("            background: white;");
            sb.AppendLine("            border: 2px solid #B84A39;");
            sb.AppendLine("            border-radius: 8px;");
            sb.AppendLine("            color: #2C4C3B;");
            sb.AppendLine("            font-weight: bold;");
            sb.AppendLine("            padding: 4px 8px;");
            sb.AppendLine("            box-shadow: 0 2px 6px rgba(0,0,0,0.15);");
            sb.AppendLine("            font-size: 11px;");
            sb.AppendLine("        }");
            sb.AppendLine("        .leaflet-tooltip-top:before {");
            sb.AppendLine("            border-top-color: #B84A39;");
            sb.AppendLine("        }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine("    <div id='map'></div>");
            sb.AppendLine("    <script src='https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.min.js'></script>");
            sb.AppendLine("    <script>");
            sb.AppendLine("        var map = L.map('map', { zoomControl: false }).setView([10.7769, 106.7009], 14);");
            sb.AppendLine("        L.control.zoom({ position: 'bottomright' }).addTo(map);");
            sb.AppendLine("        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {");
            sb.AppendLine("            maxZoom: 19, attribution: '&copy; OpenStreetMap'");
            sb.AppendLine("        }).addTo(map);");
            sb.AppendLine($"        var userMarker = L.circleMarker([{userLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {userLon.ToString(System.Globalization.CultureInfo.InvariantCulture)}], {{");
            sb.AppendLine("            color:'#0088FF', fillColor:'#0088FF', fillOpacity:0.8, radius:8, weight:2");
            sb.AppendLine("        }).addTo(map).bindPopup('V\u1ecb tr\u00ed c\u1ee7a b\u1ea1n');");
            sb.AppendLine("");
            sb.AppendLine("        var bounds = [];");
            sb.AppendLine("        bounds.push(userMarker.getLatLng());");
            sb.AppendLine("");
            sb.AppendLine("        function updateUserLocation(lat, lng) {");
            sb.AppendLine("            var newLatLng = new L.LatLng(lat, lng);");
            sb.AppendLine("            userMarker.setLatLng(newLatLng);");
            sb.AppendLine("        }");
            sb.AppendLine("");
            sb.AppendLine("        function centerOnUser() {");
            sb.AppendLine("            map.setView(userMarker.getLatLng(), 16);");
            sb.AppendLine("        }");
            if (pois != null)
            {
                foreach (var poi in pois)
                {
                    var lat = poi.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var lon = poi.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var n = (poi.Name ?? "").Replace("'", "\\'").Replace("\n", " ").Replace("\"", "&quot;");
                    var a = (poi.Address ?? "").Replace("'", "\\'").Replace("\n", " ");
                    sb.AppendLine($"        var m{poi.Id} = L.marker([{lat}, {lon}]).addTo(map);");
                    sb.AppendLine($"        m{poi.Id}.bindTooltip('{n}', {{ permanent: true, direction: 'top', className: 'custom-tooltip', offset: [0, -10] }});");
                    sb.AppendLine($"        m{poi.Id}.bindPopup('<b>{n}</b><br/><small style=\"color:#666\">{a}</small>');");
                    sb.AppendLine($"        bounds.push(m{poi.Id}.getLatLng());");
                    sb.AppendLine($"        m{poi.Id}.on('click', function() {{");
                    sb.AppendLine($"            // Double click or long press could lead to details, but let's keep it simple for now");
                    sb.AppendLine($"            // window.location.href = 'poi-app:{poi.Id}';");
                    sb.AppendLine("        });");
                }
            }
            sb.AppendLine("        if (bounds.length > 0) { map.fitBounds(bounds, { padding: [50, 50] }); }");
            sb.AppendLine("    </script>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        public string GetMapHtmlFilePath()
        {
            var cacheDir = FileSystem.Current.CacheDirectory;
            return Path.Combine(cacheDir, "map.html");
        }

        /// <summary>
        /// Tour map with REAL ROAD routing via OSRM.
        /// Has 8-second timeout → auto fallback to straight-line if OSRM unreachable.
        /// </summary>
        public string GenerateTourMapHtml(string tourName, IEnumerable<POI> tourPois, double userLat, double userLon)
        {
            var poiList = tourPois?.ToList() ?? new List<POI>();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder();

            // ── HEAD ─────────────────────────────────────────────────────────────
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("    <meta charset='utf-8' />");
            sb.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.AppendLine("    <title>L\u1ed9 Tr\u00ecnh</title>");
            sb.AppendLine("    <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.min.css' />");
            sb.AppendLine("    <style>");
            sb.AppendLine("        * { box-sizing:border-box; margin:0; padding:0; }");
            sb.AppendLine("        body { font-family:Arial,sans-serif; }");
            sb.AppendLine("        #map { position:absolute; top:0; bottom:0; width:100%; }");
            sb.AppendLine("        #ld { position:absolute; top:0; left:0; right:0; bottom:0;");
            sb.AppendLine("              background:rgba(255,255,255,0.92); z-index:2000;");
            sb.AppendLine("              display:flex; flex-direction:column;");
            sb.AppendLine("              align-items:center; justify-content:center; }");
            sb.AppendLine("        .sp { width:44px; height:44px;");
            sb.AppendLine("              border:4px solid #ffe0b2; border-top:4px solid #e65100;");
            sb.AppendLine("              border-radius:50%; animation:spin .8s linear infinite; }");
            sb.AppendLine("        @keyframes spin { to { transform:rotate(360deg); } }");
            sb.AppendLine("        #ld p { color:#e65100; font-size:15px; margin-top:14px; font-weight:bold; }");
            sb.AppendLine("        #ld small { color:#888; font-size:12px; margin-top:6px; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine("    <div id='map'></div>");
            // Loading overlay — proper Vietnamese text using HTML entities
            sb.AppendLine("    <div id='ld'>");
            sb.AppendLine("        <div class='sp'></div>");
            sb.AppendLine("        <p>\u0110ang t\u1ea3i \u0111\u01b0\u1eddng \u0111i th\u1ef1c t\u1ebf...</p>");
            sb.AppendLine("        <small>Vui l\u00f2ng \u0111\u1ee3i v\u00e0i gi\u00e2y</small>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <script src='https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.min.js'></script>");
            sb.AppendLine("    <script>");

            // ── MAP INIT ─────────────────────────────────────────────────────────
            double cLat = poiList.Count > 0 ? poiList[0].Latitude : userLat;
            double cLon = poiList.Count > 0 ? poiList[0].Longitude : userLon;
            sb.AppendLine($"        var map = L.map('map', {{ zoomControl: false }}).setView([{cLat.ToString(inv)},{cLon.ToString(inv)}],15);");
            sb.AppendLine("        L.control.zoom({ position: 'bottomright' }).addTo(map);");
            sb.AppendLine("        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png',{");
            sb.AppendLine("            maxZoom:19, attribution:'&copy; OpenStreetMap'");
            sb.AppendLine("        }).addTo(map);");

            // User marker
            sb.AppendLine($"        L.circleMarker([{userLat.ToString(inv)},{userLon.ToString(inv)}],{{");
            sb.AppendLine("            color:'#0088FF',fillColor:'#0088FF',fillOpacity:.9,radius:9,weight:3");
            sb.AppendLine($"        }}).addTo(map).bindPopup('<b>V\u1ecb tr\u00ed c\u1ee7a b\u1ea1n</b>');");

            if (poiList.Count > 0)
            {
                // ── STOP MARKERS ─────────────────────────────────────────────────
                for (int i = 0; i < poiList.Count; i++)
                {
                    var poi = poiList[i];
                    string lat = poi.Latitude.ToString(inv);
                    string lon = poi.Longitude.ToString(inv);
                    int num = i + 1;
                    string bg = (i == poiList.Count - 1) ? "#2e7d32" : "#e65100";
                    string n = (poi.Name ?? "").Replace("'", "\\'").Replace("\n", " ").Replace("\"", "&quot;");
                    string d = (poi.Description ?? "").Replace("'", "\\'").Replace("\n", " ");
                    string a = (poi.Address ?? "").Replace("'", "\\'").Replace("\n", " ");

                    string iconHtml = $"<div style=\"background:{bg};color:white;border-radius:50%;width:34px;height:34px;display:flex;align-items:center;justify-content:center;font-weight:bold;font-size:15px;border:3px solid white;box-shadow:0 2px 8px rgba(0,0,0,.45);\">{num}</div>";
                    sb.AppendLine($"        var _ic{i}=L.divIcon({{className:'',html:'{iconHtml}',iconSize:[34,34],iconAnchor:[17,17]}});");
                    sb.AppendLine($"        L.marker([{lat},{lon}],{{icon:_ic{i}}}).addTo(map)");
                    sb.AppendLine($"            .bindPopup('<b>{num}. {n}</b><br/>{d}<br/><small style=\"color:#888\">{a}</small>');");
                }

                // ── WAYPOINTS ────────────────────────────────────────────────────
                sb.Append("        var WP=[");
                sb.Append($"[{userLat.ToString(inv)},{userLon.ToString(inv)}]");
                foreach (var p in poiList)
                    sb.Append($",[{p.Latitude.ToString(inv)},{p.Longitude.ToString(inv)}]");
                sb.AppendLine("];");

                // ── STOP NAMES ───────────────────────────────────────────────────
                sb.Append("        var SN=['\u0110i\u1ec3m b\u1eaft \u0111\u1ea7u'"); // "Điểm bắt đầu"
                foreach (var p in poiList)
                {
                    string sn = (p.Name ?? "\u0110i\u1ec3m d\u1eebng").Replace("'", "\\'").Replace("\n", " ");
                    sb.Append($",'{sn}'");
                }
                sb.AppendLine("];");

                // ── JS: helpers ──────────────────────────────────────────────────
                sb.AppendLine("        var _rl=[];");
                sb.AppendLine("        function _hide(){var e=document.getElementById('ld');if(e)e.style.display='none';}");

                // Format distance & time in Vietnamese
                sb.AppendLine("        function _fd(m){return m>=1000?(m/1000).toFixed(1)+' km':Math.round(m)+' m';}");
                sb.AppendLine("        function _ft(s){");
                sb.AppendLine("            if(s>=3600) return Math.floor(s/3600)+' gi\u1edd '+Math.floor((s%3600)/60)+' ph\u00fat';");
                sb.AppendLine("            if(s>=60)   return Math.floor(s/60)+' ph\u00fat';");
                sb.AppendLine("            return Math.round(s)+' gi\u00e2y';");
                sb.AppendLine("        }");

                // Maneuver icon
                sb.AppendLine("        function _mi(type,mod){");
                sb.AppendLine("            var t=((type||'')+'_'+(mod||'')).toLowerCase();");
                sb.AppendLine("            if(t.indexOf('depart')>=0) return '&#x1F4CD;';");
                sb.AppendLine("            if(t.indexOf('arrive')>=0) return '&#x1F3C1;';");
                sb.AppendLine("            if(t.indexOf('uturn')>=0)  return '&#x21A9;';");
                sb.AppendLine("            if(t.indexOf('roundabout')>=0||t.indexOf('rotary')>=0) return '&#x1F504;';");
                sb.AppendLine("            if(t.indexOf('fork')>=0)   return '&#x2442;';");
                sb.AppendLine("            if(t.indexOf('sharp_left')>=0||t.indexOf('sharp left')>=0) return '&#x21B0;';");
                sb.AppendLine("            if(t.indexOf('slight_left')>=0||t.indexOf('slight left')>=0) return '&#x2196;';");
                sb.AppendLine("            if(t.indexOf('_left')>=0||t.indexOf('turn_left')>=0) return '&#x2B05;';");
                sb.AppendLine("            if(t.indexOf('sharp_right')>=0||t.indexOf('sharp right')>=0) return '&#x21B1;';");
                sb.AppendLine("            if(t.indexOf('slight_right')>=0||t.indexOf('slight right')>=0) return '&#x2197;';");
                sb.AppendLine("            if(t.indexOf('_right')>=0||t.indexOf('turn_right')>=0) return '&#x27A1;';");
                sb.AppendLine("            if(t.indexOf('straight')>=0||t.indexOf('continue')>=0||t.indexOf('new name')>=0) return '&#x2B06;';");
                sb.AppendLine("            return '&#x27A1;';");
                sb.AppendLine("        }");

                // Build OSRM URL
                sb.AppendLine("        function _url(pts){");
                sb.AppendLine("            var c=pts.map(function(p){return p[1]+','+p[0];}).join(';');");
                sb.AppendLine("            return 'https://router.project-osrm.org/route/v1/driving/'+c+'?overview=full&geometries=geojson&steps=true';");
                sb.AppendLine("        }");

                // Draw route on map
                sb.AppendLine("        function _draw(geo,legs){");
                sb.AppendLine("            _rl.forEach(function(l){map.removeLayer(l);});_rl=[];");
                sb.AppendLine("            _rl.push(L.geoJSON(geo,{style:{color:'#fff',weight:9,opacity:.6}}).addTo(map));");
                sb.AppendLine("            _rl.push(L.geoJSON(geo,{style:{color:'#e65100',weight:5,opacity:.95}}).addTo(map));");
                sb.AppendLine("            map.fitBounds(L.geoJSON(geo).getBounds(),{padding:[50,50]});");
                sb.AppendLine("            _panel(legs);_hide();");
                sb.AppendLine("        }");

                // Build directions panel
                sb.AppendLine("        function _panel(legs){");
                sb.AppendLine("            var old=document.getElementById('dp');if(old)old.parentNode.removeChild(old);");
                sb.AppendLine("            var td=legs.reduce(function(s,l){return s+l.distance;},0);");
                sb.AppendLine("            var tt=legs.reduce(function(s,l){return s+l.duration;},0);");
                sb.AppendLine("            var p=document.createElement('div');");
                sb.AppendLine("            p.id='dp';");
                sb.AppendLine("            p.style.cssText='position:absolute;bottom:0;left:0;right:0;max-height:42%;overflow-y:auto;background:white;z-index:1001;box-shadow:0 -3px 16px rgba(0,0,0,.22);border-radius:20px 20px 0 0;';");
                sb.AppendLine("            var h=document.createElement('div');");
                sb.AppendLine("            h.style.cssText='background:#e65100;color:white;padding:12px 18px;border-radius:20px 20px 0 0;display:flex;justify-content:space-between;align-items:center;';");
                // Header: "Hướng dẫn đường đi"
                sb.AppendLine("            h.innerHTML='<div><div style=\"font-weight:bold;font-size:14px\">H\u01b0\u1edbng d\u1eabn \u0111\u01b0\u1eddng \u0111i</div>'");
                sb.AppendLine("                +'<div style=\"font-size:11px;opacity:.9;margin-top:2px\">'+legs.length+' ch\u1eb7ng &middot; '+_ft(tt)+'</div></div>'");
                sb.AppendLine("                +'<div style=\"font-size:17px;font-weight:bold\">'+_fd(td)+'</div>';");
                sb.AppendLine("            p.appendChild(h);");
                sb.AppendLine("            var hnd=document.createElement('div');");
                sb.AppendLine("            hnd.style.cssText='width:40px;height:4px;background:#ddd;border-radius:2px;margin:8px auto 2px;';");
                sb.AppendLine("            p.appendChild(hnd);");
                sb.AppendLine("            legs.forEach(function(leg,li){");
                sb.AppendLine("                var dn=SN[li+1]||('\u0110i\u1ec3m '+(li+1));");
                sb.AppendLine("                var lh=document.createElement('div');");
                sb.AppendLine("                lh.style.cssText='background:#fff3e0;padding:7px 18px;font-size:12px;color:#bf360c;font-weight:bold;display:flex;align-items:center;gap:6px;';");
                sb.AppendLine("                lh.innerHTML='<span style=\"background:#e65100;color:white;border-radius:50%;width:20px;height:20px;display:inline-flex;align-items:center;justify-content:center;font-size:11px;\">'+(li+1)+'</span> '+dn");
                sb.AppendLine("                    +'<span style=\"margin-left:auto;font-weight:normal;color:#888;font-size:11px;\">'+_fd(leg.distance)+' &middot; '+_ft(leg.duration)+'</span>';");
                sb.AppendLine("                p.appendChild(lh);");
                sb.AppendLine("                leg.steps.forEach(function(step,si){");
                sb.AppendLine("                    if(!step.maneuver)return;");
                sb.AppendLine("                    var ic=_mi(step.maneuver.type,step.maneuver.modifier);");
                // "Bắt đầu" / "Tiếp tục"
                sb.AppendLine("                    var st=(step.name&&step.name.trim())?step.name:(si===0?'B\u1eaft \u0111\u1ea7u':'Ti\u1ebfp t\u1ee5c');");
                sb.AppendLine("                    var rw=document.createElement('div');");
                sb.AppendLine("                    rw.style.cssText='display:flex;align-items:center;padding:9px 18px;border-bottom:1px solid #f0f0f0;';");
                sb.AppendLine("                    rw.innerHTML='<span style=\"font-size:20px;min-width:30px;text-align:center\">'+ic+'</span>'");
                sb.AppendLine("                        +'<span style=\"flex:1;margin-left:12px;font-size:13px;color:#333;line-height:1.3\">'+st+'</span>'");
                sb.AppendLine("                        +(step.distance>1?'<span style=\"font-size:12px;color:#e65100;white-space:nowrap;margin-left:6px\">'+_fd(step.distance)+'</span>':'');");
                sb.AppendLine("                    p.appendChild(rw);");
                sb.AppendLine("                });");
                sb.AppendLine("            });");
                sb.AppendLine("            document.body.appendChild(p);");
                sb.AppendLine("        }");

                // Fallback: straight polyline
                sb.AppendLine("        function _fallback(msg){");
                sb.AppendLine("            var ll=WP.map(function(p){return L.latLng(p[0],p[1]);});");
                sb.AppendLine("            L.polyline(ll,{color:'#e65100',weight:5,opacity:.85,dashArray:'10,7'}).addTo(map);");
                sb.AppendLine("            map.fitBounds(L.latLngBounds(ll),{padding:[50,50]});");
                sb.AppendLine("            _hide();");
                sb.AppendLine("            var nt=document.createElement('div');");
                sb.AppendLine("            nt.style.cssText='position:absolute;bottom:0;left:0;right:0;background:#fff3e0;color:#bf360c;text-align:center;padding:14px 18px;z-index:1001;font-size:13px;border-radius:18px 18px 0 0;';");
                // "⚠ Không tải được đường đi chi tiết. Hiển thị đường thẳng."
                sb.AppendLine("            nt.innerHTML='&#x26A0; Kh\u00f4ng t\u1ea3i \u0111\u01b0\u1ee3c \u0111\u01b0\u1eddng \u0111i chi ti\u1ebft.<br>Hi\u1ec3n th\u1ecb \u0111\u01b0\u1eddng th\u1eb3ng gi\u1eefa c\u00e1c \u0111i\u1ec3m.';");
                sb.AppendLine("            document.body.appendChild(nt);");
                sb.AppendLine("        }");

                // Fetch with 8-second timeout
                sb.AppendLine("        function _fetchWithTimeout(url, ms){");
                sb.AppendLine("            var controller=typeof AbortController!=='undefined'?new AbortController():null;");
                sb.AppendLine("            var timer=null;");
                sb.AppendLine("            var p=fetch(url, controller?{signal:controller.signal}:{})");
                sb.AppendLine("                .then(function(r){clearTimeout(timer);return r.json();});");
                sb.AppendLine("            if(controller){");
                sb.AppendLine("                timer=setTimeout(function(){controller.abort();},ms);");
                sb.AppendLine("            }");
                sb.AppendLine("            return p;");
                sb.AppendLine("        }");

                sb.AppendLine("        function _fetch(){");
                sb.AppendLine("            console.log('Fetching route for waypoints:', WP);");
                sb.AppendLine("            _fetchWithTimeout(_url(WP), 8000)");
                sb.AppendLine("                .then(function(data){");
                sb.AppendLine("                    console.log('OSRM Response:', data);");
                sb.AppendLine("                    if(data.code==='Ok'&&data.routes&&data.routes.length>0){");
                sb.AppendLine("                        _draw(data.routes[0].geometry,data.routes[0].legs);");
                sb.AppendLine("                    } else { ");
                sb.AppendLine("                        console.warn('OSRM Route not found:', data.code);");
                sb.AppendLine("                        _fallback('no route'); ");
                sb.AppendLine("                    }");
                sb.AppendLine("                }).catch(function(e){");
                sb.AppendLine("                    console.error('OSRM Fetch error:', e);");
                sb.AppendLine("                    _fallback(e?e.message:'err');");
                sb.AppendLine("                });");
                sb.AppendLine("        }");
                sb.AppendLine("        _fetch();");
            }
            else
            {
                sb.AppendLine("        document.getElementById('ld').style.display='none';");
                sb.AppendLine($"        L.circleMarker([{cLat.ToString(inv)},{cLon.ToString(inv)}],{{radius:10,color:'gray'}})");
                // "Chưa có địa điểm nào trong lộ trình này."
                sb.AppendLine("            .addTo(map).bindPopup('Ch\u01b0a c\u00f3 \u0111\u1ecba \u0111i\u1ec3m n\u00e0o trong l\u1ed9 tr\u00ecnh n\u00e0y.');");
            }

            sb.AppendLine("    </script>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }
    }
}

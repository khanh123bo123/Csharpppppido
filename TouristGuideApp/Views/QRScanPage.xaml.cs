using ZXing.Net.Maui;
using System.Text.RegularExpressions;

namespace TouristGuideApp.Views;

public partial class QRScanPage : ContentPage
{
    private bool _isProcessing;

    public QRScanPage()
    {
        InitializeComponent();
        
        barcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.All,
            AutoRotate = true,
            Multiple = false
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isProcessing = false;

        // Ensure we are using the real camera (runtime permission)
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }

        if (status != PermissionStatus.Granted)
        {
            barcodeReader.IsDetecting = false;
            await DisplayAlertAsync("Quyền Camera", "Ứng dụng cần quyền camera để quét mã QR.", "OK");
            return;
        }

        barcodeReader.IsDetecting = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        barcodeReader.IsDetecting = false;
    }

    private void CameraBarcodeReaderView_BarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessing) return;

        var first = e.Results?.FirstOrDefault();
        if (first == null) return;

        var value = first.Value;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await ProcessQrCodeAsync(value);
        });
    }

    private async void OnManualInputClicked(object sender, EventArgs e)
    {
        string result = await DisplayPromptAsync("Nhập mã QR", "Nhập chuỗi ID địa điểm (ví dụ: LOC_ocdao001 hoặc 1):");
        if (!string.IsNullOrWhiteSpace(result))
        {
            await ProcessQrCodeAsync(result);
        }
    }

    private async Task ProcessQrCodeAsync(string value)
    {
        _isProcessing = true;
        barcodeReader.IsDetecting = false;
        
        var trimmed = (value ?? string.Empty).Trim();

        // Prefer real backend QR payload (e.g., LOC_...)
        var qrCodeData = TryExtractQrCodeData(trimmed);
        if (!string.IsNullOrWhiteSpace(qrCodeData))
        {
            var encoded = Uri.EscapeDataString(qrCodeData);
            await Shell.Current.GoToAsync($"{nameof(POIDetailsPage)}?qr={encoded}");
            return;
        }

        // Backward compatibility: allow numeric id (server location id)
        var serverId = TryExtractServerLocationId(trimmed);
        if (serverId is > 0)
        {
            var encoded = Uri.EscapeDataString(serverId.Value.ToString());
            await Shell.Current.GoToAsync($"{nameof(POIDetailsPage)}?qr={encoded}");
            return;
        }

        await DisplayAlertAsync("Lỗi QR", $"Mã QR không hợp lệ: {trimmed}", "OK");
        _isProcessing = false;
        barcodeReader.IsDetecting = true;
    }

    private static int? TryExtractServerLocationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        if (trimmed.StartsWith("poi:", StringComparison.OrdinalIgnoreCase))
        {
            var rest = trimmed.Substring(4);
            if (int.TryParse(rest, out var id)) return id;
        }

        if (Regex.IsMatch(trimmed, @"^\d+$") && int.TryParse(trimmed, out var direct))
        {
            return direct;
        }

        // Try to extract a number from common URL-ish formats
        var match = Regex.Match(trimmed, @"\b(\d+)\b");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var extracted))
        {
            return extracted;
        }

        return null;
    }

    private static string? TryExtractQrCodeData(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();

        // Direct payload
        if (trimmed.StartsWith("LOC_", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        // If QR contains a URL/text, try to locate LOC_... inside
        var match = Regex.Match(trimmed, @"LOC_[A-Za-z0-9]+", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Value;
        }

        return null;
    }
}

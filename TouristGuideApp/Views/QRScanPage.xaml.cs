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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isProcessing = false;
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
            _isProcessing = true;
            barcodeReader.IsDetecting = false;
            
            // Expected formats: "10" or "poi:10"
            string poiIdStr = "";

            if (value.StartsWith("poi:", StringComparison.OrdinalIgnoreCase))
            {
                poiIdStr = value.Substring(4);
            }
            else if (Regex.IsMatch(value, @"^\d+$"))
            {
                poiIdStr = value;
            }

            if (!string.IsNullOrEmpty(poiIdStr) && int.TryParse(poiIdStr, out int poiId))
            {
                // Go to POI Detail Page
                await Shell.Current.GoToAsync($"{nameof(POIDetailsPage)}?poiId={poiId}");
            }
            else
            {
                await DisplayAlert("Lỗi QR", $"Mã QR không hợp lệ: {value}", "OK");
                _isProcessing = false;
                barcodeReader.IsDetecting = true;
            }
        });
    }
}

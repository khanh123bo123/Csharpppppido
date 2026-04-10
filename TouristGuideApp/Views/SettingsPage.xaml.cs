namespace TouristGuideApp.Views;

public partial class SettingsPage : ContentPage
{
	public SettingsPage()
	{
		InitializeComponent();
	}

    private async void OnClearHistoryClicked(object sender, EventArgs e)
    {
        // Placeholder cho chức năng xóa lịch sử (reset HasBeenPlayed)
        await DisplayAlertAsync("Thành công", "Lịch sử thuyết minh đã được xóa.", "OK");
    }
}

using System.ComponentModel;
using System.Globalization;
using System.Resources;
using Microsoft.Maui.Storage;

namespace TouristGuideApp.Services;

public class LocalizationResourceManager : INotifyPropertyChanged
{
    public static LocalizationResourceManager Instance { get; } = new();

    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    private LocalizationResourceManager()
    {
        // Path to the .resx embedded resource
        _resourceManager = new ResourceManager("TouristGuideApp.Resources.Strings.AppResources", typeof(LocalizationResourceManager).Assembly);
        var code = Preferences.Default.Get("NarrationLanguageCode", "vi-VN");
        _currentCulture = new CultureInfo(code);
    }

    public string this[string resourceKey]
    {
        get
        {
            try
            {
                var text = _resourceManager.GetString(resourceKey, _currentCulture);
                return string.IsNullOrEmpty(text) ? resourceKey : text;
            }
            catch
            {
                return resourceKey;
            }
        }
    }

    public void SetCulture(CultureInfo culture)
    {
        _currentCulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

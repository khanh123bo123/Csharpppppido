using Prism;
using Prism.Ioc;
using TouristGuideAppXF.Services;
using TouristGuideAppXF.ViewModels;
using TouristGuideAppXF.Views;
using Xamarin.Essentials.Interfaces;
using Xamarin.Essentials.Implementation;
using Xamarin.Forms;

namespace TouristGuideAppXF
{
    public partial class App
    {
        public static ApiService ApiService { get; private set; }

        public App(IPlatformInitializer initializer)
            : base(initializer)
        {
            ApiService = new ApiService();
        }

        protected override async void OnInitialized()
        {
            InitializeComponent();

            await NavigationService.NavigateAsync("NavigationPage/MainPage");
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IAppInfo, AppInfoImplementation>();
            containerRegistry.RegisterInstance(ApiService);
            containerRegistry.RegisterInstance<IApiService>(ApiService);

            containerRegistry.RegisterForNavigation<NavigationPage>();
            containerRegistry.RegisterForNavigation<MainPage, MainPageViewModel>();
        }
    }
}

using BH_Install.ViewModels;
using BH_Install.Views;
using Microsoft.Extensions.DependencyInjection;

namespace BH_Install
{
    //DI 컨테이너 구성 (앱 시작 시 1회 호출)
    public static class DiService
    {
        public static ServiceProvider ServicesRegister()
        {
            var services = new ServiceCollection();

            // ViewModel
            services.AddSingleton<MainViewModel>();

            // View
            services.AddSingleton<MainView>();

            return services.BuildServiceProvider();
        }
    }
}
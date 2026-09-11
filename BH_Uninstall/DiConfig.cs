using BH_Uninstall.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace BH_Uninstall
{
    //DI 컨테이너 구성 (앱 시작 시 1회 호출)
    public static class DiConfig
    {
        public static void Configure()
        {
            Ioc.Default.ConfigureServices(
                new ServiceCollection()
                    .AddSingleton<UninstallViewModel>()
                    .BuildServiceProvider());
        }
    }
}

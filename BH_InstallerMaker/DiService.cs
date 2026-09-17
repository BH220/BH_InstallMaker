using BH_InstallerMaker.Services;
using BH_InstallerMaker.ViewModels;
using BH_InstallerMaker.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;

namespace BH_InstallerMaker
{
    public static class DiService
    {
        public static ServiceProvider ServicesRegister()
        {
            var services = new ServiceCollection();

            // 서비스 
            services.AddSingleton<IFileDialogService, FileDialogService>();
            services.AddSingleton<ProjectService>();
            services.AddSingleton<SettingsService>();
            services.AddSingleton<ModuleBuilder>();
            services.AddSingleton<IMessenger>(new WeakReferenceMessenger());

            // ViewModel
            services.AddSingleton<MainViewModel>();

            // View
            services.AddSingleton<MainView>();

            return services.BuildServiceProvider();
        }
    }
}

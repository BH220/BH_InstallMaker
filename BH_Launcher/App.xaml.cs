using System.Windows;
using BH_Install.Core;
using BH_Launcher.Views;
using Microsoft.Extensions.DependencyInjection;

namespace BH_Launcher
{
    public partial class App : Application
    {
        public IServiceProvider? Services { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            //관리자 권한 변경 (권한이 없으면 관리자로 다시 실행하고 이 프로세스는 끝낸다)
            if (AppBootstrap.RelaunchAsAdminIfNeeded())
            {
                Shutdown();
                return;
            }

            //디버그 빌드 또는 exe 옆에 CTest.dat 이 있으면 콘솔 창을 띄운다
            AppBootstrap.ShowConsoleWindow();

            // 1. 기본 WPF 어플리케이션 초기화
            base.OnStartup(e);

            // DI 컨테이너 구성
            Services = DiService.ServicesRegister();

            // 2. 어플리케이션 종료 모드 설정
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            Console.WriteLine("[System] Program Start");

            //라이선스를 확인하여 복사본인지 체크한다

            // MainWindow 설정 및 수동 Show
            var view = Services.GetRequiredService<MainView>();
            ShowWindow(view);
        }

        private static void ShowWindow(Window window)
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Closed += Window_Closed;
            window.WindowState = WindowState.Normal;
            window.Show();
        }

        //메인 창이 닫히면 남은 창을 모두 닫고 프로세스를 끝낸다
        private static void Window_Closed(object? sender, EventArgs e)
        {
            if (Current is not null)
            {
                foreach (Window win in Current.Windows)
                {
                    if (win != sender as Window)
                        win.Close();
                }
            }
            Current?.Shutdown();
            Environment.Exit(0);
        }
    }
}
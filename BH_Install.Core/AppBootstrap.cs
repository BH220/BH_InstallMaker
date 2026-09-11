using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows;
using Microsoft.Win32.SafeHandles;

namespace BH_Install.Core
{
    //모든 앱(메이커·설치·런처·언인스톨)의 App.OnStartup 이 공통으로 쓰는 시작 처리.
    //  - 관리자 권한이 아니면 자기 자신을 관리자로 다시 실행
    //  - 디버그 빌드이거나 exe 옆에 CTest.dat 이 있으면 콘솔 창을 열어 Console 출력을 보여준다
    public static class AppBootstrap
    {
        //exe 옆에 이 파일이 있으면 릴리스 빌드에서도 콘솔 창을 연다
        public const string ConsoleTriggerFile = "CTest.dat";

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCP(uint wCodePageID);

        private const uint CP_UTF8 = 65001;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
            uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_WRITE = 0x2;
        private const uint OPEN_EXISTING = 0x3;

        public static bool IsRunAsAdmin()
        {
            try
            {
                using WindowsIdentity id = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        //관리자 권한이 아니면 같은 인수로 자기 자신을 runas 로 다시 실행하고 true 를 돌려준다.
        //호출자는 true 를 받으면 바로 종료해야 한다. 이미 관리자이거나 UAC 를 거부해 재실행에 실패하면 false.
        public static bool RelaunchAsAdminIfNeeded()
        {
            if (IsRunAsAdmin())
                return false;

            string? exe = Environment.ProcessPath;
            if (exe is null)
                return false;

            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                Verb = "runas",
            };
            foreach (string arg in Environment.GetCommandLineArgs().Skip(1))
                psi.ArgumentList.Add(arg);

            try
            {
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                //UAC 거부(1223) 등. 현재 권한으로 계속 간다.
                Console.WriteLine("관리자 권한 실행 실패: " + ex.Message);
                return false;
            }
        }

        //디버그 빌드이거나 CTest.dat 이 있으면 콘솔 창을 만들어 Console.Out 을 연결한다.
        public static void ShowConsoleWindow()
        {
            bool open = IsDebugBuild(Assembly.GetEntryAssembly())
                        || File.Exists(Path.Combine(AppContext.BaseDirectory, ConsoleTriggerFile));
            if (!open)
                return;

            if (!AllocConsole())
            {
                MessageBox.Show("Console Window Load Failed");
                return;
            }

            //콘솔 코드 페이지를 UTF-8 로 맞추고 같은 인코딩으로 쓴다.
            //시스템 기본(949)이든 사용자가 바꾼 값(65001)이든 상관없이 한글이 깨지지 않는다.
            SetConsoleOutputCP(CP_UTF8);
            SetConsoleCP(CP_UTF8);

            IntPtr handle = CreateFile("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            var safeHandle = new SafeFileHandle(handle, ownsHandle: true);
            var writer = new StreamWriter(new FileStream(safeHandle, FileAccess.Write), new UTF8Encoding(false))
            {
                AutoFlush = true,
            };
            Console.SetOut(writer);
            Console.WriteLine("[System] Console ready");
        }

        //진입 어셈블리가 Debug 구성으로 빌드되었는지 (DebuggableAttribute 로 판단)
        private static bool IsDebugBuild(Assembly? assembly) =>
            assembly?.GetCustomAttribute<DebuggableAttribute>()?.IsJITTrackingEnabled == true;
    }
}
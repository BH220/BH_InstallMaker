using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;

namespace BH_Install.Core.Manager
{
    //런처가 갱신을 시작하기 전에 설치 폴더의 프로그램이 실행 중이면 모두 끝낸다. 파일이 잠겨 있으면 덮어쓸 수 없기 때문이다.
    //
    //설치 폴더의 파일을 잡고 있는 프로세스를 Windows 재시작 관리자(Restart Manager)에게 묻는다.
    //exe 는 실행 중이면 항상 열려 있고 dll 도 올려 둔 프로세스에서 열려 있으므로, 대상 프로그램·서비스·dll 을 쓰는 다른 프로세스가 모두 잡힌다.
    //모든 프로세스의 모듈을 훑는 방식은 수 초가 걸리지만 이 방식은 수십 ms 다.
    //  - 서비스(RmService) → SCM 으로 정상 정지
    //  - 탐색기·시스템 필수 프로세스(RmExplorer·RmCritical) → 건드리지 않고 경고만
    //  - 그 외 → 창 닫기 요청 뒤 종료 (자식 프로세스 포함)
    //자기 자신(런처)은 제외한다.
    public static class TargetProcessManager
    {
        private static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan KillWait = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ServiceStopWait = TimeSpan.FromSeconds(10);

        public static void TerminateAll(string rootPath, Action<string>? log = null)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                return;

            string[] files = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
                return;

            RmProcessInfo[] holders;
            try
            {
                holders = RestartManager.GetProcessesLocking(files);
            }
            catch (Exception ex)
            {
                //재시작 관리자를 못 쓰면 실행 파일 위치로만 판단한다
                log?.Invoke($"재시작 관리자 조회 실패, 실행 파일 위치로 대신 확인합니다: {ex.Message}");
                TerminateByMainModule(rootPath, log);
                return;
            }

            int self = Environment.ProcessId;
            foreach (RmProcessInfo info in holders)
            {
                if (info.Process.dwProcessId == self)
                    continue;

                switch (info.ApplicationType)
                {
                    case RmAppType.RmService:
                        StopService(info.strServiceShortName, log);
                        break;

                    case RmAppType.RmExplorer:
                    case RmAppType.RmCritical:
                        log?.Invoke($"경고: {info.strAppName}(PID {info.Process.dwProcessId}) 이(가) 설치 폴더의 파일을 사용 중이지만 시스템 프로세스라 종료하지 않습니다.");
                        break;

                    default:
                        log?.Invoke($"실행 중인 프로그램 종료: {info.strAppName} (PID {info.Process.dwProcessId})");
                        TerminateProcess(info.Process.dwProcessId, log);
                        break;
                }
            }
        }

        // ----- 종료 -----

        private static void StopService(string serviceName, Action<string>? log)
        {
            try
            {
                using var service = new ServiceController(serviceName);
                if (service.Status == ServiceControllerStatus.Stopped)
                    return;

                log?.Invoke($"서비스 정지: {serviceName}");
                if (service.CanStop)
                    service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, ServiceStopWait);
            }
            catch (Exception ex)
            {
                log?.Invoke($"서비스 {serviceName} 정지 실패: {ex.Message}");
            }
        }

        //창이 있으면 먼저 닫기를 요청하고, 끝나지 않으면 자식 프로세스까지 강제 종료한다
        private static void TerminateProcess(int pid, Action<string>? log)
        {
            try
            {
                using Process process = Process.GetProcessById(pid);
                if (process.MainWindowHandle != IntPtr.Zero && process.CloseMainWindow() && process.WaitForExit((int)CloseWait.TotalMilliseconds))
                    return;

                process.Kill(entireProcessTree: true);
                process.WaitForExit((int)KillWait.TotalMilliseconds);
            }
            catch (ArgumentException)
            {
                //이미 끝난 프로세스
            }
            catch (Exception ex)
            {
                log?.Invoke($"PID {pid} 종료 실패: {ex.Message}");
            }
        }

        //대안 경로: 실행 파일이 설치 폴더 아래인 프로세스만 종료한다 (dll 만 올려 둔 프로세스는 못 잡는다)
        private static void TerminateByMainModule(string rootPath, Action<string>? log)
        {
            string root = Path.GetFullPath(rootPath).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            int self = Environment.ProcessId;

            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    if (process.Id == self)
                        continue;

                    string? exe;
                    try { exe = process.MainModule?.FileName; }
                    catch { continue; }   //보호된 프로세스 등

                    if (exe is null || !exe.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        continue;

                    log?.Invoke($"실행 중인 프로그램 종료: {Path.GetFileName(exe)} (PID {process.Id})");
                    TerminateProcess(process.Id, log);
                }
            }
        }

        // ----- Restart Manager (rstrtmgr.dll) -----

        private enum RmAppType
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RmUniqueProcess
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RmProcessInfo
        {
            public RmUniqueProcess Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string strServiceShortName;
            public RmAppType ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)] public bool bRestartable;
        }

        private static class RestartManager
        {
            private const int ErrorMoreData = 234;
            private const int SessionKeyLength = 32;

            [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
            private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

            [DllImport("rstrtmgr.dll")]
            private static extern int RmEndSession(uint pSessionHandle);

            [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
            private static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames,
                uint nApplications, RmUniqueProcess[]? rgApplications, uint nServices, string[]? rgsServiceNames);

            [DllImport("rstrtmgr.dll")]
            private static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo,
                [In, Out] RmProcessInfo[]? rgAffectedApps, ref uint lpdwRebootReasons);

            //files 중 하나라도 열어 둔 프로세스 목록
            public static RmProcessInfo[] GetProcessesLocking(string[] files)
            {
                var key = new StringBuilder(SessionKeyLength + 1);
                int result = RmStartSession(out uint session, 0, key);
                if (result != 0)
                    throw new InvalidOperationException($"RmStartSession 실패 ({result})");

                try
                {
                    result = RmRegisterResources(session, (uint)files.Length, files, 0, null, 0, null);
                    if (result != 0)
                        throw new InvalidOperationException($"RmRegisterResources 실패 ({result})");

                    uint count = 0;
                    uint reasons = 0;
                    result = RmGetList(session, out uint needed, ref count, null, ref reasons);
                    if (result == 0 || needed == 0)
                        return Array.Empty<RmProcessInfo>();
                    if (result != ErrorMoreData)
                        throw new InvalidOperationException($"RmGetList 실패 ({result})");

                    var infos = new RmProcessInfo[needed];
                    count = needed;
                    result = RmGetList(session, out needed, ref count, infos, ref reasons);
                    if (result != 0)
                        throw new InvalidOperationException($"RmGetList 실패 ({result})");

                    return infos.Take((int)count).ToArray();
                }
                finally
                {
                    RmEndSession(session);
                }
            }
        }
    }
}

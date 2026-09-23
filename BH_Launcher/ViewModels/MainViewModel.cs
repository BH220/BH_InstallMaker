using BH_Install.Core;
using BH_Install.Core.Common;
using BH_Install.Core.Helper;
using BH_Install.Core.Manager;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;  

namespace BH_Launcher.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // 메이커가 빌드 직전에 BH_Install.Core 의 ProgramModel.json 에 저장한 매니페스트.
        // 비어 있으면(F5, 빈 ProgramModel.json) 설치 안내 후 종료한다.
        private readonly ProgramModel _model = ProgramManifest.Instance.ProgramModel;

        private Dictionary<string, string> listUpdate = new Dictionary<string, string>();

        private readonly Random _rand = new();
        private bool _started;

        [ObservableProperty]
        private string programName = "BH Sample Program";

        [ObservableProperty]
        private string versionText = "v1.2.0";

        [ObservableProperty]
        private string statusText = "서버에서 업데이트 확인 중...";

        [ObservableProperty]
        private string detailText = "-";

        [ObservableProperty]
        private double percent;

        [ObservableProperty]
        private string percentText = "0%";

        public MainViewModel()
        {
            if (!ProgramManifest.Instance.IsLoaded)
            {
                MessageBox.Show("정상 설치 후 실행해 주세요.", ProgramName, MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }


            //갱신 중 파일이 잠기지 않도록, 설치 폴더에서 실행 중인 대상 프로그램·서비스·dll 을 잡고 있는 프로세스를 모두 끝내고 시작한다
            try
            {
                TargetProcessManager.TerminateAll(_model.RootPath, Console.WriteLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"실행 중인 프로그램 정리 실패: {ex.Message}");
            }

            ProgramName = _model.Name;
            VersionText = $"v{_model.Version}";
            
#if DEBUG3
#else
            //설치 프로그램이 HKLM\{RegistryKey}\license 에 남긴 값을 ProgramId 와 맞춰본다. 틀리면 안내 후 종료.
            if (!LocalLicenseChecker.Instance.IsLicenseValid(_model.ProgramId, _model.RegistryKey, out string licenseError))
            {
                MessageBox.Show(licenseError, ProgramName, MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
#endif

            //메이커에서 지정한 업데이트 서버 주소. 업데이트 목록 조회·파일 다운로드의 기준 URL 이다.
            CheckUpdateServer();
        }

        //업데이트 서버 요청용 HttpClient. Chrome 이 보내는 것과 같은 헤더를 붙여 일반 브라우저 접근처럼 보이게 한다.
        //일부 호스팅·CDN 은 기본 User-Agent 를 막거나 다르게 응답한다. 캐시된 옛 update.json 을 받지 않도록 no-cache 도 붙인다.
        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };

            HttpRequestHeaders h = client.DefaultRequestHeaders;
            h.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
            h.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            h.AcceptLanguage.ParseAdd("ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7");
            h.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"128\", \"Not;A=Brand\";v=\"24\", \"Google Chrome\";v=\"128\"");
            h.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            h.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
            h.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            h.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            h.TryAddWithoutValidation("Sec-Fetch-Site", "none");
            h.TryAddWithoutValidation("Sec-Fetch-User", "?1");
            h.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            h.CacheControl = new CacheControlHeaderValue { NoCache = true };
            h.Pragma.ParseAdd("no-cache");
            return client;
        }

        //업데이트 서버에 접속해 update.json 을 listUpdate 에 담는다. 주소가 잘못되었거나 받지 못하면 안내 후 종료.
        private void CheckUpdateServer()
        {
            UpdateUrl = _model.UpdateUrl ?? string.Empty;
            Uri? baseUri = Uri.TryCreate(UpdateUrl, UriKind.Absolute, out Uri? parsed) ? parsed : null;
            _updateHost = baseUri?.Host ?? "서버";

            try
            {
                if (baseUri is null)
                    throw new InvalidOperationException($"업데이트 서버 주소가 올바르지 않습니다: '{UpdateUrl}'");

                //Path.Combine 은 역슬래시를 붙이므로 URL 에는 쓰지 않는다
                Uri checkUrl = new(baseUri, "update.json");

                using HttpClient client = CreateHttpClient();
                string text = client.GetStringAsync(checkUrl).GetAwaiter().GetResult();

                listUpdate = JsonConvert.DeserializeObject<Dictionary<string, string>>(text)
                    ?? throw new InvalidOperationException("update.json 을 해석할 수 없습니다.");
                if (listUpdate.Count == 0)
                    throw new InvalidOperationException("update.json 에 파일 목록이 없습니다.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"업데이트 서버에 연결할 수 없습니다.\n{ex.Message}", ProgramName, MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        //업데이트 서버 주소 (매니페스트 UpdateUrl)
        public string UpdateUrl { get; private set; }

        //상태 문구에 보여줄 서버 호스트 이름
        private string _updateHost { get; set; }

        //프로그램을 띄운 뒤(또는 띄우지 못해 안내를 마친 뒤) 창을 닫아 달라는 요청
        public event EventHandler? CloseRequested;

        public async Task RunUpdateAsync()
        {
            if (_started)
                return;
            _started = true;

            //1) 업데이트 대상 확인: 서버 목록(상대 경로 → SHA-256)과 설치 폴더 파일의 해시를 비교한다. 없거나 다르면 대상.
            StatusText = "업데이트 대상을 확인합니다..";
            List<string> listTarget = new();
            foreach ((string relative, string hash) in listUpdate)
            {
                string file = Path.Combine(_model.RootPath, relative);
                if (!File.Exists(file) || !string.Equals(file.ToHashEx(), hash, StringComparison.OrdinalIgnoreCase))
                    listTarget.Add(relative);
            }

            //2) 내려받아 적용. Updates 폴더에 {상대 경로}.zip 을 받아 풀고 설치 폴더의 같은 상대 경로에 덮어쓴다.
            double _totalMb = 0;
            string rootDic = Path.Combine(_model.RootPath, "Updates");
            try
            {
                Directory.CreateDirectory(rootDic);
                Uri baseUri = new(UpdateUrl);
                using HttpClient client = CreateHttpClient();

                for (int i = 0; i < listTarget.Count; i++)
                    _totalMb += await DownloadAndApplyAsync(client, baseUri, rootDic, listTarget[i], i, listTarget.Count);

                //if (Directory.Exists(rootDic))
                //    Directory.Delete(rootDic, recursive: true);
            }
            catch (Exception ex)
            {
                //중간에 실패해도 다음 실행 때 해시가 다른 파일만 다시 받으므로 그대로 두고 끝낸다
                MessageBox.Show($"업데이트에 실패했습니다.\n{ex.Message}", ProgramName, MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            // 3) 완료 후 실행
            Percent = 100;
            PercentText = "100%";
            DetailText = $"{_totalMb:0.0} / {_totalMb:0.0} MB";
            StatusText = "최신 버전입니다. 잠시 후 프로그램을 시작합니다...";
            VersionText = $"v{_model.Version}";

            await Task.Delay(1500);

            //4) 실제 프로그램 실행. 못 띄우면 사유를 알리고, 어느 쪽이든 런처 창은 닫는다
            if (!TryLaunchProgram(out string message))
                MessageBox.Show(message, ProgramName, MessageBoxButton.OK, MessageBoxImage.Information);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        //파일 하나를 받아 적용한다.
        //  서버: {UpdateUrl}/{상대 경로}.zip (메이커가 파일마다 따로 압축, 항목 이름은 파일 이름)
        //  받기: Updates\{상대 경로}.zip → 풀기: Updates\{상대 경로} → 해시 확인 → 설치 폴더\{상대 경로} 에 덮어쓰기
        //진행률은 받은 바이트/Content-Length 로 표시하고, 받은 크기(MB)를 돌려준다.
        private async Task<double> DownloadAndApplyAsync(HttpClient client, Uri baseUri, string updatesDir, string relative, int index, int count)
        {
            string zipPath = Path.Combine(updatesDir, relative + ".zip");
            string extracted = Path.Combine(updatesDir, relative);
            string dest = Path.Combine(_model.RootPath, relative);

            //이전에 남은 파일이 있으면 지우고 시작
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            if (File.Exists(extracted)) File.Delete(extracted);

            //내려받기
            Uri url = new(baseUri, relative.Replace('\\', '/') + ".zip");
            StatusText = $"다운로드 중 ({index + 1}/{count}): {Path.GetFileName(relative)}";

            using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            long? total = response.Content.Headers.ContentLength;

            long received = 0;
            await using (Stream http = await response.Content.ReadAsStreamAsync())
            await using (FileStream file = File.Create(zipPath))
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = await http.ReadAsync(buffer)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read));
                    received += read;
                    ReportProgress(index, count, total is > 0 ? (double)received / total.Value : 0, received, total);
                }
            }
            ReportProgress(index, count, 1, received, total);

            //풀기 → 해시 확인 → 덮어쓰기
            ZipFile.ExtractToDirectory(zipPath, Path.GetDirectoryName(extracted)!, overwriteFiles: true);
            //File.Delete(zipPath);
            if (!File.Exists(extracted))
                throw new InvalidOperationException($"압축 파일에 {Path.GetFileName(relative)} 이(가) 없습니다.");

            if (listUpdate.TryGetValue(relative, out string? expected)
                && !string.Equals(extracted.ToHashEx(), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"내려받은 파일이 손상되었습니다: {relative}");

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Move(extracted, dest, overwrite: true);

            return received / 1024.0 / 1024.0;
        }

        //전체 진행률 = (끝난 파일 수 + 현재 파일 비율) / 전체 파일 수
        private void ReportProgress(int index, int count, double fileFraction, long received, long? total)
        {
            Percent = (index + fileFraction) / count * 100;
            PercentText = $"{(int)Percent}%";

            double receivedMb = received / 1024.0 / 1024.0;
            DetailText = total is > 0
                ? $"{receivedMb:0.0} / {total.Value / 1024.0 / 1024.0:0.0} MB"
                : $"{receivedMb:0.0} MB";
        }

        // 매니페스트의 MainExe 를 런처 폴더 기준으로 실행한다.
        // 실패하면 false 와 사용자에게 보여줄 문구를 돌려준다.
        private bool TryLaunchProgram(out string message)
        {
            if (string.IsNullOrWhiteSpace(_model.MainExe))
            {
                message = "실행할 메인 프로그램이 지정되지 않았습니다.";
                return false;
            }

            string exe = Path.Combine(_model.RootPath, _model.MainExe);
            if (!File.Exists(exe))
            {
                message = $"프로그램 파일이 없습니다.\n{exe}\n\n업데이트 서버 연동 후에는 여기서 자동으로 내려받습니다.";
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo(exe)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exe),
                });
                message = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                message = $"프로그램을 실행할 수 없습니다.\n{ex.Message}";
                return false;
            }
        }
    }
}
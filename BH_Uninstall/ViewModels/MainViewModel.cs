using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using BH_Install.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BH_Uninstall.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public const int ScreenConfirm = 0;
        public const int ScreenProgress = 1;
        public const int ScreenDone = 2;

        // 메이커가 임베드한 매니페스트. 없으면(F5 디버깅) 더미로 화면만 보여주고 실제 삭제는 하지 않는다.
        private readonly ProgramModel _model;
        private readonly bool _isReal;

        private readonly Random _rand = new();

        // 언인스톨러 자신이 있는 폴더가 설치 폴더다. 매니페스트의 RootPath 와 같아야만 삭제를 진행한다.
        private static readonly string InstallDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        private readonly string _selfPath;

        private bool _completed;
        private bool _failed;
        private string _failReason = string.Empty;

        public MainViewModel()
        {
            ProgramModel? embedded = ProgramManifest.LoadEmbedded();
            _isReal = embedded is not null;
            _model = embedded ?? CreateDummyModel();
            _selfPath = Environment.ProcessPath ?? Path.Combine(InstallDir, ProgramManifest.UninstallFileName(_model));
        }

        private static ProgramModel CreateDummyModel() => new()
        {
            Name = "BH Sample Program",
            Publisher = "BH Soft",
            Version = "1.3.1",
            Description = "웹 업데이트 기반 런처를 통해 배포되는 샘플 프로그램입니다.",
            RootPath = @"C:\Program Files\BH Soft\BH Sample Program",
            DataRootPath = @"C:\ProgramData\BH Soft\BH Sample Program",
            RegistryKey = @"SOFTWARE\BH Soft\BH Sample Program",
            MainExe = "BH_Program.exe",
        };

        private sealed record RemoveStage(double At, string Text, Func<MainViewModel, Task>? Action);

        private static readonly RemoveStage[] Stages =
        {
            new(0,  "제거 준비 중...",                      vm => vm.PrepareAsync()),
            new(20, "바탕화면 아이콘 삭제 중...",            vm => vm.DeleteShortcutAsync(ShortcutHelper.DesktopDir)),
            new(36, "시작 메뉴 항목 삭제 중...",             vm => vm.DeleteStartMenuShortcutAsync()),
            new(50, "레지스트리 항목 삭제 중...",            vm => vm.RemoveRegistryAsync()),
            new(64, "프로그램 파일 삭제 중...",              vm => vm.DeleteFilesAsync()),
            new(90, "제거 마무리 중...",                    null),
        };

        [ObservableProperty]
        private int screenIndex = ScreenConfirm;

        [ObservableProperty]
        private string actionLabel = "제거";

        [ObservableProperty]
        private bool isActionEnabled = true;

        [ObservableProperty]
        private bool isCancelVisible = true;

        [ObservableProperty]
        private string stageText = "제거 준비 중...";

        [ObservableProperty]
        private double percent;

        [ObservableProperty]
        private string percentText = "0%";

        public ObservableCollection<string> Logs { get; } = new();

        public string ProgramName => _model.Name;
        public string ProgramSub => $"버전 {_model.Version} · {_model.Publisher}";
        public string ConfirmText => $"이 컴퓨터에서 {_model.Name}을(를) 제거합니다.";

        public string DoneTitle => _failed ? "제거를 완료하지 못했습니다" : "제거가 완료되었습니다";

        public string DoneSubText => _failed
            ? $"제거 중 오류가 발생했습니다.\n{_failReason}"
            : $"{_model.Name}이(가) 컴퓨터에서 제거되었습니다.";

        public event EventHandler? CloseRequested;

        [RelayCommand]
        private async Task ActionAsync()
        {
            switch (ScreenIndex)
            {
                case ScreenConfirm:
                    ScreenIndex = ScreenProgress;
                    ActionLabel = "제거 중...";
                    IsActionEnabled = false;

                    await RunRemoveAsync();

                    OnPropertyChanged(nameof(DoneTitle));
                    OnPropertyChanged(nameof(DoneSubText));
                    ScreenIndex = ScreenDone;
                    ActionLabel = "닫기";
                    IsActionEnabled = true;
                    IsCancelVisible = false;
                    break;

                case ScreenDone:
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        [RelayCommand]
        private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

        // 창이 닫힐 때 호출된다. 제거가 끝났으면 자기 exe 와 빈 설치 폴더를 지우는 명령을 예약한다.
        public void OnWindowClosing()
        {
            if (_isReal && _completed && !_failed)
                ScheduleSelfDelete();
        }

        private async Task RunRemoveAsync()
        {
            Percent = 0;
            Logs.Clear();
            _failed = false;
            _failReason = string.Empty;
            int stageIndex = -1;

            if (!_isReal)
                AddLog("매니페스트가 없어 미리보기 모드로 동작합니다. 실제 삭제는 하지 않습니다.");

            while (Percent < 100 && !_failed)
            {
                while (!_failed && stageIndex + 1 < Stages.Length && Percent >= Stages[stageIndex + 1].At)
                {
                    stageIndex++;
                    RemoveStage stage = Stages[stageIndex];

                    StageText = stage.Text;
                    AddLog(stage.Text);

                    if (stage.Action is null) continue;

                    try
                    {
                        await stage.Action(this);
                    }
                    catch (Exception ex)
                    {
                        _failed = true;
                        _failReason = ex.Message;
                        AddLog($"오류: {ex.Message}");
                    }
                }

                if (_failed) break;

                Percent = Math.Min(100, Percent + _rand.NextDouble() * 2.0 + 0.3);
                PercentText = $"{(int)Percent}%";

                await Task.Delay(60);
            }

            if (_failed)
            {
                StageText = "제거 실패";
                AddLog("제거가 중단되었습니다.");
            }
            else
            {
                _completed = true;
                AddLog("제거 완료. 이 창을 닫으면 제거 프로그램 자신도 삭제됩니다.");
            }
        }

        // ===== 제거 단계 =====

        // 실제 모드면 백그라운드에서 작업을 수행하고, 미리보기 모드면 잠깐 기다리기만 한다.
        private Task RunReal(Action work)
        {
            if (!_isReal)
                return Task.Delay(150);

            return Task.Run(work);
        }

        private static bool PathsEqual(string a, string b) =>
            string.Equals(
                Path.GetFullPath(a).TrimEnd('\\', '/'),
                Path.GetFullPath(b).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);

        // 잘못된 매니페스트로 시스템 폴더를 지우는 사고를 막는다.
        private static void EnsureSafeToDelete(string dir)
        {
            string full = Path.GetFullPath(dir).TrimEnd('\\', '/');

            if (full.Split('\\').Length < 2)
                throw new InvalidOperationException($"설치 폴더가 드라이브 루트입니다. 삭제하지 않습니다: {full}");

            Environment.SpecialFolder[] protectedFolders =
            {
                Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86,
                Environment.SpecialFolder.Windows,
                Environment.SpecialFolder.System,
                Environment.SpecialFolder.CommonApplicationData,
                Environment.SpecialFolder.UserProfile,
            };

            foreach (Environment.SpecialFolder f in protectedFolders)
            {
                string p = Environment.GetFolderPath(f);
                if (!string.IsNullOrEmpty(p) && PathsEqual(p, full))
                    throw new InvalidOperationException($"설치 폴더가 시스템 폴더입니다. 삭제하지 않습니다: {full}");
            }
        }

        private Task PrepareAsync() => RunReal(() =>
        {
            if (!PathsEqual(InstallDir, _model.RootPath))
                throw new InvalidOperationException(
                    $"제거 프로그램이 설치 폴더 밖에서 실행되었습니다.\n실행 위치: {InstallDir}\n설치 폴더: {_model.RootPath}");

            EnsureSafeToDelete(InstallDir);

            AddLog($"설치 폴더: {InstallDir}");
            if (!string.IsNullOrWhiteSpace(_model.DataRootPath))
                AddLog($"데이터 폴더는 유지합니다: {_model.DataRootPath}");
        });


        private Task DeleteShortcutAsync(string directory) => RunReal(() =>
        {
            string lnk = Path.Combine(directory, _model.Name + ".lnk");
            AddLog(ShortcutHelper.DeleteIfExists(lnk) ? $"삭제: {lnk}" : $"없음: {lnk}");
        });

        //시작 메뉴 > {제작자} > {프로그램}.lnk 를 지우고, 제작자 폴더가 비면 폴더도 지운다
        private Task DeleteStartMenuShortcutAsync() => RunReal(() =>
        {
            string dir = ShortcutHelper.StartMenuDirFor(_model);
            string lnk = Path.Combine(dir, _model.Name + ".lnk");
            AddLog(ShortcutHelper.DeleteIfExists(lnk) ? $"삭제: {lnk}" : $"없음: {lnk}");
            ShortcutHelper.DeleteDirectoryIfEmpty(dir);
        });

        private Task RemoveRegistryAsync() => RunReal(() =>
        {
            RegisterHelper.Remove(_model);
            AddLog("레지스트리 항목과 앱 및 기능 등록을 삭제했습니다.");
        });

        // 설치 폴더의 파일을 모두 지운다. 실행 중인 자기 exe 는 창을 닫을 때 지운다.
        private Task DeleteFilesAsync() => RunReal(() =>
        {
            int files = 0, dirs = 0;

            foreach (string f in Directory.EnumerateFiles(InstallDir, "*", SearchOption.AllDirectories))
            {
                if (PathsEqual(f, _selfPath)) continue;

                try
                {
                    File.SetAttributes(f, FileAttributes.Normal);
                    File.Delete(f);
                    files++;
                }
                catch (Exception ex)
                {
                    AddLog($"삭제 실패: {Path.GetFileName(f)} ({ex.Message})");
                }
            }

            foreach (string d in Directory.EnumerateDirectories(InstallDir, "*", SearchOption.AllDirectories)
                                          .OrderByDescending(d => d.Length))
            {
                try { Directory.Delete(d, recursive: false); dirs++; } catch { }
            }

            AddLog($"파일 {files}개, 폴더 {dirs}개를 삭제했습니다.");
        });

        // 프로세스가 끝난 뒤 자기 exe 와 (비어 있으면) 설치 폴더를 지운다.
        // timeout 은 콘솔이 없으면 실패하므로 ping 으로 2초 기다린다.
        private void ScheduleSelfDelete()
        {
            try
            {
                string command = $"/S /C \"ping -n 3 127.0.0.1 >nul & del /f /q \"{_selfPath}\" & rmdir \"{InstallDir}\"\"";
                Process.Start(new ProcessStartInfo("cmd.exe", command)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
            }
            catch
            {
                // 자기 삭제 실패는 제거 결과에 영향이 없다
            }
        }

        // 백그라운드 스레드에서도 호출되므로 UI 스레드로 넘긴다.
        private void AddLog(string text)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}]  {text}";

            Dispatcher? dispatcher = Application.Current?.Dispatcher;

            if (dispatcher is null || dispatcher.CheckAccess())
                Logs.Add(line);
            else
                dispatcher.Invoke(() => Logs.Add(line));
        }
    }
}

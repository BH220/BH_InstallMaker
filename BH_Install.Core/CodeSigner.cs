using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace BH_Install.Core
{
    //PFX 검사 결과
    public sealed class PfxCheckResult
    {
        //열기에 성공했고 개인키를 가진 인증서를 찾았다
        public bool IsValid { get; init; }
        //실패 원인이 암호 불일치다
        public bool IsWrongPassword { get; init; }
        //사람이 읽는 메시지(실패 원인)
        public string Message { get; init; } = string.Empty;
        public string SubjectName { get; init; } = string.Empty;
        public string IssuerName { get; init; } = string.Empty;
        public string Thumbprint { get; init; } = string.Empty;
        public DateTime NotAfter { get; init; }

        public bool IsExpired => IsValid && NotAfter <= DateTime.Now;
        public int DaysLeft => (int)Math.Floor((NotAfter - DateTime.Now).TotalDays);
    }

    //서명 옵션
    public sealed class SignOptions
    {
        public required string PfxPath { get; init; }
        public required string PfxPassword { get; init; }
        //RFC 3161 타임스탬프 서버. 비우면 타임스탬프를 생략한다.
        //타임스탬프가 없으면 인증서 만료와 함께 서명도 무효가 된다.
        public string TimestampUrl { get; init; } = CodeSigner.DefaultTimestampUrl;
        //UAC 대화상자에 표시될 이름
        public string? Description { get; init; }
        //이미 서명된 파일도 다시 서명한다
        public bool Force { get; init; }
    }

    //서명 결과
    public sealed class SignReport
    {
        public List<string> Signed { get; } = new();
        public List<(string File, string Signer)> Skipped { get; } = new();
        public List<(string File, string Error)> Failed { get; } = new();
        public bool IsSuccess => Failed.Count == 0;
    }

    //signtool.exe 로 exe/dll 에 Authenticode 서명을 붙인다.
    //D:\BH Soft Cert\scripts\Sign-App.ps1 과 같은 규칙을 C# 으로 옮긴 것이다.
    //  - 폴더를 주면 exe/dll 을 재귀 검색한다
    //  - 이미 서명된 파일(NuGet 서드파티 DLL 등)은 원본 서명을 지키기 위해 건너뛴다
    //  - SHA-256 서명 + RFC3161 타임스탬프
    //  - PFX 에 루트 체인이 함께 들어 있어도 개인키를 가진 인증서를 /sha1 로 명시한다
    //    (루트 CA 에 EKU 제한이 없어 signtool 이 둘 다 후보로 보고 "Multiple certificates" 로 실패하는 문제 방지)
    public static class CodeSigner
    {
        public const string DefaultTimestampUrl = "http://timestamp.digicert.com";

        //ERROR_INVALID_PASSWORD
        private const int ErrorInvalidPassword = unchecked((int)0x80070056);

        //signtool.exe 를 찾는다. PATH → Windows Kits 최신 버전의 x64 순서.
        public static string? FindSignTool()
        {
            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim(), "signtool.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch
                {
                    //잘못된 PATH 항목은 무시
                }
            }

            string?[] roots =
            {
                Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
                Environment.GetEnvironmentVariable("ProgramFiles"),
            };

            foreach (string? root in roots)
            {
                if (string.IsNullOrEmpty(root)) continue;

                string bin = Path.Combine(root, "Windows Kits", "10", "bin");
                if (!Directory.Exists(bin)) continue;

                //버전 폴더(10.0.26100.0 등)가 여러 개면 가장 높은 버전의 x64 를 쓴다
                string? found = Directory.EnumerateDirectories(bin)
                    .Select(dir => (Dir: dir, Ver: Version.TryParse(Path.GetFileName(dir), out Version? v) ? v : null))
                    .Where(x => x.Ver is not null)
                    .OrderByDescending(x => x.Ver)
                    .Select(x => Path.Combine(x.Dir, "x64", "signtool.exe"))
                    .FirstOrDefault(File.Exists);

                if (found is not null) return found;
            }

            return null;
        }

        //PFX 를 열어 개인키를 가진(서명용) 인증서를 확인한다. 암호 검증 겸용.
        public static PfxCheckResult CheckPfx(string pfxPath, string password)
        {
            if (string.IsNullOrWhiteSpace(pfxPath) || !File.Exists(pfxPath))
                return new PfxCheckResult { Message = $"PFX 파일을 찾을 수 없습니다: {pfxPath}" };

            var col = new X509Certificate2Collection();
            try
            {
                //키를 디스크에 남기지 않는다
                col.Import(pfxPath, password, X509KeyStorageFlags.EphemeralKeySet);
            }
            catch (CryptographicException ex) when (ex.HResult == ErrorInvalidPassword)
            {
                return new PfxCheckResult { IsWrongPassword = true, Message = "PFX 암호가 올바르지 않습니다." };
            }
            catch (Exception ex)
            {
                return new PfxCheckResult { Message = $"PFX 를 열 수 없습니다: {ex.Message}" };
            }

            try
            {
                X509Certificate2? signing = null;
                foreach (X509Certificate2 c in col)
                {
                    if (c.HasPrivateKey) { signing = c; break; }
                }

                if (signing is null)
                    return new PfxCheckResult { Message = "PFX 에 개인키를 가진 인증서가 없습니다." };

                return new PfxCheckResult
                {
                    IsValid = true,
                    SubjectName = signing.GetNameInfo(X509NameType.SimpleName, false),
                    IssuerName = signing.GetNameInfo(X509NameType.SimpleName, true),
                    Thumbprint = signing.Thumbprint,
                    NotAfter = signing.NotAfter,
                    Message = string.Empty
                };
            }
            finally
            {
                foreach (X509Certificate2 c in col) c.Dispose();
            }
        }

        //파일에 서명이 있으면 서명자 이름을, 없으면 null 을 돌려준다. 신뢰 체인은 검사하지 않는다.
        public static string? GetSignerName(string file)
        {
            try
            {
                using X509Certificate raw = X509Certificate.CreateFromSignedFile(file);
                using var cert = new X509Certificate2(raw);
                return cert.GetNameInfo(X509NameType.SimpleName, false);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        //서명 대상 수집: 폴더면 exe/dll 재귀, 파일이면 그 파일
        public static IReadOnlyList<string> CollectTargets(IEnumerable<string> paths)
        {
            var list = new List<string>();
            foreach (string p in paths)
            {
                if (Directory.Exists(p))
                {
                    list.AddRange(Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                                 || f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)));
                }
                else if (File.Exists(p))
                {
                    list.Add(p);
                }
            }
            return list;
        }

        //paths 의 exe/dll 에 서명한다. 진행 상황은 log 로 알린다. 암호는 절대 log 에 쓰지 않는다.
        public static async Task<SignReport> SignAsync(
            IEnumerable<string> paths, SignOptions options, Action<string>? log = null, CancellationToken ct = default)
        {
            var report = new SignReport();

            string signtool = FindSignTool()
                ?? throw new FileNotFoundException("signtool.exe 를 찾을 수 없습니다. Windows SDK 를 설치하세요.");
            log?.Invoke($"signtool: {signtool}");

            PfxCheckResult pfx = CheckPfx(options.PfxPath, options.PfxPassword);
            if (!pfx.IsValid)
                throw new InvalidOperationException(pfx.Message);
            if (pfx.IsExpired)
                throw new InvalidOperationException($"인증서가 만료되었습니다 ({pfx.NotAfter:yyyy-MM-dd}). 재발급이 필요합니다.");

            log?.Invoke($"인증서: {pfx.SubjectName} ({pfx.Thumbprint}), 만료 {pfx.NotAfter:yyyy-MM-dd}");
            log?.Invoke(string.IsNullOrWhiteSpace(options.TimestampUrl)
                ? "타임스탬프: 사용 안 함 (인증서 만료 시 서명도 무효가 됩니다)"
                : $"타임스탬프: {options.TimestampUrl.Trim()}");

            IReadOnlyList<string> targets = CollectTargets(paths);
            if (targets.Count == 0)
            {
                log?.Invoke("서명할 파일이 없습니다.");
                return report;
            }

            //이미 서명된 파일은 건너뛴다 (서드파티 DLL 의 원본 서명 보존)
            var toSign = new List<string>();
            foreach (string f in targets)
            {
                string? signer = options.Force ? null : GetSignerName(f);
                if (signer is null) toSign.Add(f);
                else report.Skipped.Add((f, signer));
            }

            if (report.Skipped.Count > 0)
            {
                log?.Invoke($"이미 서명되어 건너뜀 {report.Skipped.Count}개:");
                foreach ((string file, string signer) in report.Skipped)
                    log?.Invoke($"  {Path.GetFileName(file)}  ({signer})");
            }

            if (toSign.Count == 0)
            {
                log?.Invoke("새로 서명할 파일이 없습니다.");
                return report;
            }

            log?.Invoke($"서명 진행 {toSign.Count}개:");
            foreach (string f in toSign)
            {
                ct.ThrowIfCancellationRequested();

                (int code, string output) = await RunSignToolAsync(signtool, options, pfx.Thumbprint, f, ct);

                if (code == 0 && GetSignerName(f) is { } signedBy)
                {
                    report.Signed.Add(f);
                    log?.Invoke($"  {Path.GetFileName(f)} ... 서명 완료 ({signedBy})");
                }
                else
                {
                    string error = code == 0 ? "서명 후 확인에 실패했습니다." : Summarize(output, code);
                    report.Failed.Add((f, error));
                    log?.Invoke($"  {Path.GetFileName(f)} ... 실패: {error}");
                }
            }

            log?.Invoke($"서명 {report.Signed.Count}개, 건너뜀 {report.Skipped.Count}개, 실패 {report.Failed.Count}개");
            return report;
        }

        private static async Task<(int ExitCode, string Output)> RunSignToolAsync(
            string signtool, SignOptions o, string thumbprint, string file, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = signtool,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            //ArgumentList 를 쓰면 공백·따옴표 이스케이프를 신경 쓰지 않아도 된다
            var a = psi.ArgumentList;
            a.Add("sign");
            a.Add("/fd"); a.Add("SHA256");
            a.Add("/f"); a.Add(o.PfxPath);
            a.Add("/p"); a.Add(o.PfxPassword);
            a.Add("/sha1"); a.Add(thumbprint);
            if (!string.IsNullOrWhiteSpace(o.TimestampUrl))
            {
                a.Add("/tr"); a.Add(o.TimestampUrl.Trim());
                a.Add("/td"); a.Add("SHA256");
            }
            if (!string.IsNullOrWhiteSpace(o.Description))
            {
                a.Add("/d"); a.Add(o.Description.Trim());
            }
            a.Add(file);

            using var p = new Process { StartInfo = psi };
            p.Start();

            Task<string> stdout = p.StandardOutput.ReadToEndAsync(ct);
            Task<string> stderr = p.StandardError.ReadToEndAsync(ct);

            try
            {
                await p.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                throw;
            }

            string output = await stdout + Environment.NewLine + await stderr;
            return (p.ExitCode, output);
        }

        //signtool 출력에서 의미 있는 줄 하나를 고른다. signtool 은 암호를 출력하지 않는다.
        private static string Summarize(string output, int code)
        {
            List<string> lines = output
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            string? err = lines.FirstOrDefault(l => l.Contains("Error", StringComparison.OrdinalIgnoreCase))
                          ?? lines.FirstOrDefault();

            return err is null ? $"exit {code}" : $"{err} (exit {code})";
        }
    }
}
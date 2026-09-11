using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace BH_InstallerMaker.Services
{
    //csproj에서 읽어낸 프로젝트 정보. IconPath 는 ApplicationIcon 파일의 절대 경로(없으면 null).
    public record ProjectInfo(string Stem, string Version, string MainExeName, string TargetFramework, string? IconPath);

    //배포 대상 프로젝트 읽기/게시 담당
    public class ProjectService
    {
        //dotnet 은 자기 콘솔의 코드 페이지(환경에 따라 949 또는 65001)로 출력하고, .NET 의 자식 출력 읽기는
        //다른 규칙으로 인코딩을 고르므로 환경에 따라 한글이 깨진다. 바이트를 직접 받아 줄마다
        //엄격한 UTF-8 로 먼저 해석하고, 실패하면 CP949 로 해석한다.
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
        private static readonly Encoding Cp949;

        static ProjectService()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Cp949 = Encoding.GetEncoding(949);
        }

        private static string DecodeLine(byte[] bytes, int count)
        {
            try { return StrictUtf8.GetString(bytes, 0, count); }
            catch (DecoderFallbackException) { return Cp949.GetString(bytes, 0, count); }
        }

        //스트림을 줄 단위 바이트로 읽어 디코딩해 콜백에 넘긴다 (\r\n, \n 모두 줄 끝으로 본다)
        private static async Task PumpAsync(Stream stream, Action<string> onLine, CancellationToken ct)
        {
            var line = new MemoryStream();
            byte[] buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    byte b = buffer[i];
                    if (b == (byte)'\n')
                    {
                        onLine(DecodeLine(line.GetBuffer(), (int)line.Length).TrimEnd('\r'));
                        line.SetLength(0);
                    }
                    else
                    {
                        line.WriteByte(b);
                    }
                }
            }
            if (line.Length > 0)
                onLine(DecodeLine(line.GetBuffer(), (int)line.Length).TrimEnd('\r'));
        }

        public ProjectInfo Read(string csprojPath)
        {
            var doc = XDocument.Load(csprojPath);

            string? Prop(string name) =>
                doc.Descendants()
                   .FirstOrDefault(x => x.Name.LocalName == name && !string.IsNullOrWhiteSpace(x.Value))?
                   .Value.Trim();

            //$(변수) 형태면 csproj 안의 해당 프로퍼티 값으로 해석
            string? Resolve(string? value)
            {
                for (int i = 0; i < 5 && value is not null; i++)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(value, @"^\$\((\w+)\)$");
                    if (!match.Success)
                        return value;
                    value = Prop(match.Groups[1].Value);
                }
                return value;
            }

            string stem = Path.GetFileNameWithoutExtension(csprojPath);
            string version = Resolve(Prop("FileVersion"))
                             ?? Resolve(Prop("Version"))
                             ?? Resolve(Prop("AssemblyVersion"))
                             ?? "1.0.0";
            string mainExe = (Resolve(Prop("AssemblyName")) ?? stem) + ".exe";
            string tf = Prop("TargetFramework") ?? Prop("TargetFrameworks") ?? "?";

            //exe 아이콘. 런처가 같은 아이콘을 쓰기 위해 파일 경로를 확인한다.
            string? iconPath = null;
            string? icon = Resolve(Prop("ApplicationIcon"));
            if (!string.IsNullOrWhiteSpace(icon))
            {
                string full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(csprojPath)!, icon));
                if (File.Exists(full)) iconPath = full;
            }

            return new ProjectInfo(stem, version, mainExe, tf, iconPath);
        }

        //dotnet publish 실행. 출력은 호출한 스레드(UI)로 마샬링하여 log 콜백에 전달.
        //extraArgs 로 -p:이름=값 같은 추가 인수를 넘길 수 있다.
        public async Task<int> PublishAsync(
            string csprojPath, string workingDir, string outDir, Action<string> log,
            IEnumerable<string>? extraArgs = null, CancellationToken ct = default)
        {
            var context = SynchronizationContext.Current;
            void Post(string message)
            {
                if (context is null)
                    log(message);
                else
                    context.Post(_ => log(message), null);
            }

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDir,
            };

            //ArgumentList 를 쓰면 공백이 든 경로를 따로 감쌀 필요가 없다
            psi.ArgumentList.Add("publish");
            psi.ArgumentList.Add(csprojPath);
            psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("Release");
            psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(outDir);
            psi.ArgumentList.Add("--nologo");
            if (extraArgs is not null)
                foreach (string a in extraArgs) psi.ArgumentList.Add(a);

            log("  > dotnet " + string.Join(" ", psi.ArgumentList.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)));

            using var process = new Process { StartInfo = psi };
            process.Start();

            //인코딩을 스스로 판별하기 위해 StreamReader 대신 바이트 스트림을 읽는다
            Task stdout = PumpAsync(process.StandardOutput.BaseStream, l => { if (!string.IsNullOrWhiteSpace(l)) Post("  " + l.Trim()); }, ct);
            Task stderr = PumpAsync(process.StandardError.BaseStream, l => { if (!string.IsNullOrWhiteSpace(l)) Post("  [오류] " + l.Trim()); }, ct);

            try
            {
                await process.WaitForExitAsync(ct);
                await Task.WhenAll(stdout, stderr);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }

            return process.ExitCode;
        }
    }
}
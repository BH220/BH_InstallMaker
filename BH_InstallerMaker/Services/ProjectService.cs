using System.Diagnostics;
using System.IO;
using System.Xml.Linq;

namespace BH_InstallerMaker.Services
{
    //csproj에서 읽어낸 프로젝트 정보
    public record ProjectInfo(string Stem, string Version, string MainExeName, string TargetFramework);

    //배포 대상 프로젝트 읽기/게시 담당
    public class ProjectService
    {
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

            return new ProjectInfo(stem, version, mainExe, tf);
        }

        //dotnet publish 실행. 출력은 호출한 스레드(UI)로 마샬링하여 log 콜백에 전달
        public async Task<int> PublishAsync(string csprojPath, string workingDir, string outDir, Action<string> log)
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
                Arguments = $"publish \"{csprojPath}\" -c Release -o \"{outDir}\" --nologo",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDir,
            };

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, ea) =>
            {
                if (!string.IsNullOrWhiteSpace(ea.Data))
                    Post("  " + ea.Data.Trim());
            };
            process.ErrorDataReceived += (_, ea) =>
            {
                if (!string.IsNullOrWhiteSpace(ea.Data))
                    Post("  [오류] " + ea.Data.Trim());
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
    }
}

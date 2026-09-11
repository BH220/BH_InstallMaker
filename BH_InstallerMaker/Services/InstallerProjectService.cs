using System.IO;
using BH_Install.Core;

namespace BH_InstallerMaker.Services
{
    //대상 프로젝트별 설치 설정(<프로젝트>.bhinstaller.json) 읽기/쓰기.
    //csproj 옆에 두어 대상 프로젝트 저장소와 함께 버전 관리되게 한다.
    public sealed class InstallerProjectService
    {
        public const string Extension = ".bhinstaller.json";

        public static string GetPath(string csprojPath) => Path.ChangeExtension(csprojPath, Extension);

        public ProgramModel? Load(string csprojPath)
        {
            string path = GetPath(csprojPath);
            try
            {
                return File.Exists(path) ? ProgramManifest.FromJson(File.ReadAllText(path)) : null;
            }
            catch
            {
                //손상된 파일은 새 설정으로 시작
                return null;
            }
        }

        public void Save(string csprojPath, ProgramModel model)
        {
            File.WriteAllText(GetPath(csprojPath), ProgramManifest.ToJson(model));
        }
    }
}
namespace BH_Install.Core.Common
{
    //설치 대상 프로그램 정보(프로그램 매니페스트).
    //메이커가 [설치 파일 빌드] 때 ProgramManifest.SaveToModel() 로 BH_Install.Core\Resources\ProgramModel.json 에 저장하고,
    //그 파일이 포함 리소스로 BH_Install.Core.dll 에 들어간다. 설치·런처·언인스톨 모듈은 ProgramManifest.Instance.ProgramModel 로 읽는다.
    public class ProgramModel
    {
        //프로그램 이름. "앱 및 기능" 의 Uninstall 레지스트리 키 이름으로도 쓴다.
        public string Name { get; set; } = "";
        //프로그램 제작자
        public string Publisher { get; set; } = "";
        //프로그램 버전
        public string Version { get; set; } = "";
        //프로그램 설명
        public string Description { get; set; } = "";
        //프로그램 실행 루트 경로
        public string RootPath { get; set; } = "";
        //프로그램 데이터 저장 루트 경로
        public string DataRootPath { get; set; } = "";
        //레지스트리를 사용한 경우 그 루트 경로(HKEY_LOCAL_MACHINE만 가능)
        public string RegistryKey { get; set; } = "";
        //런처가 실행할 대상 프로그램 exe (RootPath 기준 상대 경로). 메이커가 csproj 의 AssemblyName 으로 자동 결정한다.
        public string MainExe { get; set; } = "";
        //런처가 업데이트 목록과 파일을 조회하는 서버 주소. 프로그램마다 다르다. 예: https://update.bhsoft.co.kr/myapp/
        public string UpdateUrl { get; set; } = "";
        //프로그램 아이콘
        public string MainIcon { get; set; } = "";
        //런처의 버전
        public string VerLauncher { get; set; } = "";
        //라이선스 인증은 모든 프로그램이 필수로 사용한다. DB 에서 확인하는 프로그램 ID.
        public int ProgramId { get; set; } = 0;
        //윈도우 실행창을 통해 바로 실행 가능한 명령어를 지정한다. 빈값인 경우 처리하지 않는다.
        public string ExeCommand { get; set; } = "";
    }
}
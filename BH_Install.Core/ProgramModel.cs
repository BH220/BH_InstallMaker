namespace BH_Install.Core
{
    public class ProgramModel
    {
        //프로그램이름(윈도우 서비스일 경우 표시 이름)
        public string Name { get; set; }
        //프로그램 제작자
        public string Publisher { get; set; }
        //프로그램 버전
        public string Version { get; set; }
        //프로그램 설명
        public string Description { get; set; }
        //프로그램 실행 루트 경로
        public string RootPath { get; set; }
        //프로그램 데이터 저장 루트 경로
        public string DataRootPath { get; set; }
        //레지스트리를 사용한 경우 그 루트 경로(HKEY_LOCAL_MACHINE만 가능)
        public string RegistryKey { get; set; }

        //윈도우 서비스 사용시
        public bool UseWindowsService { get; set; } = false;
        //윈도우서비스로 동작할 경우 서비스 이름(영문, 띄어쓰기 없음)
        public string WindowsServiceName { get; set; }
        //윈도우서비스로 동작할 경우 서비스 설명(한글, 띄어쓰기 가능)
        public string WindowsServiceDescription { get; set; }

        //라이선스를 확인하는 경우
        public bool UseLicense { get; set; } = false;
        //라이선스를 확인하는 경우 프로그램 ID(DB에서 확인)
        public int ProgramId { get; set; }
    }
}

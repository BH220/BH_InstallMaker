namespace BH_Install.Core
{
    //라이선스 서버 요청 종류 코드
    public static class LicenseRequestType
    {
        //활성 요청
        public const string Activate = "120001";
    }

    //라이선스 서버에 보내는 요청 값. 설치 프로그램과 런처가 같은 형식을 쓴다.
    public sealed record LicenseRequest(
        string ProgramId,
        string RequestType,
        string Ip,
        string Mac,
        string PcName,
        string UserName,
        string LicenseKey)
    {
        //PC 식별 정보를 모아 요청을 만든다. 공인 IP 조회는 네트워크를 타므로 비동기다.
        public static async Task<LicenseRequest> CreateAsync(
            int programId, string requestType, string licenseKey, CancellationToken ct = default)
        {
            string ip = await MachineInfo.GetPublicIpAsync(ct);
            return new LicenseRequest(
                ProgramId: programId.ToString(),
                RequestType: requestType,
                Ip: ip,
                Mac: MachineInfo.GetMacAddress(),
                PcName: MachineInfo.PcName,
                UserName: MachineInfo.UserName,
                LicenseKey: licenseKey.Trim());
        }
    }
}
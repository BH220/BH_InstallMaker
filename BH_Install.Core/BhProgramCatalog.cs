namespace BH_Install.Core
{
    //라이선스 DB 의 프로그램 번호(program_num). DB 조회 대신 하드코딩한다.
    //값이 곧 DB 의 번호이므로 바꾸면 안 된다.
    public enum BhProgramId
    {
        SecurityCode = 2,
        CarLog = 3,
        Commander = 4,
        ServerManager = 5,
        SimpleStockManager = 6,
        Anniversary = 7,
        VpnBrowser = 8,
    }

    //프로그램 한 건의 표시 정보
    public sealed record BhProgramInfo(BhProgramId Id, string Name, string NameKr)
    {
        //DB 의 program_num
        public int Number => (int)Id;

        //콤보박스 등에 보이는 이름. 예: "2: BH Security Code(시큐리티 코드)"
        public string DisplayName => $"{Number}: {Name}({NameKr})";

        public override string ToString() => DisplayName;
    }

    //DB 테이블(program_num, NAME, name_kr)을 그대로 옮긴 목록
    public static class BhProgramCatalog
    {
        public static readonly IReadOnlyList<BhProgramInfo> All = new[]
        {
            new BhProgramInfo(BhProgramId.SecurityCode,       "BH Security Code",     "시큐리티 코드"),
            new BhProgramInfo(BhProgramId.CarLog,             "BH CarLog",            "BH 차계부"),
            new BhProgramInfo(BhProgramId.Commander,          "BH Commander",         "BH 단축명령"),
            new BhProgramInfo(BhProgramId.ServerManager,      "BH ServerManager",     "BH 서버매니저"),
            new BhProgramInfo(BhProgramId.SimpleStockManager, "Simple Stock Manager", "간편 재고 관리 프로그램"),
            new BhProgramInfo(BhProgramId.Anniversary,        "BH Anniversary",       "BH 기념일 관리"),
            new BhProgramInfo(BhProgramId.VpnBrowser,         "BH VpnBrowser",        "BH VPN 브라우저"),
        };

        //번호로 찾는다. 없으면 null.
        public static BhProgramInfo? Find(int number) => All.FirstOrDefault(p => p.Number == number);
    }
}
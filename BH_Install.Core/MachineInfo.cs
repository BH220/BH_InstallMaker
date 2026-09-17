using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;

namespace BH_Install.Core
{
    //라이선스 요청에 넣는 PC 식별 정보를 모은다. 설치 프로그램과 런처가 함께 쓴다.
    public static class MachineInfo
    {
        //공인 IP 를 알려주는 서비스. 앞에서부터 시도하고 실패하면 다음으로 넘어간다.
        private static readonly string[] PublicIpServices =
        {
            "https://api.ipify.org",
            "https://checkip.amazonaws.com",
            "https://ifconfig.me/ip",
            "https://icanhazip.com",
        };

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

        public static string PcName => Environment.MachineName;

        //현재 프로세스의 사용자. 관리자 승격을 다른 계정으로 했다면 그 계정 이름이 된다.
        public static string UserName => Environment.UserName;

        //외부에서 보이는 공인 IP. 사설망 IP(192.168.x.x 등)가 아니라 인터넷 쪽 주소다.
        //어느 서비스에도 닿지 못하면 빈 문자열.
        public static async Task<string> GetPublicIpAsync(CancellationToken ct = default)
        {
            foreach (string url in PublicIpServices)
            {
                try
                {
                    string text = (await Http.GetStringAsync(url, ct)).Trim();
                    if (IPAddress.TryParse(text, out IPAddress? ip) && !IsPrivate(ip))
                        return ip.ToString();
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    //다음 서비스로
                }
            }
            return string.Empty;
        }

        //실제 통신에 쓰는 물리 NIC 의 MAC. 형식 AA-BB-CC-DD-EE-FF. 못 찾으면 빈 문자열.
        //기본 게이트웨이가 있는 인터페이스를 우선하고, 가상·VPN·블루투스 어댑터는 뒤로 미룬다.
        public static string GetMacAddress()
        {
            static bool LooksVirtual(NetworkInterface n)
            {
                string d = (n.Description + " " + n.Name).ToLowerInvariant();
                return d.Contains("virtual") || d.Contains("vmware") || d.Contains("hyper-v") || d.Contains("vethernet")
                    || d.Contains("vpn") || d.Contains("tap") || d.Contains("tun") || d.Contains("bluetooth")
                    || d.Contains("loopback") || d.Contains("wsl") || d.Contains("docker");
            }

            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211
                         || n.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet)
                .Where(n => n.GetPhysicalAddress().GetAddressBytes().Length == 6)
                .OrderByDescending(n => n.GetIPProperties().GatewayAddresses.Count > 0)   //게이트웨이 있는 것 우선
                .ThenBy(LooksVirtual)                                                   //가상 어댑터는 뒤로
                .ThenBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) //유선 우선
                .ToList();

            NetworkInterface? nic = candidates.FirstOrDefault();
            if (nic is null) return string.Empty;

            return string.Join("-", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
        }

        //사설/특수 대역이면 true (10.x, 172.16~31.x, 192.168.x, 127.x, 169.254.x, IPv6 링크로컬·유니크로컬)
        private static bool IsPrivate(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip)) return true;

            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                byte[] b = ip.GetAddressBytes();
                return b[0] == 10
                    || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                    || (b[0] == 192 && b[1] == 168)
                    || (b[0] == 169 && b[1] == 254);
            }

            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
        }
    }
}
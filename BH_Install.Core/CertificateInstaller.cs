using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace BH_Install.Core
{
    //루트 인증서 설치 결과 상태
    public enum CertInstallStatus
    {
        //새로 설치했다
        Installed,
        //같은 지문의 인증서가 이미 있어 건너뛰었다
        AlreadyInstalled,
        //실패했다(권한 부족, 리소스 없음 등)
        Failed
    }

    //루트 인증서 설치 결과
    public sealed class CertInstallReport
    {
        public CertInstallStatus Status { get; init; }
        //로그나 안내에 쓸 사람이 읽는 메시지
        public string Message { get; init; } = string.Empty;
        //설치 대상 인증서 지문
        public string Thumbprint { get; init; } = string.Empty;
        //설치 대상 인증서 표시 이름
        public string SubjectName { get; init; } = string.Empty;
        //실패한 경우 원인 예외
        public Exception? Error { get; init; }

        //설치 자체가 실패하지 않았는지(이미 설치된 경우도 성공으로 본다)
        public bool IsSuccess => Status != CertInstallStatus.Failed;
    }

    //BH Soft 사설 루트 CA 인증서를 로컬 컴퓨터의 신뢰할 수 있는 루트 저장소에 설치한다.
    //
    //코드 서명(Authenticode) 검증은 서명에 쓴 인증서의 발급자 체인이
    //신뢰할 수 있는 루트로 끝나야 통과한다. 공인 CA가 아닌 사설 CA로 서명한 경우
    //이 루트를 클라이언트 PC에 등록하지 않으면 체인이 UntrustedRoot 로 끝나
    //서명이 붙어 있어도 "알 수 없는 게시자"로 표시된다.
    //
    //주의: SmartScreen 경고는 Microsoft 평판 기반이므로 이 설치로 사라지지 않는다.
    public static class CertificateInstaller
    {
        //임베디드 리소스의 논리 이름. csproj 의 LogicalName 과 일치해야 한다.
        private const string RootCertResourceName = "BH_Install.Core.Resources.BHSoft_RootCA.cer";

        //관리자 권한으로 실행 중인지. LocalMachine 저장소 쓰기에 필요하다.
        public static bool IsElevated()
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        //임베디드 리소스에서 루트 CA 인증서(공개키)를 읽는다.
        //별도 파일로 배포하지 않으므로 배포 중 누락이나 교체 위험이 없다.
        public static X509Certificate2 LoadEmbeddedRoot()
        {
            Assembly asm = typeof(CertificateInstaller).Assembly;

            using Stream? stream = asm.GetManifestResourceStream(RootCertResourceName);
            if (stream is null)
            {
                //리소스 이름이 어긋나면 여기서 걸린다. 실제 포함된 이름을 함께 알려준다.
                string names = string.Join(", ", asm.GetManifestResourceNames());
                throw new InvalidOperationException(
                    $"루트 인증서 리소스를 찾을 수 없습니다: {RootCertResourceName} (포함된 리소스: {names})");
            }

            byte[] buffer = new byte[stream.Length];
            stream.ReadExactly(buffer);

            return new X509Certificate2(buffer);
        }

        //루트 CA 인증서를 신뢰할 수 있는 루트 저장소에 설치한다.
        //storeLocation 을 CurrentUser 로 주면 관리자 권한 없이 되지만
        //Windows 보안 경고 대화상자가 뜨고 해당 사용자에게만 적용된다.
        public static CertInstallReport InstallRoot(
            Action<string>? log = null,
            StoreLocation storeLocation = StoreLocation.LocalMachine)
        {
            X509Certificate2? cert = null;

            try
            {
                cert = LoadEmbeddedRoot();

                string subject = cert.GetNameInfo(X509NameType.SimpleName, false);
                string thumbprint = cert.Thumbprint ?? string.Empty;

                log?.Invoke($"루트 인증서: {subject} ({thumbprint})");

                if (storeLocation == StoreLocation.LocalMachine && !IsElevated())
                {
                    return Fail(cert, subject, thumbprint,
                        "관리자 권한이 없어 루트 인증서를 설치할 수 없습니다.", null, log);
                }

                using var store = new X509Store(StoreName.Root, storeLocation);
                store.Open(OpenFlags.ReadWrite);

                //같은 지문이 이미 있으면 다시 넣지 않는다.
                //validOnly 를 false 로 두어 만료 여부와 무관하게 지문으로만 찾는다.
                X509Certificate2Collection found =
                    store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);

                if (found.Count > 0)
                {
                    log?.Invoke("루트 인증서가 이미 설치되어 있어 건너뜁니다.");
                    return new CertInstallReport
                    {
                        Status = CertInstallStatus.AlreadyInstalled,
                        Message = "루트 인증서가 이미 설치되어 있습니다.",
                        Thumbprint = thumbprint,
                        SubjectName = subject
                    };
                }

                store.Add(cert);
                log?.Invoke("루트 인증서를 설치했습니다.");

                return new CertInstallReport
                {
                    Status = CertInstallStatus.Installed,
                    Message = "루트 인증서를 설치했습니다.",
                    Thumbprint = thumbprint,
                    SubjectName = subject
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                return Fail(cert, ex, "루트 저장소에 쓸 권한이 없습니다. 관리자 권한으로 실행하세요.", log);
            }
            catch (CryptographicException ex)
            {
                //권한 부족이나 사용자가 보안 경고에서 [아니요]를 누른 경우 여기로 온다.
                return Fail(cert, ex, $"루트 인증서 설치가 거부되었습니다. ({ex.Message})", log);
            }
            catch (Exception ex)
            {
                return Fail(cert, ex, $"루트 인증서 설치 중 오류가 발생했습니다. ({ex.Message})", log);
            }
            finally
            {
                cert?.Dispose();
            }
        }

        //루트 인증서가 이미 신뢰 저장소에 있는지 확인한다.
        //설치 전 안내 문구를 바꾸거나 설치 단계를 건너뛸 때 쓴다.
        public static bool IsRootTrusted(StoreLocation storeLocation = StoreLocation.LocalMachine)
        {
            try
            {
                using X509Certificate2 cert = LoadEmbeddedRoot();
                using var store = new X509Store(StoreName.Root, storeLocation);
                store.Open(OpenFlags.ReadOnly);

                return store.Certificates
                    .Find(X509FindType.FindByThumbprint, cert.Thumbprint ?? string.Empty, false)
                    .Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static CertInstallReport Fail(
            X509Certificate2? cert, Exception? ex, string message, Action<string>? log)
        {
            string subject = string.Empty;
            string thumbprint = string.Empty;

            if (cert is not null)
            {
                subject = cert.GetNameInfo(X509NameType.SimpleName, false);
                thumbprint = cert.Thumbprint ?? string.Empty;
            }

            return Fail(cert, subject, thumbprint, message, ex, log);
        }

        private static CertInstallReport Fail(
            X509Certificate2? cert, string subject, string thumbprint,
            string message, Exception? ex, Action<string>? log)
        {
            log?.Invoke($"경고: {message}");

            return new CertInstallReport
            {
                Status = CertInstallStatus.Failed,
                Message = message,
                Thumbprint = thumbprint,
                SubjectName = subject,
                Error = ex
            };
        }
    }
}

namespace BH_InstallerMaker.Services
{
    //뷰모델이 뷰(다이얼로그)에 직접 의존하지 않도록 분리
    public interface IFileDialogService
    {
        string? OpenFile(string title, string filter);
    }
}

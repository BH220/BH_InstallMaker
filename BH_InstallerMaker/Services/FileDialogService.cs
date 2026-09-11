using Microsoft.Win32;

namespace BH_InstallerMaker.Services
{
    public class FileDialogService : IFileDialogService
    {
        public string? OpenFile(string title, string filter)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}

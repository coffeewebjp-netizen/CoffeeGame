#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CoffeeGame.Persistence
{
    public static class WindowsFolderPicker
    {
        private const uint FOS_PICKFOLDERS = 0x00000020;
        private const uint FOS_FORCEFILESYSTEM = 0x00000040;
        private const uint FOS_PATHMUSTEXIST = 0x00000800;
        private const uint SIGDN_FILESYSPATH = 0x80058000;

        public static string PreferredStartFolder()
        {
            if (!string.IsNullOrWhiteSpace(CloudSaveSettings.FolderPath)
                && Directory.Exists(CloudSaveSettings.FolderPath)
                && !LooksLikeGuessedUserProfileDrive(CloudSaveSettings.FolderPath))
            {
                return CloudSaveSettings.FolderPath;
            }

            if (Directory.Exists(@"I:\"))
            {
                return Directory.Exists(@"I:\CoffeeGAME") ? @"I:\CoffeeGAME" : @"I:\";
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        public static bool LooksLikeGuessedUserProfileDrive(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return false;
            }

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string full = Path.GetFullPath(folder);
            return full.StartsWith(Path.Combine(profile, "Google Drive"), StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(Path.Combine(profile, "GoogleDrive"), StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(Path.Combine(profile, "マイドライブ"), StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryPick(string title, out string folder)
        {
            folder = null;
            IFileOpenDialog dialog = null;
            IShellItem startItem = null;
            IShellItem result = null;
            try
            {
                dialog = (IFileOpenDialog)new FileOpenDialogRCW();
                dialog.SetOptions(FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);
                dialog.SetTitle(string.IsNullOrWhiteSpace(title) ? "セーブ先フォルダを選択" : title);

                string start = PreferredStartFolder();
                if (!string.IsNullOrEmpty(start)
                    && SHCreateItemFromParsingName(start, IntPtr.Zero, typeof(IShellItem).GUID, out startItem) == 0
                    && startItem != null)
                {
                    dialog.SetFolder(startItem);
                }

                if (dialog.Show(GetActiveWindow()) != 0)
                {
                    return false;
                }

                dialog.GetResult(out result);
                result.GetDisplayName(SIGDN_FILESYSPATH, out folder);
                return !string.IsNullOrWhiteSpace(folder);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (result != null)
                {
                    Marshal.ReleaseComObject(result);
                }

                if (startItem != null)
                {
                    Marshal.ReleaseComObject(startItem);
                }

                if (dialog != null)
                {
                    Marshal.ReleaseComObject(dialog);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IShellItem ppv);

        [ComImport]
        [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialogRCW
        {
        }

        [ComImport]
        [Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint fos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults(out IntPtr ppenum);
            void GetSelectedItems(out IntPtr ppsai);
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }
    }
}
#endif

using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SystemCompass.App;

/// <summary>Reads the shell's small icon for a local executable, with a cache.</summary>
internal static class IconLoader
{
    private const uint ShgfiIcon = 0x100;
    private const uint ShgfiSmallIcon = 0x1;

    private static readonly Dictionary<string, ImageSource?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? Get(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var key = path!;
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var info = new ShFileInfo();
        var result = SHGetFileInfo(key, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiSmallIcon);
        ImageSource? image = null;
        if (result != IntPtr.Zero && info.Icon != IntPtr.Zero)
        {
            try
            {
                image = Imaging.CreateBitmapSourceFromHIcon(
                    info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                image.Freeze();
            }
            finally
            {
                DestroyIcon(info.Icon);
            }
        }

        Cache[key] = image;
        return image;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string path, uint attributes, ref ShFileInfo info, uint size, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }
}

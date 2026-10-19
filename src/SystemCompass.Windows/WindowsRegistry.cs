using Microsoft.Win32;

namespace SystemCompass.Windows;

internal static class WindowsRegistry
{
    public static RegistryKey OpenBase(string hive) => hive.ToUpperInvariant() switch
    {
        "HKCU" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64),
        "HKLM" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
        _ => throw new ArgumentOutOfRangeException(nameof(hive), hive, "Unsupported hive."),
    };
}

using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using SystemCompass.Core.Tweaks;

namespace SystemCompass.Windows;

public sealed class WindowsTweaks : ApplyTweaks
{
    public TweakValue? ReadRegistry(RegistryTarget target)
    {
        using var baseKey = WindowsRegistry.OpenBase(target.Hive);
        using var key = baseKey.OpenSubKey(target.KeyPath);
        if (key is null)
        {
            return null;
        }

        var value = key.GetValue(target.ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null)
        {
            return null;
        }

        var kind = MapKind(key.GetValueKind(target.ValueName));
        var data = value switch
        {
            // DWord values above int.MaxValue come back as negative ints.
            int number => unchecked((uint)number).ToString(CultureInfo.InvariantCulture),
            long number => unchecked((ulong)number).ToString(CultureInfo.InvariantCulture),
            byte[] bytes => Convert.ToHexString(bytes),
            _ => value.ToString() ?? string.Empty,
        };
        return new TweakValue(kind, data);
    }

    public void WriteRegistry(RegistryTarget target, TweakValue value)
    {
        using var baseKey = WindowsRegistry.OpenBase(target.Hive);
        using var key = baseKey.CreateSubKey(target.KeyPath, writable: true)
            ?? throw new InvalidOperationException($"Registry key '{target.KeyPath}' could not be created.");
        key.SetValue(target.ValueName, ConvertValue(value), MapKind(value.Kind));
    }

    public void DeleteRegistry(RegistryTarget target)
    {
        using var baseKey = WindowsRegistry.OpenBase(target.Hive);
        using var key = baseKey.OpenSubKey(target.KeyPath, writable: true);
        key?.DeleteValue(target.ValueName, throwOnMissingValue: false);
    }

    public ServiceStartModeKind? ReadServiceStart(string serviceName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{serviceName}");
        return (key?.GetValue("Start") as int?) switch
        {
            0 => ServiceStartModeKind.Boot,
            1 => ServiceStartModeKind.System,
            2 => ServiceStartModeKind.Automatic,
            3 => ServiceStartModeKind.Manual,
            4 => ServiceStartModeKind.Disabled,
            _ => null,
        };
    }

    public void SetServiceStart(string serviceName, ServiceStartModeKind mode)
    {
        var startMode = mode switch
        {
            ServiceStartModeKind.Boot => "boot",
            ServiceStartModeKind.System => "system",
            ServiceStartModeKind.Automatic => "auto",
            ServiceStartModeKind.Manual => "demand",
            ServiceStartModeKind.Disabled => "disabled",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported start mode."),
        };

        var startInfo = new ProcessStartInfo("sc.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("config");
        startInfo.ArgumentList.Add(serviceName);
        startInfo.ArgumentList.Add("start=");
        startInfo.ArgumentList.Add(startMode);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("sc.exe could not be started.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"sc config failed with exit code {process.ExitCode}: {error.Trim()} {output.Trim()}".Trim());
        }
    }

    public uint? ReadPowerSetting(Guid subGroup, Guid setting, bool ac)
    {
        var scheme = PowerNative.GetActiveScheme();
        return scheme is null ? null : PowerNative.Read(scheme.Value, subGroup, setting, ac);
    }

    public void WritePowerSetting(Guid subGroup, Guid setting, bool ac, uint value)
    {
        var scheme = PowerNative.GetActiveScheme()
            ?? throw new InvalidOperationException("No active power scheme is available.");
        PowerNative.Write(scheme, subGroup, setting, ac, value);
    }

    private static object ConvertValue(TweakValue value) => value.Kind switch
    {
        RegistryValueKindKind.String or RegistryValueKindKind.ExpandString => value.Data,
        RegistryValueKindKind.DWord => uint.Parse(value.Data, CultureInfo.InvariantCulture),
        RegistryValueKindKind.QWord => long.Parse(value.Data, CultureInfo.InvariantCulture),
        RegistryValueKindKind.Binary => Convert.FromHexString(value.Data),
        _ => throw new ArgumentOutOfRangeException(nameof(value), value.Kind, "Unsupported registry value kind."),
    };

    private static RegistryValueKindKind MapKind(RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.String => RegistryValueKindKind.String,
        RegistryValueKind.ExpandString => RegistryValueKindKind.ExpandString,
        RegistryValueKind.DWord => RegistryValueKindKind.DWord,
        RegistryValueKind.QWord => RegistryValueKindKind.QWord,
        RegistryValueKind.Binary => RegistryValueKindKind.Binary,
        _ => RegistryValueKindKind.String,
    };

    private static RegistryValueKind MapKind(RegistryValueKindKind kind) => kind switch
    {
        RegistryValueKindKind.String => RegistryValueKind.String,
        RegistryValueKindKind.ExpandString => RegistryValueKind.ExpandString,
        RegistryValueKindKind.DWord => RegistryValueKind.DWord,
        RegistryValueKindKind.QWord => RegistryValueKind.QWord,
        RegistryValueKindKind.Binary => RegistryValueKind.Binary,
        _ => RegistryValueKind.String,
    };
}

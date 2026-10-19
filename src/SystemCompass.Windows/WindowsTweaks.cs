using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.Win32;
using SystemCompass.Core.Tweaks;

namespace SystemCompass.Windows;

public sealed class WindowsTweaks : ApplyTweaks
{
    private static readonly XNamespace TaskNamespace =
        "http://schemas.microsoft.com/windows/2004/02/mit/task";

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

        RunToolChecked("sc.exe", "config", serviceName, "start=", startMode);
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

    public string? ReadTcpGlobal(string name)
    {
        var result = RunTool("netsh.exe", "int", "tcp", "dump");
        if (result.ExitCode != 0)
        {
            return null;
        }

        const string prefix = "set global ";
        foreach (var raw in result.Output.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var pair in line[prefix.Length..].Split(
                ' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=');
                if (separator > 0
                    && pair[..separator].Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair[(separator + 1)..];
                }
            }
        }

        return null;
    }

    public void WriteTcpGlobal(string name, string value)
    {
        RunToolChecked("netsh.exe", "int", "tcp", "set", "global", $"{name}={value}");
    }

    public bool? ReadScheduledTaskEnabled(string taskPath)
    {
        var result = RunTool("schtasks.exe", "/Query", "/TN", taskPath, "/XML");
        if (result.ExitCode != 0)
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(result.Output.TrimStart('\uFEFF'));
            var settings = document.Root?.Element(TaskNamespace + "Settings");
            var enabled = settings?.Element(TaskNamespace + "Enabled")?.Value;
            return !string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    public void SetScheduledTaskEnabled(string taskPath, bool enabled)
    {
        RunToolChecked(
            "schtasks.exe",
            "/Change",
            "/TN",
            taskPath,
            enabled ? "/ENABLE" : "/DISABLE");
    }

    private static (int ExitCode, string Output, string Error) RunTool(
        string fileName,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"{fileName} could not be started.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }

    private static string RunToolChecked(string fileName, params string[] arguments)
    {
        var result = RunTool(fileName, arguments);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{fileName} failed with exit code {result.ExitCode}: {result.Error.Trim()} {result.Output.Trim()}".Trim());
        }

        return result.Output;
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

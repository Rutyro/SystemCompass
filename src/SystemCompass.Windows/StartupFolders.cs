using System.Runtime.InteropServices;
using Microsoft.Win32;
using SystemCompass.Core.Startup;

namespace SystemCompass.Windows;

public sealed class StartupFolders : ManageStartup
{
    private sealed record Folder(string Path, string Hive, StartupScope Scope, string ApprovalPath);

    private readonly Classifier _classifier;
    private readonly SignatureVerifier _signatures;
    private readonly TimeProvider _clock;
    private readonly string _userStartup;
    private readonly string _commonStartup;

    public StartupFolders(Classifier classifier, SignatureVerifier signatures, TimeProvider clock)
    {
        _classifier = classifier;
        _signatures = signatures;
        _clock = clock;
        _userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        _commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
    }

    public StartupMechanism Mechanism => StartupMechanism.StartupFolder;

    public bool CanSetState => true;

    public IReadOnlyList<StartupEntry> Enumerate()
    {
        var folders = new[]
        {
            new Folder(_userStartup, "HKCU", StartupScope.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder"),
            new Folder(_commonStartup, "HKLM", StartupScope.AllUsers,
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder"),
        };

        var entries = new List<StartupEntry>();
        foreach (var folder in folders)
        {
            entries.AddRange(EnumerateFolder(folder));
        }

        return entries;
    }

    public void SetState(string entryId, bool enabled)
    {
        var entry = Enumerate().FirstOrDefault(candidate => candidate.Id == entryId)
            ?? throw new InvalidOperationException("Unknown startup entry.");
        var folder = entry.Scope == StartupScope.CurrentUser
            ? new Folder(_userStartup, "HKCU", StartupScope.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder")
            : new Folder(_commonStartup, "HKLM", StartupScope.AllUsers,
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder");

        var file = Directory.EnumerateFiles(folder.Path)
            .FirstOrDefault(candidate =>
                Path.GetFileNameWithoutExtension(candidate).Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The startup shortcut no longer exists.");

        using var baseKey = WindowsRegistry.OpenBase(folder.Hive);
        using var approvalKey = baseKey.CreateSubKey(folder.ApprovalPath, writable: true)
            ?? throw new InvalidOperationException("The StartupApproved key could not be opened.");
        approvalKey.SetValue(
            Path.GetFileName(file),
            StartupApprovedValue.Create(enabled, _clock.GetUtcNow()),
            RegistryValueKind.Binary);
    }

    private IEnumerable<StartupEntry> EnumerateFolder(Folder folder)
    {
        if (!Directory.Exists(folder.Path))
        {
            yield break;
        }

        using var baseKey = WindowsRegistry.OpenBase(folder.Hive);
        using var approvalKey = baseKey.OpenSubKey(folder.ApprovalPath);

        foreach (var file in Directory.EnumerateFiles(folder.Path).Where(IsStartupItem))
        {
            var fileName = Path.GetFileName(file);
            var name = Path.GetFileNameWithoutExtension(file);
            var target = ResolveTarget(file);
            var state = StartupApprovedValue.Parse(approvalKey?.GetValue(fileName) as byte[] ?? []);
            yield return CreateEntry(folder, name, file, target, state);
        }
    }

    private StartupEntry CreateEntry(Folder folder, string name, string shortcutPath, string target, EntryState state)
    {
        var commandLine = target.Equals(shortcutPath, StringComparison.OrdinalIgnoreCase)
            ? shortcutPath
            : $"\"{target}\"";
        var entry = new StartupEntry(
            StartupEntryId.Create(StartupMechanism.StartupFolder, folder.Scope, folder.Path, name),
            name,
            folder.Path,
            StartupMechanism.StartupFolder,
            folder.Scope,
            state,
            commandLine,
            target,
            _signatures.Verify(target),
            new Assessment(EntryCategory.Unclear, RiskLevel.Medium, string.Empty, string.Empty),
            RequiresElevation: folder.Hive == "HKLM");

        return entry with { Assessment = _classifier.Assess(entry) };
    }

    private static bool IsStartupItem(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".url", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveTarget(string shortcutPath)
    {
        if (!shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return shortcutPath;
        }

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return shortcutPath;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            string target = shortcut.TargetPath;
            return string.IsNullOrWhiteSpace(target) ? shortcutPath : target;
        }
        catch (COMException)
        {
            return shortcutPath;
        }
        catch (MissingMethodException)
        {
            return shortcutPath;
        }
        catch (InvalidCastException)
        {
            return shortcutPath;
        }
    }
}

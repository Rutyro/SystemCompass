using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text.Json;

namespace SystemCompass.App;

/// <summary>
/// Machine-scope changes are handed to the CLI with a one-time UAC prompt. The
/// helper file is treated as untrusted and re-audits every entry itself.
/// </summary>
internal static class ElevationHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static async Task<(bool Success, string Message)> ApplyAsync(
        IReadOnlyList<(string EntryId, bool Enabled)> changes)
    {
        var helper = LocateHelper();
        if (helper is null)
        {
            return (false, "The elevated helper syscompass.exe was not found next to the app.");
        }

        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User?.Value;
        if (string.IsNullOrEmpty(sid))
        {
            return (false, "The current user SID could not be read.");
        }

        var intentPath = Path.Combine(
            Path.GetTempPath(),
            $"systemcompass-intent-{Guid.NewGuid():N}.json");
        var payload = new
        {
            entries = changes.Select(change => new { entryId = change.EntryId, enabled = change.Enabled }),
        };
        using var intentStream = new FileStream(
            intentPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using (var writer = new StreamWriter(intentStream, leaveOpen: true))
        {
            await writer.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
        }

        try
        {
            var startInfo = new ProcessStartInfo(helper)
            {
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            startInfo.ArgumentList.Add("apply-intent");
            startInfo.ArgumentList.Add(intentPath);
            startInfo.ArgumentList.Add(sid);
            startInfo.ArgumentList.Add("--data-dir");
            startInfo.ArgumentList.Add(App.Services.DataDirectory);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return (false, "The elevated helper could not be started.");
            }

            await process.WaitForExitAsync();
            var resultPath = intentPath + ".result.json";
            if (!File.Exists(resultPath))
            {
                return (false, $"The elevated helper exited with code {process.ExitCode} without a result.");
            }

            var result = JsonSerializer.Deserialize<IntentResultFile>(
                await File.ReadAllTextAsync(resultPath), JsonOptions);
            var failures = result?.Results.Where(entry => !entry.Success).ToArray() ?? [];
            return failures.Length == 0
                ? (true, "Applied with administrator rights.")
                : (false, string.Join("; ", failures.Select(failure => failure.Message)));
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (false, "Elevation was cancelled.");
        }
        finally
        {
            // The write handle stays open across the UAC prompt so another
            // process cannot replace the intent while the helper is starting.
            intentStream.Dispose();
            TryDelete(intentPath);
            TryDelete(intentPath + ".result.json");
        }
    }

    public static async Task<(bool Success, string Message)> RunTweakAsync(string tweakId, bool revert)
    {
        var helper = LocateHelper();
        if (helper is null)
        {
            return (false, "The elevated helper syscompass.exe was not found next to the app.");
        }

        try
        {
            var startInfo = new ProcessStartInfo(helper)
            {
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            startInfo.ArgumentList.Add("tweak");
            startInfo.ArgumentList.Add(revert ? "revert" : "apply");
            startInfo.ArgumentList.Add(tweakId);
            startInfo.ArgumentList.Add("--data-dir");
            startInfo.ArgumentList.Add(App.Services.DataDirectory);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return (false, "The elevated helper could not be started.");
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0
                ? (true, revert ? "Reverted with administrator rights." : "Applied with administrator rights.")
                : (false, $"The elevated helper exited with code {process.ExitCode}.");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (false, "Elevation was cancelled.");
        }
    }

    private static string? LocateHelper()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "syscompass.exe");
        if (File.Exists(local))
        {
            return local;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 8 && directory is not null; depth++, directory = directory.Parent)
        {
            foreach (var configuration in new[] { "Release", "Debug" })
            {
                var candidate = Path.Combine(
                    directory.FullName, "src", "SystemCompass.Cli", "bin", configuration,
                    "net10.0-windows10.0.19041.0", "syscompass.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // The elevated process may still hold the file briefly; the temp
            // directory is cleaned up by the operating system.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record IntentResultFile(IReadOnlyList<IntentResult> Results);

    private sealed record IntentResult(string EntryId, bool Success, string Message);
}

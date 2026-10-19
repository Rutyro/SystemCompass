namespace SystemCompass.Core.Startup;

/// <summary>
/// Rejects paths that could reach the network before any filesystem probe.
/// A UNC path or a mapped network drive makes File.Exists, WinVerifyTrust and
/// SHGetFileInfo perform SMB authentication, which would leak NTLM material
/// from the app or its elevated helper.
/// </summary>
public static class LocalPath
{
    public static bool IsLocalFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var candidate = path;
        if (candidate.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            candidate = candidate[4..];
            if (candidate.StartsWith(@"UNC\", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        else if (candidate.StartsWith(@"\\", StringComparison.Ordinal) || candidate.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return false;
        }

        if (!Path.IsPathFullyQualified(candidate))
        {
            return false;
        }

        var root = Path.GetPathRoot(candidate);
        if (root is not { Length: >= 2 } || !char.IsAsciiLetter(root[0]) || root[1] != ':')
        {
            return false;
        }

        try
        {
            if (new DriveInfo(root).DriveType is DriveType.Network
                or DriveType.NoRootDirectory
                or DriveType.Unknown)
            {
                return false;
            }

            // A local symlink may still point at a UNC share. Resolve the link
            // target text before any caller opens or verifies the file.
            var target = new FileInfo(candidate).LinkTarget;
            if (target is not null)
            {
                return IsLocalFile(target);
            }
        }
        catch (Exception ex) when (ex is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return false;
        }

        return true;
    }
}

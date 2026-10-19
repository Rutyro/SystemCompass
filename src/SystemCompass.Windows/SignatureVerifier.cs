using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using SystemCompass.Core.Startup;

namespace SystemCompass.Windows;

/// <summary>
/// Verifies Authenticode signatures with WinVerifyTrust. Revocation checks are
/// disabled and cache-only retrieval is enforced so verification never performs
/// a network request.
/// </summary>
public sealed class SignatureVerifier
{
    private readonly Dictionary<string, SignatureInfo> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public SignatureInfo Verify(string? path)
    {
        if (!LocalPath.IsLocalFile(path) || !File.Exists(path))
        {
            return SignatureInfo.Unknown;
        }

        lock (_gate)
        {
            if (_cache.TryGetValue(path, out var cached))
            {
                return cached;
            }
        }

        var info = AuthenticodeInterop.Verify(path);
        lock (_gate)
        {
            _cache[path] = info;
            return info;
        }
    }
}

internal static class AuthenticodeInterop
{
    private const uint WtdUiNone = 2;
    private const uint WtdRevokeNone = 0;
    private const uint WtdChoiceFile = 1;
    private const uint WtdChoiceCatalog = 2;
    private const uint WtdStateActionVerify = 1;
    private const uint WtdStateActionClose = 2;
    private const uint WtdCacheOnlyUrlRetrieval = 0x00001000;
    private const uint WtdRevocationCheckNone = 0x00000010;
    private const uint WtdSaferFlag = 0x00000100;

    private const int TrustENoSignature = unchecked((int)0x800B0100);
    private const int TrustESubjectFormUnknown = unchecked((int)0x800B0003);
    private const int TrustEProviderUnknown = unchecked((int)0x800B0001);

    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
    private static readonly Guid DriverActionVerify = new("F750E6C3-38EE-11d1-85E5-00C04FC295EE");

    public static SignatureInfo Verify(string path)
    {
        var fileResult = VerifyFile(path, out var fileSigner);
        if (fileResult == 0)
        {
            return new SignatureInfo(SignatureStatus.Valid, NullIfEmpty(fileSigner));
        }

        if (fileResult is TrustENoSignature or TrustESubjectFormUnknown or TrustEProviderUnknown)
        {
            if (VerifyCatalog(path, out var catalogSigner))
            {
                return new SignatureInfo(SignatureStatus.ValidCatalog, NullIfEmpty(catalogSigner));
            }

            return new SignatureInfo(SignatureStatus.Unsigned);
        }

        return new SignatureInfo(SignatureStatus.Invalid);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static int VerifyFile(string path, out string? signer)
    {
        signer = null;
        var pathPointer = Marshal.StringToHGlobalUni(path);
        var fileInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        var dataPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustData>());

        try
        {
            var fileInfo = new WinTrustFileInfo
            {
                Size = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
                FilePath = pathPointer,
            };
            Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);

            var data = CreateData(WtdChoiceFile, fileInfoPointer);
            Marshal.StructureToPtr(data, dataPointer, false);

            var action = GenericVerifyV2;
            var result = WinVerifyTrust(IntPtr.Zero, ref action, dataPointer);
            if (result == 0)
            {
                signer = ReadSignerName(dataPointer);
            }

            CloseState(dataPointer, action);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(dataPointer);
            Marshal.FreeHGlobal(fileInfoPointer);
            Marshal.FreeHGlobal(pathPointer);
        }
    }

    private static bool VerifyCatalog(string path, out string? signer)
    {
        signer = null;
        using var stream = File.OpenRead(path);
        var subsystem = DriverActionVerify;
        if (!CryptCATAdminAcquireContext2(out var admin, ref subsystem, "SHA256", IntPtr.Zero, 0))
        {
            return false;
        }

        IntPtr catalog = IntPtr.Zero;
        try
        {
            uint hashSize = 0;
            if (!CryptCATAdminCalcHashFromFileHandle2(admin, stream.SafeFileHandle, ref hashSize, null, 0))
            {
                return false;
            }

            var hash = new byte[hashSize];
            if (!CryptCATAdminCalcHashFromFileHandle2(admin, stream.SafeFileHandle, ref hashSize, hash, 0))
            {
                return false;
            }

            catalog = CryptCATAdminEnumCatalogFromHash(admin, hash, hashSize, 0, ref catalog);
            if (catalog == IntPtr.Zero)
            {
                return false;
            }

            var catalogInfo = new CatalogInfo
            {
                Size = (uint)Marshal.SizeOf<CatalogInfo>(),
                CatalogFile = string.Empty,
            };
            if (!CryptCATCatalogInfoFromContext(catalog, ref catalogInfo, 0))
            {
                return false;
            }

            return VerifyCatalogMember(path, stream.SafeFileHandle, catalogInfo.CatalogFile, hash, admin, out signer);
        }
        finally
        {
            if (catalog != IntPtr.Zero)
            {
                CryptCATAdminReleaseCatalogContext(admin, catalog, 0);
            }

            CryptCATAdminReleaseContext(admin, 0);
        }
    }

    private static bool VerifyCatalogMember(
        string path,
        SafeFileHandle memberFile,
        string catalogPath,
        byte[] hash,
        IntPtr admin,
        out string? signer)
    {
        signer = null;
        var catalogPathPointer = Marshal.StringToHGlobalUni(catalogPath);
        var memberPathPointer = Marshal.StringToHGlobalUni(path);
        var memberTagPointer = Marshal.StringToHGlobalUni(Convert.ToHexString(hash));
        var hashPointer = Marshal.AllocHGlobal(hash.Length);
        var catalogInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustCatalogInfo>());
        var dataPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustData>());

        try
        {
            Marshal.Copy(hash, 0, hashPointer, hash.Length);
            var catalogInfo = new WinTrustCatalogInfo
            {
                Size = (uint)Marshal.SizeOf<WinTrustCatalogInfo>(),
                CatalogFilePath = catalogPathPointer,
                MemberTag = memberTagPointer,
                MemberFilePath = memberPathPointer,
                MemberFile = memberFile.DangerousGetHandle(),
                CalculatedHash = hashPointer,
                CalculatedHashSize = (uint)hash.Length,
                CatalogContext = IntPtr.Zero,
                CatalogAdmin = admin,
            };
            Marshal.StructureToPtr(catalogInfo, catalogInfoPointer, false);

            var data = CreateData(WtdChoiceCatalog, catalogInfoPointer);
            Marshal.StructureToPtr(data, dataPointer, false);

            var action = GenericVerifyV2;
            var result = WinVerifyTrust(IntPtr.Zero, ref action, dataPointer);
            if (result == 0)
            {
                signer = ReadSignerName(dataPointer);
            }

            CloseState(dataPointer, action);
            return result == 0;
        }
        finally
        {
            Marshal.FreeHGlobal(dataPointer);
            Marshal.FreeHGlobal(catalogInfoPointer);
            Marshal.FreeHGlobal(hashPointer);
            Marshal.FreeHGlobal(memberTagPointer);
            Marshal.FreeHGlobal(memberPathPointer);
            Marshal.FreeHGlobal(catalogPathPointer);
        }
    }

    private static WinTrustData CreateData(uint choice, IntPtr unionPointer)
    {
        return new WinTrustData
        {
            Size = (uint)Marshal.SizeOf<WinTrustData>(),
            UiChoice = WtdUiNone,
            RevocationChecks = WtdRevokeNone,
            UnionChoice = choice,
            UnionData = unionPointer,
            StateAction = WtdStateActionVerify,
            ProviderFlags = WtdCacheOnlyUrlRetrieval | WtdRevocationCheckNone | WtdSaferFlag,
        };
    }

    private static void CloseState(IntPtr dataPointer, Guid action)
    {
        var data = Marshal.PtrToStructure<WinTrustData>(dataPointer);
        data.StateAction = WtdStateActionClose;
        Marshal.StructureToPtr(data, dataPointer, false);
        WinVerifyTrust(IntPtr.Zero, ref action, dataPointer);
    }

    private static string? ReadSignerName(IntPtr dataPointer)
    {
        var data = Marshal.PtrToStructure<WinTrustData>(dataPointer);
        if (data.StateData == IntPtr.Zero)
        {
            return null;
        }

        var provider = WTHelperProvDataFromStateData(data.StateData);
        if (provider == IntPtr.Zero)
        {
            return null;
        }

        var providerSigner = WTHelperGetProvSignerFromChain(provider, 0, false, 0);
        if (providerSigner == IntPtr.Zero)
        {
            return null;
        }

        var providerCertificate = WTHelperGetProvCertFromChain(providerSigner, 0);
        if (providerCertificate == IntPtr.Zero)
        {
            return null;
        }

        // CRYPT_PROVIDER_CERT starts with cbStruct and then holds pCert.
        var certificate = Marshal.ReadIntPtr(providerCertificate, IntPtr.Size);
        if (certificate == IntPtr.Zero)
        {
            return null;
        }

        var length = CertGetNameString(certificate, 4, 0, IntPtr.Zero, null, 0);
        if (length <= 1)
        {
            return null;
        }

        var builder = new StringBuilder((int)length);
        return CertGetNameString(certificate, 4, 0, IntPtr.Zero, builder, length) <= 1
            ? null
            : builder.ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustFileInfo
    {
        public uint Size;
        public IntPtr FilePath;
        public IntPtr File;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustCatalogInfo
    {
        public uint Size;
        public uint CatalogVersion;
        public IntPtr CatalogFilePath;
        public IntPtr MemberTag;
        public IntPtr MemberFilePath;
        public IntPtr MemberFile;
        public IntPtr CalculatedHash;
        public uint CalculatedHashSize;
        public IntPtr CatalogContext;
        public IntPtr CatalogAdmin;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint Size;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr UnionData;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CatalogInfo
    {
        public uint Size;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string CatalogFile;
    }

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, IntPtr data);

    [DllImport("wintrust.dll")]
    private static extern IntPtr WTHelperProvDataFromStateData(IntPtr stateData);

    [DllImport("wintrust.dll")]
    private static extern IntPtr WTHelperGetProvSignerFromChain(
        IntPtr providerData, uint signerIndex, [MarshalAs(UnmanagedType.Bool)] bool counterSigner, uint counterSignerIndex);

    [DllImport("wintrust.dll")]
    private static extern IntPtr WTHelperGetProvCertFromChain(IntPtr signer, uint certificateIndex);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CertGetNameString(
        IntPtr certificate, uint type, uint flags, IntPtr typeParameter, StringBuilder? name, uint nameLength);

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminAcquireContext2(
        out IntPtr admin, ref Guid subsystem, string? hashAlgorithm, IntPtr strongHashPolicy, uint flags);

    [DllImport("wintrust.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminCalcHashFromFileHandle2(
        IntPtr admin, SafeFileHandle file, ref uint hashSize, [Out] byte[]? hash, uint flags);

    [DllImport("wintrust.dll", SetLastError = true)]
    private static extern IntPtr CryptCATAdminEnumCatalogFromHash(
        IntPtr admin, byte[] hash, uint hashSize, uint flags, ref IntPtr previousCatalog);

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATCatalogInfoFromContext(
        IntPtr catalog, ref CatalogInfo info, uint flags);

    [DllImport("wintrust.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminReleaseCatalogContext(IntPtr admin, IntPtr catalog, uint flags);

    [DllImport("wintrust.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminReleaseContext(IntPtr admin, uint flags);
}

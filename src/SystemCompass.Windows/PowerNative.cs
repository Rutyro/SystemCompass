using System.Runtime.InteropServices;

namespace SystemCompass.Windows;

internal static class PowerNative
{
    public static Guid? GetActiveScheme()
    {
        var result = PowerGetActiveScheme(IntPtr.Zero, out var pointer);
        if (result != 0 || pointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStructure<Guid>(pointer);
        }
        finally
        {
            LocalFree(pointer);
        }
    }

    public static uint? Read(Guid scheme, Guid subGroup, Guid setting, bool ac)
    {
        uint result;
        uint value;
        if (ac)
        {
            result = PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref subGroup, ref setting, out value);
        }
        else
        {
            result = PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref subGroup, ref setting, out value);
        }

        return result == 0 ? value : null;
    }

    public static void Write(Guid scheme, Guid subGroup, Guid setting, bool ac, uint value)
    {
        var result = ac
            ? PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref subGroup, ref setting, value)
            : PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref subGroup, ref setting, value);
        if (result != 0)
        {
            throw new InvalidOperationException($"PowerWriteValueIndex failed with code {result}.");
        }

        var activate = PowerSetActiveScheme(IntPtr.Zero, ref scheme);
        if (activate != 0)
        {
            throw new InvalidOperationException($"PowerSetActiveScheme failed with code {activate}.");
        }
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACValueIndex(
        IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupGuid, ref Guid powerSettingGuid, out uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadDCValueIndex(
        IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupGuid, ref Guid powerSettingGuid, out uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteACValueIndex(
        IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupGuid, ref Guid powerSettingGuid, uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteDCValueIndex(
        IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupGuid, ref Guid powerSettingGuid, uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}

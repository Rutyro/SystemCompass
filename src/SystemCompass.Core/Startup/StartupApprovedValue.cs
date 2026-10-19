using System.Buffers.Binary;

namespace SystemCompass.Core.Startup;

/// <summary>
/// Parses and creates the binary values stored under
/// Explorer\StartupApproved. Windows itself uses byte 0 as the enabled flag;
/// the remaining bytes are a FILETIME that Task Manager fills in.
/// </summary>
public static class StartupApprovedValue
{
    public const byte EnabledFlag = 0x02;
    public const byte DisabledFlag = 0x03;

    public static EntryState Parse(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return EntryState.Enabled;
        }

        return value[0] switch
        {
            EnabledFlag or 0x06 => EntryState.Enabled,
            DisabledFlag or 0x07 => EntryState.Disabled,
            _ => EntryState.Unknown,
        };
    }

    public static byte[] Create(bool enabled, DateTimeOffset timestamp)
    {
        var bytes = new byte[12];
        bytes[0] = enabled ? EnabledFlag : DisabledFlag;
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(4), timestamp.UtcDateTime.ToFileTimeUtc());
        return bytes;
    }
}

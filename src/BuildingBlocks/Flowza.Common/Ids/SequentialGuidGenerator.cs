using System.Security.Cryptography;

namespace Flowza.Common.Ids;

/// <summary>
/// Creates time-ordered GUIDs in code that are ordered the way <b>SQL Server</b> sorts <c>uniqueidentifier</c>
/// values, so new rows are appended to the end of an index instead of scattered across it
/// (tech discussion T3, D3).
/// </summary>
/// <remarks>
/// SQL Server compares the 16 bytes in this order: 10–15, then 8–9, 6–7, 4–5, 0–3. So the generator writes the
/// Unix time in milliseconds (48 bits) into bytes 10–15, a counter for IDs made in the same millisecond into
/// bytes 8–9, and random bytes into 0–7. These are not RFC 9562 version 7 GUIDs: plain version 7 GUIDs are
/// ordered by their first bytes, which SQL Server compares last.
/// </remarks>
public sealed class SequentialGuidGenerator
{
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();
    private long _lastMilliseconds = long.MinValue;
    private ushort _counter;

    public SequentialGuidGenerator(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>A shared generator on the system clock.</summary>
    public static SequentialGuidGenerator Shared { get; } = new(TimeProvider.System);

    public Guid NewGuid()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes[..8]);

        long milliseconds;
        ushort counter;
        lock (_lock)
        {
            milliseconds = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
            if (milliseconds <= _lastMilliseconds)
            {
                // Same millisecond, or the clock moved backwards: stay on the last time and count up.
                milliseconds = _lastMilliseconds;
                if (_counter == ushort.MaxValue)
                {
                    milliseconds++;
                    _counter = 0;
                }
                else
                {
                    _counter++;
                }
            }
            else
            {
                _counter = 0;
            }

            _lastMilliseconds = milliseconds;
            counter = _counter;
        }

        bytes[8] = (byte)(counter >> 8);
        bytes[9] = (byte)counter;
        bytes[10] = (byte)(milliseconds >> 40);
        bytes[11] = (byte)(milliseconds >> 32);
        bytes[12] = (byte)(milliseconds >> 24);
        bytes[13] = (byte)(milliseconds >> 16);
        bytes[14] = (byte)(milliseconds >> 8);
        bytes[15] = (byte)milliseconds;

        return new Guid(bytes);
    }
}

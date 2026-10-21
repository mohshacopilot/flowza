using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Flowza.Persistence.Conventions;

/// <summary>
/// Stores <see cref="DateTime"/> as UTC and reads it back marked as UTC (constitution 8.4). A local time is
/// converted to UTC before saving; an unspecified time is treated as UTC.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

/// <summary>
/// Stores <see cref="DateTimeOffset"/> as a UTC <c>datetime2</c> (tech discussion T3, D2); reads it back with offset 0.
/// The instant is kept; the original offset is not (times are shown in the user's own time zone, NFR-05).
/// </summary>
public sealed class UtcDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTime>(
    value => value.UtcDateTime,
    value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

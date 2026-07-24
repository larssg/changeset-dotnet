using System.Globalization;
using CsCheck;

namespace Changeset.Tests;

/// <summary>
/// Property-based tests (CsCheck) for the type-coercion boundaries of the casting
/// layer. The coercion API itself is internal, so all properties are exercised
/// through the public <see cref="Changeset{T}.Cast(IReadOnlyDictionary{string, object?}, IReadOnlyList{string}, CastOptions?)"/>
/// entry point using the <see cref="CoercionTarget"/> model.
/// </summary>
public class TypeCoercionPropertyTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    private static readonly Gen<UserRole> GenRole = Gen.Int[0, 2].Select(i => (UserRole)i);

    /// <summary>Arbitrary unicode strings, including control chars and lone surrogates.</summary>
    private static readonly Gen<string> GenUnicodeString =
        Gen.UShort.Select(u => (char)u).Array[0, 24].Select(chars => new string(chars));

    /// <summary>Every coercible field on <see cref="CoercionTarget"/> with its underlying target type.</summary>
    private static readonly (string Field, Type Target)[] CoercibleFields =
    [
        ("IntValue", typeof(int)),
        ("LongValue", typeof(long)),
        ("DoubleValue", typeof(double)),
        ("FloatValue", typeof(float)),
        ("DecimalValue", typeof(decimal)),
        ("BoolValue", typeof(bool)),
        ("DateTimeValue", typeof(DateTime)),
        ("DateTimeOffsetValue", typeof(DateTimeOffset)),
        ("DateOnlyValue", typeof(DateOnly)),
        ("TimeOnlyValue", typeof(TimeOnly)),
        ("GuidValue", typeof(Guid)),
        ("EnumValue", typeof(UserRole)),
        ("NullableIntValue", typeof(int)),
        ("NullableDecimalValue", typeof(decimal)),
        ("NullableDateTimeValue", typeof(DateTime)),
        ("NullableGuidValue", typeof(Guid)),
        ("NullableEnumValue", typeof(UserRole)),
        ("StringValue", typeof(string)),
    ];

    private static Changeset<CoercionTarget> CastSingle(
        string field, object? value, CastOptions? options = null)
    {
        var @params = new Dictionary<string, object?> { [field] = value };
        return Changeset<CoercionTarget>.Cast(@params, [field], options);
    }

    // ---------------------------------------------------------------------
    // Round-trip: format with the cast's FormatProvider, coerce back, equal.
    // ---------------------------------------------------------------------

    [Fact]
    public void Cast_IntString_RoundTrips() =>
        Gen.Int.Sample(i =>
        {
            var cs = CastSingle("IntValue", i.ToString(Invariant));
            Assert.True(cs.IsValid);
            Assert.Equal(i, cs.GetChange<int>("IntValue"));
        });

    [Fact]
    public void Cast_LongString_RoundTrips() =>
        Gen.Long.Sample(l =>
        {
            var cs = CastSingle("LongValue", l.ToString(Invariant));
            Assert.True(cs.IsValid);
            Assert.Equal(l, cs.GetChange<long>("LongValue"));
        });

    [Fact]
    public void Cast_DoubleString_RoundTripFormat_RoundTrips() =>
        Gen.Double.Where(double.IsFinite).Sample(d =>
        {
            var cs = CastSingle("DoubleValue", d.ToString("R", Invariant));
            Assert.True(cs.IsValid);
            Assert.Equal(d, cs.GetChange<double>("DoubleValue"));
        });

    [Fact]
    public void Cast_FloatString_RoundTripFormat_RoundTrips() =>
        Gen.Float.Where(float.IsFinite).Sample(f =>
        {
            var cs = CastSingle("FloatValue", f.ToString("R", Invariant));
            Assert.True(cs.IsValid);
            Assert.Equal(f, cs.GetChange<float>("FloatValue"));
        });

    [Fact]
    public void Cast_DecimalString_RoundTrips() =>
        Gen.Decimal.Sample(d =>
        {
            var cs = CastSingle("DecimalValue", d.ToString(Invariant));
            Assert.True(cs.IsValid);
            Assert.Equal(d, cs.GetChange<decimal>("DecimalValue"));
        });

    [Fact]
    public void Cast_BoolString_RoundTrips() =>
        Gen.Bool.Sample(b =>
        {
            var fromName = CastSingle("BoolValue", b.ToString());
            Assert.True(fromName.IsValid);
            Assert.Equal(b, fromName.GetChange<bool>("BoolValue"));

            var fromDigit = CastSingle("BoolValue", b ? "1" : "0");
            Assert.True(fromDigit.IsValid);
            Assert.Equal(b, fromDigit.GetChange<bool>("BoolValue"));
        });

    [Fact]
    public void Cast_DateTimeString_Iso8601_RoundTrips() =>
        Gen.DateTime.Sample(dt =>
        {
            var cs = CastSingle("DateTimeValue", dt.ToString("O", Invariant));
            Assert.True(cs.IsValid);
            Assert.Equal(dt, cs.GetChange<DateTime>("DateTimeValue"));
        });

    [Fact]
    public void Cast_DateTimeOffsetString_Iso8601_RoundTrips() =>
        Gen.DateTimeOffset.Sample(dto =>
        {
            var cs = CastSingle("DateTimeOffsetValue", dto.ToString("O", Invariant));
            Assert.True(cs.IsValid);
            Assert.Equal(dto, cs.GetChange<DateTimeOffset>("DateTimeOffsetValue"));
        });

    [Fact]
    public void Cast_DateOnlyString_Iso8601_RoundTrips() =>
        Gen.DateOnly.Sample(date =>
        {
            var cs = CastSingle("DateOnlyValue", date.ToString("O", Invariant));
            Assert.True(cs.IsValid);
            Assert.Equal(date, cs.GetChange<DateOnly>("DateOnlyValue"));
        });

    [Fact]
    public void Cast_TimeOnlyString_RoundTripFormat_RoundTrips() =>
        Gen.TimeOnly.Sample(time =>
        {
            var cs = CastSingle("TimeOnlyValue", time.ToString("O", Invariant));
            Assert.True(cs.IsValid);
            Assert.Equal(time, cs.GetChange<TimeOnly>("TimeOnlyValue"));
        });

    [Fact]
    public void Cast_GuidString_AllStandardFormats_RoundTrip() =>
        Gen.Select(Gen.Guid, Gen.OneOfConst("N", "D", "B", "P")).Sample((guid, format) =>
        {
            var cs = CastSingle("GuidValue", guid.ToString(format));
            Assert.True(cs.IsValid);
            Assert.Equal(guid, cs.GetChange<Guid>("GuidValue"));
        });

    [Fact]
    public void Cast_EnumString_NameInAnyCase_RoundTrips() =>
        Gen.Select(GenRole, Gen.Bool).Sample((role, upper) =>
        {
            var name = upper ? role.ToString().ToUpperInvariant() : role.ToString().ToLowerInvariant();
            var cs = CastSingle("EnumValue", name);
            Assert.True(cs.IsValid);
            Assert.Equal(role, cs.GetChange<UserRole>("EnumValue"));
        });

    [Fact]
    public void Cast_NullableTargets_StringValues_RoundTrip() =>
        Gen.Select(Gen.Int, Gen.Decimal, Gen.Guid, GenRole).Sample((i, d, g, role) =>
        {
            var @params = new Dictionary<string, object?>
            {
                ["NullableIntValue"] = i.ToString(Invariant),
                ["NullableDecimalValue"] = d.ToString(Invariant),
                ["NullableGuidValue"] = g.ToString(),
                ["NullableEnumValue"] = role.ToString()
            };

            var cs = Changeset<CoercionTarget>.Cast(@params,
                ["NullableIntValue", "NullableDecimalValue", "NullableGuidValue", "NullableEnumValue"]);

            Assert.True(cs.IsValid);
            Assert.Equal(i, cs.GetChange<int?>("NullableIntValue"));
            Assert.Equal(d, cs.GetChange<decimal?>("NullableDecimalValue"));
            Assert.Equal(g, cs.GetChange<Guid?>("NullableGuidValue"));
            Assert.Equal(role, cs.GetChange<UserRole?>("NullableEnumValue"));
        });

    // ---------------------------------------------------------------------
    // Already-typed values pass through unchanged.
    // ---------------------------------------------------------------------

    [Fact]
    public void Cast_AlreadyTypedPrimitives_PassThroughUnchanged() =>
        Gen.Select(Gen.Int, Gen.Long, Gen.Double, Gen.Float, Gen.Decimal, Gen.Bool)
            .Sample((i, l, d, f, m, b) =>
            {
                var @params = new Dictionary<string, object?>
                {
                    ["IntValue"] = i,
                    ["LongValue"] = l,
                    ["DoubleValue"] = d,
                    ["FloatValue"] = f,
                    ["DecimalValue"] = m,
                    ["BoolValue"] = b
                };

                var cs = Changeset<CoercionTarget>.Cast(@params,
                    ["IntValue", "LongValue", "DoubleValue", "FloatValue", "DecimalValue", "BoolValue"]);

                Assert.True(cs.IsValid);
                Assert.Equal(i, cs.GetChange<int>("IntValue"));
                Assert.Equal(l, cs.GetChange<long>("LongValue"));
                Assert.Equal(d, cs.GetChange<double>("DoubleValue"));
                Assert.Equal(f, cs.GetChange<float>("FloatValue"));
                Assert.Equal(m, cs.GetChange<decimal>("DecimalValue"));
                Assert.Equal(b, cs.GetChange<bool>("BoolValue"));
            });

    [Fact]
    public void Cast_AlreadyTypedTemporalAndIdValues_PassThroughUnchanged() =>
        Gen.Select(Gen.DateTime, Gen.DateTimeOffset, Gen.DateOnly, Gen.TimeOnly, Gen.Guid, GenRole)
            .Sample((dt, dto, date, time, guid, role) =>
            {
                var @params = new Dictionary<string, object?>
                {
                    ["DateTimeValue"] = dt,
                    ["DateTimeOffsetValue"] = dto,
                    ["DateOnlyValue"] = date,
                    ["TimeOnlyValue"] = time,
                    ["GuidValue"] = guid,
                    ["EnumValue"] = role
                };

                var cs = Changeset<CoercionTarget>.Cast(@params,
                    ["DateTimeValue", "DateTimeOffsetValue", "DateOnlyValue", "TimeOnlyValue", "GuidValue", "EnumValue"]);

                Assert.True(cs.IsValid);
                Assert.Equal(dt, cs.GetChange<DateTime>("DateTimeValue"));
                Assert.Equal(dto, cs.GetChange<DateTimeOffset>("DateTimeOffsetValue"));
                Assert.Equal(date, cs.GetChange<DateOnly>("DateOnlyValue"));
                Assert.Equal(time, cs.GetChange<TimeOnly>("TimeOnlyValue"));
                Assert.Equal(guid, cs.GetChange<Guid>("GuidValue"));
                Assert.Equal(role, cs.GetChange<UserRole>("EnumValue"));
            });

    // ---------------------------------------------------------------------
    // Total function: arbitrary strings never throw — value or cast error.
    // ---------------------------------------------------------------------

    [Fact]
    public void Cast_ArbitraryUnicodeString_NeverThrows_YieldsValueOrCastError() =>
        GenUnicodeString.Sample(input =>
        {
            foreach (var (field, target) in CoercibleFields)
            {
                var cs = CastSingle(field, input);

                if (cs.IsValid)
                {
                    Assert.True(cs.Changes.TryGetValue(field, out var value));
                    Assert.IsAssignableFrom(target, value);
                }
                else
                {
                    Assert.Equal("invalid_cast", cs.ErrorsOn(field)[0].Code);
                    Assert.False(cs.Changes.ContainsKey(field));
                }
            }
        });

    [Fact]
    public void Cast_ArbitraryUnicodeString_NeverThrows_WithStrictAndNoTrim() =>
        GenUnicodeString.Sample(input =>
        {
            var options = new CastOptions { StrictCasting = true, TrimStrings = false };
            foreach (var (field, target) in CoercibleFields)
            {
                var cs = CastSingle(field, input, options);

                if (cs.IsValid)
                {
                    Assert.True(cs.Changes.TryGetValue(field, out var value));
                    Assert.IsAssignableFrom(target, value);
                }
                else
                {
                    Assert.Equal("invalid_cast", cs.ErrorsOn(field)[0].Code);
                }
            }
        });

    // ---------------------------------------------------------------------
    // Overflow boundaries: out-of-range numeric strings are cast errors.
    // ---------------------------------------------------------------------

    [Fact]
    public void Cast_IntString_OutsideIntRange_ProducesCastError() =>
        Gen.OneOf(
            Gen.Long[(long)int.MaxValue + 1, long.MaxValue],
            Gen.Long[long.MinValue, (long)int.MinValue - 1])
        .Sample(l =>
        {
            var cs = CastSingle("IntValue", l.ToString(Invariant));
            Assert.False(cs.IsValid);
            Assert.Equal("invalid_cast", cs.ErrorsOn("IntValue")[0].Code);
        });

    [Fact]
    public void Cast_LongString_OutsideLongRange_ProducesCastError() =>
        Gen.ULong[(ulong)long.MaxValue + 2, ulong.MaxValue].Sample(magnitude =>
        {
            var positive = CastSingle("LongValue", magnitude.ToString(Invariant));
            Assert.False(positive.IsValid);
            Assert.Equal("invalid_cast", positive.ErrorsOn("LongValue")[0].Code);

            // magnitude > |long.MinValue|, so the negative side overflows too.
            var negative = CastSingle("LongValue", "-" + magnitude.ToString(Invariant));
            Assert.False(negative.IsValid);
            Assert.Equal("invalid_cast", negative.ErrorsOn("LongValue")[0].Code);
        });

    [Fact]
    public void Cast_DecimalString_OutsideDecimalRange_ProducesCastError() =>
        Gen.Select(Gen.Double[8e28, 1e37], Gen.Bool).Sample((magnitude, negative) =>
        {
            var text = (negative ? "-" : "") + magnitude.ToString("F0", Invariant);
            var cs = CastSingle("DecimalValue", text);
            Assert.False(cs.IsValid);
            Assert.Equal("invalid_cast", cs.ErrorsOn("DecimalValue")[0].Code);
        });

    [Fact]
    public void Cast_TypedLong_OutsideIntRange_ProducesCastError() =>
        Gen.OneOf(
            Gen.Long[(long)int.MaxValue + 1, long.MaxValue],
            Gen.Long[long.MinValue, (long)int.MinValue - 1])
        .Sample(l =>
        {
            // Numeric-to-numeric coercion of an out-of-range value must error,
            // not wrap around silently.
            var cs = CastSingle("IntValue", l);
            Assert.False(cs.IsValid);
            Assert.Equal("invalid_cast", cs.ErrorsOn("IntValue")[0].Code);
        });

    [Fact]
    public void Cast_DoubleString_BeyondDoubleRange_CoercesToInfinity_CurrentBehavior() =>
        Gen.Select(Gen.Int[309, 999], Gen.Bool).Sample((exponent, negative) =>
        {
            // Since .NET Core 3.0, double.TryParse maps overflowing values to
            // +/-Infinity instead of failing, so the coercion layer silently
            // accepts them — this documents the actual behavior.
            var text = (negative ? "-1e" : "1e") + exponent.ToString(Invariant);
            var cs = CastSingle("DoubleValue", text);
            Assert.True(cs.IsValid);
            var value = cs.GetChange<double>("DoubleValue");
            Assert.True(double.IsInfinity(value));
            Assert.Equal(negative, double.IsNegativeInfinity(value));
        });

    // ---------------------------------------------------------------------
    // CastOptions interactions.
    // ---------------------------------------------------------------------

    [Fact]
    public void Cast_TrimStrings_ControlsWhitespaceHandling_ForStringTarget() =>
        Gen.Select(Gen.Char['a', 'z'].Array[1, 8].Select(c => new string(c)),
                Gen.OneOfConst(" ", "  ", "\t", "\n", " \t "))
            .Sample((core, pad) =>
            {
                var padded = pad + core + pad;

                var trimmed = CastSingle("StringValue", padded);
                Assert.Equal(core, trimmed.GetChange<string>("StringValue"));

                var untrimmed = CastSingle("StringValue", padded,
                    new CastOptions { TrimStrings = false });
                Assert.Equal(padded, untrimmed.GetChange<string>("StringValue"));
            });

    [Fact]
    public void Cast_TrimStrings_ControlsWhitespaceTolerance_ForBoolTarget() =>
        Gen.Select(Gen.Bool, Gen.OneOfConst(" ", "  ", "\t", "\n", " \t "))
            .Sample((b, pad) =>
            {
                var padded = pad + b + pad;

                var trimmed = CastSingle("BoolValue", padded);
                Assert.True(trimmed.IsValid);
                Assert.Equal(b, trimmed.GetChange<bool>("BoolValue"));

                var untrimmed = CastSingle("BoolValue", padded,
                    new CastOptions { TrimStrings = false });
                Assert.False(untrimmed.IsValid);
                Assert.Equal("invalid_cast", untrimmed.ErrorsOn("BoolValue")[0].Code);
            });

    [Fact]
    public void Cast_DecimalString_DanishCulture_RoundTrips() =>
        Gen.Decimal.Sample(d =>
        {
            var options = new CastOptions { FormatProvider = Danish };
            var cs = CastSingle("DecimalValue", d.ToString(Danish), options);
            Assert.True(cs.IsValid);
            Assert.Equal(d, cs.GetChange<decimal>("DecimalValue"));
        });

    [Fact]
    public void Cast_DoubleString_DanishCulture_RoundTripFormat_RoundTrips() =>
        Gen.Double.Where(double.IsFinite).Sample(d =>
        {
            var options = new CastOptions { FormatProvider = Danish };
            var cs = CastSingle("DoubleValue", d.ToString("R", Danish), options);
            Assert.True(cs.IsValid);
            Assert.Equal(d, cs.GetChange<double>("DoubleValue"));
        });

    [Fact]
    public void Cast_StrictCasting_RandomUnknownKeys_ProduceUnpermittedFieldErrors() =>
        Gen.Char['a', 'z'].Array[1, 12].Select(c => new string(c))
            .Where(name => !CoercibleFields.Any(f =>
                string.Equals(f.Field, name, StringComparison.OrdinalIgnoreCase)))
            .Sample(key =>
            {
                var @params = new Dictionary<string, object?> { [key] = "x" };

                var strict = Changeset<CoercionTarget>.Cast(@params, ["IntValue"],
                    new CastOptions { StrictCasting = true });
                Assert.False(strict.IsValid);
                Assert.Equal("unpermitted_field", strict.ErrorsOn(key)[0].Code);

                var lenient = Changeset<CoercionTarget>.Cast(@params, ["IntValue"]);
                Assert.True(lenient.IsValid);
                Assert.Empty(lenient.Changes);
            });

    // ---------------------------------------------------------------------
    // Documented coercion quirks (actual behavior, not necessarily desired).
    // ---------------------------------------------------------------------

    [Fact]
    public void Cast_DecimalString_GroupSeparatorMisplacement_ReinterpretsValue_CurrentBehavior() =>
        Gen.Select(Gen.Int[1, 9], Gen.Int[1, 9]).Sample((whole, frac) =>
        {
            // NumberStyles.Number allows thousands separators without validating
            // group positions, so "1,5" under the invariant culture parses as 15
            // rather than failing — European decimal-comma input is silently
            // reinterpreted. This documents the actual behavior.
            var cs = CastSingle("DecimalValue", $"{whole},{frac}");
            Assert.True(cs.IsValid);
            Assert.Equal(whole * 10 + frac, cs.GetChange<decimal>("DecimalValue"));
        });

    [Fact]
    public void Cast_NumericStringToEnum_AcceptsUndefinedValues_CurrentBehavior() =>
        Gen.Int.Sample(i =>
        {
            // Enum.TryParse accepts any numeric string, including values with no
            // defined enum member — this documents the actual behavior.
            var cs = CastSingle("EnumValue", i.ToString(Invariant));
            Assert.True(cs.IsValid);
            Assert.Equal((UserRole)i, cs.GetChange<UserRole>("EnumValue"));
        });

    [Fact]
    public void Cast_TypedFractionalDouble_ToIntTarget_RoundsSilently_CurrentBehavior() =>
        Gen.Double[-1_000_000, 1_000_000].Sample(d =>
        {
            // Numeric-to-numeric coercion goes through Convert.ChangeType, which
            // rounds fractional values (banker's rounding) instead of erroring —
            // this documents the actual behavior.
            var cs = CastSingle("IntValue", d);
            Assert.True(cs.IsValid);
            Assert.Equal((int)Math.Round(d, MidpointRounding.ToEven), cs.GetChange<int>("IntValue"));
        });
}

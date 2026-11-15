using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Tests.Recordings;

public sealed class TypedTelemetryDomainTests
{
    [Fact]
    public void ExistingChannelConstructorDefaultsToNumeric()
    {
        var channel = new ChannelDefinition(Guid.NewGuid(), "pressure", "psi");
        Assert.Equal(ChannelValueType.Numeric, channel.ValueType);
    }

    [Fact]
    public void TypedChannelPreservesItsDeclaredType()
    {
        var channel = new ChannelDefinition(Guid.NewGuid(), "valve", null, ChannelValueType.Categorical);
        Assert.Equal(ChannelValueType.Categorical, channel.ValueType);
    }

    [Fact]
    public void ExistingNumericSampleExposesBothViews()
    {
        var id = Guid.NewGuid();
        var sample = new TelemetrySample(0, default, new Dictionary<Guid, double?> { [id] = 42.5 });

        Assert.Equal(42.5, sample.GetValue(id));
        Assert.Equal(42.5, Assert.IsType<ChannelValue.Numeric>(sample.GetTypedValue(id)).Value);
    }

    [Fact]
    public void TypedSampleDoesNotEncodeNonNumericValuesAsNumbers()
    {
        var pressure = Guid.NewGuid();
        var valve = Guid.NewGuid();
        var pump = Guid.NewGuid();
        var missing = Guid.NewGuid();

        var sample = TelemetrySample.FromTypedValues(0, default,
            new Dictionary<Guid, ChannelValue?>
            {
                [pressure] = new ChannelValue.Numeric(4500),
                [valve] = new ChannelValue.Categorical("ON"),
                [pump] = new ChannelValue.Boolean(false),
                [missing] = null
            });

        Assert.Equal(4500, sample.GetValue(pressure));
        Assert.False(sample.Values.ContainsKey(valve));
        Assert.False(sample.Values.ContainsKey(pump));
        Assert.False(sample.Values.ContainsKey(missing));
        Assert.Equal("ON", Assert.IsType<ChannelValue.Categorical>(sample.GetTypedValue(valve)).Value);
        Assert.False(Assert.IsType<ChannelValue.Boolean>(sample.GetTypedValue(pump)).Value);
        Assert.True(sample.HasChannel(missing));
        Assert.Null(sample.GetTypedValue(missing));
    }

    [Fact]
    public void TypedSampleCopiesInputDictionary()
    {
        var id = Guid.NewGuid();
        var input = new Dictionary<Guid, ChannelValue?> { [id] = new ChannelValue.Categorical("OFF") };
        var sample = TelemetrySample.FromTypedValues(0, default, input);
        input[id] = new ChannelValue.Categorical("ON");
        Assert.Equal("OFF", Assert.IsType<ChannelValue.Categorical>(sample.GetTypedValue(id)).Value);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteNumericValuesArePreserved(double value)
    {
        var numeric = new ChannelValue.Numeric(value);

        if (double.IsNaN(value))
        {
            Assert.True(double.IsNaN(numeric.Value));
        }
        else
        {
            Assert.Equal(value, numeric.Value);
        }
    }
}

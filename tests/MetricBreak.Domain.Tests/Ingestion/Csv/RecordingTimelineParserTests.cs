using MetricBreak.Domain.Recordings;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Domain.Tests.Ingestion.Csv;

public sealed class RecordingTimelineParserTests
{
    private static string SamplePath(string fileName) =>
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "samples",
                fileName));

    [Fact]
    public void Parse_FracturingTimelineFile_ImportsTelemetryAndAnnotations()
    {
        var path = SamplePath("hydraulic_fracturing_treatment_3hr_1s_timeline.csv");
        Assert.True(File.Exists(path), $"Missing sample: {path}");

        using var reader = OpenReader(path);
        var parsed = new IndexedCsvTelemetryParser().Parse(reader);

        Assert.Equal(10_800, parsed.Samples.Count);
        Assert.False(parsed.Timeline.IsEmpty);
        Assert.Equal(10, parsed.Timeline.Events.Count);
        Assert.Equal(9, parsed.Timeline.Stages.Count);
        Assert.Contains(
            parsed.Timeline.Events,
            item => item.Name == "Start Pumping");
        Assert.Contains(
            parsed.Timeline.Stages,
            item => item.Name.StartsWith("PLF Stage 3", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_HeartRateTimelineFile_ImportsDescriptions()
    {
        var path = SamplePath("heart_rate_3hr_1s_30channels_events_stages.csv");
        Assert.True(File.Exists(path), $"Missing sample: {path}");

        using var reader = OpenReader(path);
        var parsed = new IndexedCsvTelemetryParser().Parse(reader);

        Assert.Equal(10_800, parsed.Samples.Count);
        Assert.False(parsed.Timeline.IsEmpty);

        var started = parsed.Timeline.Events.First(
            item => item.Name == "Recording Started");
        Assert.Equal(
            "Wearable monitoring session begins",
            started.Description);
    }

    [Fact]
    public void Parse_TreatmentDataFile_ImportsEventsAndStages()
    {
        var path = SamplePath("hydraulic_fracturing_3hr_1sec_treatment_data.csv");
        Assert.True(File.Exists(path), $"Missing sample: {path}");

        using var reader = OpenReader(path);
        var parsed = new IndexedCsvTelemetryParser().Parse(reader);

        Assert.Equal(10_800, parsed.Samples.Count);
        Assert.Equal(11, parsed.Timeline.Events.Count);
        Assert.Equal(7, parsed.Timeline.Stages.Count);

        var breakdown = parsed.Timeline.Events.Single(
            item => item.Name == "Formation Breakdown");
        Assert.Contains("pressure drop", breakdown.Description, StringComparison.Ordinal);

        var acid = parsed.Timeline.Stages.Single(
            item => item.Name == "Stage 1: Acid Spearhead");
        Assert.Contains("hydrochloric acid", acid.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_StageDataFile_AcceptsWallClockTimestamps()
    {
        var path = SamplePath("hydraulic_fracturing_stage_data_4hr_1hz.csv");
        Assert.True(File.Exists(path), $"Missing sample: {path}");

        using var reader = OpenReader(path);
        var parsed = new IndexedCsvTelemetryParser().Parse(reader);

        Assert.Equal(14_400, parsed.Samples.Count);
        Assert.Equal(TimestampOrigin.Unix, parsed.TimestampOrigin);
        Assert.Equal(
            TelemetryTimestamp.FromDateTimeOffset(
                new DateTimeOffset(2026, 11, 22, 8, 0, 0, TimeSpan.Zero)),
            parsed.Samples[0].Timestamp);
        Assert.Equal(
            parsed.Samples[0].Timestamp.UnixNanoseconds + 14_399L * 1_000_000_000L,
            parsed.Samples[^1].Timestamp.UnixNanoseconds);
    }

    [Fact]
    public void Parse_TelemetryOnlyFile_HasEmptyTimeline()
    {
        var path = SamplePath("hydraulic_fracturing_treatment_3hr_1s.csv");
        if (!File.Exists(path))
            return;

        using var reader = OpenReader(path);
        var parsed = new IndexedCsvTelemetryParser().Parse(reader);

        Assert.True(parsed.Timeline.IsEmpty);
    }

    private static StreamReader OpenReader(string path)
    {
        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        return new StreamReader(
            stream,
            System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
    }
}
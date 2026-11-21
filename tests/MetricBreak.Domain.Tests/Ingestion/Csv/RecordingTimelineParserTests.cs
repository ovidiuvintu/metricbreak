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
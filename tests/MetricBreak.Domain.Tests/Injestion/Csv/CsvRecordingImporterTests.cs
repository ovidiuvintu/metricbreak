
using System.Security.Cryptography;
using System.Text;
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Replay;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Domain.Tests.Ingestion.Csv;

public sealed class CsvRecordingImporterTests
{
    [Fact]
    public void Import_ValidCsv_CreatesRecordingAndReader()
    {
        const string csv = """
            Timestamp,Pressure,Rate
            2026-11-08T10:00:00Z,8500,72
            2026-11-08T10:00:01Z,8800,70
            2026-11-08T10:00:02Z,9100,68
            """;

        var path = CreateTemporaryCsv(csv);

        try
        {
            var importer = new CsvRecordingImporter();

            var imported = importer.Import(path);

            Assert.NotEqual(Guid.Empty, imported.Recording.Id);
            Assert.Equal(
                imported.Recording.Id,
                imported.Reader.RecordingId);

            Assert.Equal(3, imported.Reader.SampleCount);
            Assert.Equal(2, imported.Channels.Count);

            Assert.Equal(
                "Pressure",
                imported.Channels[0].Name);

            Assert.Equal(
                "Rate",
                imported.Channels[1].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_PreservesOriginalSourceIdentity()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,8500
            """;

        var path = CreateTemporaryCsv(csv);

        try
        {
            var imported = new CsvRecordingImporter().Import(path);

            var bytes = File.ReadAllBytes(path);

            Assert.Equal(
                Path.GetFileName(path),
                imported.Recording.Source.Name);

            Assert.Equal(
                bytes.LongLength,
                imported.Recording.Source.SizeInBytes);

            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(bytes)),
                imported.Recording.Source.ContentHash);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_SameFileTwice_PreservesHashButCreatesDistinctRecordings()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,8500
            """;

        var path = CreateTemporaryCsv(csv);

        try
        {
            var importer = new CsvRecordingImporter();

            var first = importer.Import(path);
            var second = importer.Import(path);

            Assert.NotEqual(
                first.Recording.Id,
                second.Recording.Id);

            Assert.Equal(
                first.Recording.Source.ContentHash,
                second.Recording.Source.ContentHash);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_CsvRecording_CanReplayToBreakpoint()
    {
        const string csv = """
            Timestamp,Pressure,Rate
            2026-11-08T10:00:00Z,8500,72
            2026-11-08T10:00:01Z,8800,70
            2026-11-08T10:00:02Z,9100,68
            2026-11-08T10:00:03Z,9300,55
            """;

        var path = CreateTemporaryCsv(csv);

        try
        {
            var imported = new CsvRecordingImporter().Import(path);

            var session = new ReplaySession(
                imported.Recording,
                imported.Reader);

            var pressureChannel = Assert.Single(
                imported.Channels,
                channel => channel.Name == "Pressure");

            var breakpoint = new ConditionalBreakpoint(
                Guid.NewGuid(),
                pressureChannel.Id,
                ComparisonOperator.GreaterThan,
                9000);

            var breakpoints = new BreakpointCollection();
            breakpoints.Add(breakpoint);

            var stop = session.RunToBreakpoints(breakpoints);

            Assert.NotNull(stop);
            Assert.Equal(2, stop.Position.SequenceNumber);

            var hit = Assert.Single(stop.Hits);

            Assert.Equal(9100d, hit.ObservedValue);
            Assert.Equal(
                pressureChannel.Id,
                hit.ChannelId);

            Assert.Equal(
                2,
                session.Position.SequenceNumber);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_InvalidCsv_ThrowsFormatException()
    {
        const string csv = """
            Timestamp,Pressure
            invalid-timestamp,8500
            """;

        var path = CreateTemporaryCsv(csv);

        try
        {
            var importer = new CsvRecordingImporter();

            Assert.Throws<FormatException>(() =>
                importer.Import(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_NonexistentFile_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.csv");

        Assert.Throws<FileNotFoundException>(() =>
            new CsvRecordingImporter().Import(path));
    }

    [Fact]
    public void Import_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CsvRecordingImporter().Import(null!));
    }

    [Fact]
    public void Import_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new CsvRecordingImporter().Import(""));
    }

    [Fact]
    public void Import_InvalidUtf8_ThrowsDecoderFallbackException()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.csv");

        try
        {
            File.WriteAllBytes(path, new byte[]
            {
                0x54, 0x69, 0x6D, 0x65,
                0x73, 0x74, 0x61, 0x6D,
                0x70, 0x0A, 0xFF
            });

            Assert.Throws<DecoderFallbackException>(() =>
                new CsvRecordingImporter().Import(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTemporaryCsv(string content)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.csv");

        File.WriteAllText(
            path,
            content,
            new UTF8Encoding(false));

        return path;
    }
}

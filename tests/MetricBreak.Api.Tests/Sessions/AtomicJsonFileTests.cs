
using System.Text.Json;
using MetricBreak.Api.Sessions;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class AtomicJsonFileTests
{
    [Fact]
    public void Write_WhenInterruptedBeforeCommit_PreservesPreviousDocument()
    {
        WithTemporaryDirectory(directory =>
        {
            var destination = Path.Combine(
                directory, "project.mbproj.json");

            File.WriteAllText(
                destination, "{\"version\":1}");

            Assert.Throws<IOException>(() =>
                AtomicJsonFile.Write(
                    destination,
                    "{\"version\":2}",
                    () => throw new IOException(
                        "Simulated interruption")));

            Assert.Equal(
                "{\"version\":1}",
                File.ReadAllText(destination));

            Assert.Single(Directory.GetFiles(directory));
        });
    }

    [Fact]
    public void Write_NewDocument_WhenInterrupted_LeavesNoDestinationOrTemporaryFile()
    {
        WithTemporaryDirectory(directory =>
        {
            var destination = Path.Combine(
                directory, "investigation.json");

            Assert.Throws<IOException>(() =>
                AtomicJsonFile.Write(
                    destination,
                    "{\"value\":42}",
                    () => throw new IOException(
                        "Simulated interruption")));

            Assert.False(File.Exists(destination));
            Assert.Empty(Directory.GetFiles(directory));
        });
    }

    [Fact]
    public void Write_ReplacesExistingDocumentWithValidJsonAndNoTemporaryFiles()
    {
        WithTemporaryDirectory(directory =>
        {
            var destination = Path.Combine(
                directory, "workspace-state.json");

            AtomicJsonFile.Write(
                destination, "{\"value\":1}");

            AtomicJsonFile.Write(
                destination, "{\"value\":2}");

            using var document = JsonDocument.Parse(
                File.ReadAllText(destination));

            Assert.Equal(
                2,
                document.RootElement
                    .GetProperty("value")
                    .GetInt32());

            Assert.Single(Directory.GetFiles(directory));
        });
    }

    private static void WithTemporaryDirectory(
        Action<string> action)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-atomic-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        try
        {
            action(directory);
        }
        finally
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }
}

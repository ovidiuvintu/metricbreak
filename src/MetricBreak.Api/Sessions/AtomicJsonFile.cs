
using System.Text;

namespace MetricBreak.Api.Sessions;

/// <summary>
/// Publishes a complete UTF-8 JSON document using a same-directory rename.
/// This protects the destination from partial writes; it does not make
/// multi-file updates transactional or coordinate concurrent writers.
/// </summary>
internal static class AtomicJsonFile
{
    internal static void Write(
        string destinationPath,
        string json,
        Action? beforeCommit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(json);

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                using var writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false),
                    leaveOpen: true);

                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            beforeCommit?.Invoke();

            File.Move(
                temporaryPath,
                fullPath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

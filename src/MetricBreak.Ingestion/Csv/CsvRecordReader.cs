
using System.Text;

namespace MetricBreak.Ingestion.Csv;

internal static class CsvRecordReader
{
    public static List<string>? ReadRecord(TextReader reader)
    {
        var fields = new List<string>();
        var field = new StringBuilder();

        var inQuotes = false;
        var quotedFieldClosed = false;
        var atFieldStart = true;
        var sawInput = false;

        while (true)
        {
            var next = reader.Read();

            if (next == -1)
            {
                if (inQuotes)
                    throw new FormatException(
                        "Unterminated quoted CSV field.");

                if (!sawInput)
                    return null;

                fields.Add(field.ToString());
                return fields;
            }

            sawInput = true;
            var c = (char)next;

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                        quotedFieldClosed = true;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                atFieldStart = true;
                quotedFieldClosed = false;
                continue;
            }

            if (c == '\r' || c == '\n')
            {
                if (c == '\r' && reader.Peek() == '\n')
                    reader.Read();

                fields.Add(field.ToString());
                return fields;
            }

            if (quotedFieldClosed)
            {
                throw new FormatException(
                    "Unexpected character after closing CSV quote.");
            }

            if (c == '"')
            {
                if (!atFieldStart)
                {
                    throw new FormatException(
                        "Unexpected quote inside unquoted CSV field.");
                }

                inQuotes = true;
                atFieldStart = false;
                continue;
            }

            field.Append(c);
            atFieldStart = false;
        }
    }
}

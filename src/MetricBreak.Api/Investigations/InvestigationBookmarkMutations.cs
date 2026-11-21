namespace MetricBreak.Api.Investigations;

internal static class InvestigationBookmarkMutations
{
    public static ExportedInvestigation Normalize(ExportedInvestigation investigation)
    {
        ArgumentNullException.ThrowIfNull(investigation);

        return investigation.Bookmarks is not null
            ? investigation
            : investigation with { Bookmarks = Array.Empty<InvestigationBookmarkSnapshot>() };
    }

    public static IReadOnlyList<InvestigationBookmarkSnapshot> Sort(
        IEnumerable<InvestigationBookmarkSnapshot> bookmarks)
    {
        return bookmarks
            .OrderBy(bookmark => bookmark.SampleSequence)
            .ThenBy(bookmark => bookmark.CreatedAtUtc)
            .ThenBy(bookmark => bookmark.BookmarkId)
            .ToArray();
    }

    public static bool TryValidateSequence(
        ExportedInvestigation investigation,
        long sampleSequence,
        out string? error)
    {
        error = null;

        if (sampleSequence < 0 ||
            sampleSequence >= investigation.Recording.SampleCount)
        {
            error = $"Sample sequence {sampleSequence} is outside the recording.";
            return false;
        }

        return true;
    }

    public static InvestigationBookmarkSnapshot Create(
        long sampleSequence,
        long timestampNanoseconds,
        string? title,
        string? annotation)
    {
        var now = DateTimeOffset.UtcNow;

        return new InvestigationBookmarkSnapshot(
            BookmarkId: Guid.NewGuid(),
            SampleSequence: sampleSequence,
            TimestampNanoseconds: timestampNanoseconds,
            Title: NormalizeOptionalText(title),
            Annotation: NormalizeOptionalText(annotation),
            CreatedAtUtc: now,
            ModifiedAtUtc: now);
    }

    public static ExportedInvestigation Add(
        ExportedInvestigation investigation,
        InvestigationBookmarkSnapshot bookmark)
    {
        investigation = Normalize(investigation);

        var bookmarks = investigation.Bookmarks!.ToList();
        bookmarks.Add(bookmark);

        return investigation with
        {
            ExportedAtUtc = DateTimeOffset.UtcNow,
            Bookmarks = Sort(bookmarks),
        };
    }

    public static bool TryUpdate(
        ExportedInvestigation investigation,
        Guid bookmarkId,
        string? title,
        string? annotation,
        out ExportedInvestigation? updated,
        out string? error)
    {
        investigation = Normalize(investigation);
        updated = null;
        error = null;

        var existing = investigation.Bookmarks!
            .FirstOrDefault(bookmark => bookmark.BookmarkId == bookmarkId);

        if (existing is null)
        {
            error = "Bookmark was not found.";
            return false;
        }

        var replacement = existing with
        {
            Title = NormalizeOptionalText(title),
            Annotation = NormalizeOptionalText(annotation),
            ModifiedAtUtc = DateTimeOffset.UtcNow,
        };

        var bookmarks = investigation.Bookmarks!
            .Select(bookmark =>
                bookmark.BookmarkId == bookmarkId ? replacement : bookmark)
            .ToArray();

        updated = investigation with
        {
            ExportedAtUtc = DateTimeOffset.UtcNow,
            Bookmarks = Sort(bookmarks),
        };

        return true;
    }

    public static bool TryDelete(
        ExportedInvestigation investigation,
        Guid bookmarkId,
        out ExportedInvestigation? updated,
        out string? error)
    {
        investigation = Normalize(investigation);
        updated = null;
        error = null;

        var bookmarks = investigation.Bookmarks!
            .Where(bookmark => bookmark.BookmarkId != bookmarkId)
            .ToArray();

        if (bookmarks.Length == investigation.Bookmarks!.Count)
        {
            error = "Bookmark was not found.";
            return false;
        }

        updated = investigation with
        {
            ExportedAtUtc = DateTimeOffset.UtcNow,
            Bookmarks = Sort(bookmarks),
        };

        return true;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }
}
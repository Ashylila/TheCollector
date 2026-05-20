using System.Collections.Generic;

namespace TheCollector.Data.Models;

// Numeric values match OtterGui.Widgets.ChangeLogDisplayType so existing
// configs migrate transparently (Dalamud serializes enums as integers).
public enum ChangeLogDisplayType
{
    New           = 0,
    HighlightOnly = 1,
    Never         = 2,
}

public enum ChangelogEntryKind
{
    Entry,
    Highlight,
    Important,
}

public class ChangelogEntry
{
    public string Text { get; }
    public ChangelogEntryKind Kind { get; }

    public ChangelogEntry(string text, ChangelogEntryKind kind)
    {
        Text = text;
        Kind = kind;
    }
}

public class ChangelogVersion
{
    public string Title { get; }
    public int Index { get; }
    public List<ChangelogEntry> Entries { get; } = new();

    public ChangelogVersion(string title, int index)
    {
        Title = title;
        Index = index;
    }

    public bool HasHighlight
    {
        get
        {
            foreach (var e in Entries)
                if (e.Kind == ChangelogEntryKind.Highlight) return true;
            return false;
        }
    }

    public bool HasImportant
    {
        get
        {
            foreach (var e in Entries)
                if (e.Kind == ChangelogEntryKind.Important) return true;
            return false;
        }
    }

    public ChangelogVersion RegisterEntry(string text)
    {
        Entries.Add(new ChangelogEntry(text, ChangelogEntryKind.Entry));
        return this;
    }

    public ChangelogVersion RegisterHighlight(string text)
    {
        Entries.Add(new ChangelogEntry(text, ChangelogEntryKind.Highlight));
        return this;
    }

    public ChangelogVersion RegisterImportant(string text)
    {
        Entries.Add(new ChangelogEntry(text, ChangelogEntryKind.Important));
        return this;
    }
}

public class ChangelogBook
{
    public List<ChangelogVersion> Versions { get; } = new();

    public ChangelogVersion NextVersion(string title)
    {
        var v = new ChangelogVersion(title, Versions.Count);
        Versions.Add(v);
        return v;
    }
}

namespace Content.Client._Lust.Book;

/// <summary>Selected localized pages and any document-level or unselected-locale error.</summary>
public sealed record BookContentParseResult(
    IReadOnlyList<BookPageData> Pages,
    string Locale,
    BookContentError? Error = null)
{
    /// <summary>Includes both document errors and errors attached to individual pages.</summary>
    public bool HasErrors
    {
        get
        {
            if (Error != null)
                return true;

            foreach (var page in Pages)
            {
                if (page.Error != null)
                    return true;
            }

            return false;
        }
    }
}

/// <summary>Diagnostics for logging alongside the book resource path; page indices are zero-based.</summary>
public sealed record BookContentError(
    BookContentErrorKind Kind,
    string Details,
    string? Locale = null,
    int? PageIndex = null)
{
    /// <summary>Localize this key for the reader; technical diagnostics belong in the log.</summary>
    public LocId MessageKey => "book-content-error";
}

/// <summary>Distinguishes invalid document structure from failures on an individual page.</summary>
public enum BookContentErrorKind : byte
{
    InvalidXml,
    InvalidStructure,
    MissingEnglishLocale,
    MismatchedPageCounts,
    InvalidImagePath,
    MissingImageResource,
    InvalidColor,
    ContentTooLarge,
    MissingContentResource,
}

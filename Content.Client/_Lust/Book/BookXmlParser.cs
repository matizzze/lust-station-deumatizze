using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using static Content.Client._Lust.Book.BookXmlReader;

namespace Content.Client._Lust.Book;

/// <summary>Parses the restricted book XML format without resolving external resources or UI markup.</summary>
public sealed class BookXmlParser
{
    private const string EnglishLocale = "en-US";
    private const int MaxDocumentCharacters = 2_000_000;
    private const int MaxInlineDepth = 32;

    private readonly Func<ResPath, bool> _imageExists;

    /// <summary>Uses the mounted game content roots for image existence checks.</summary>
    public BookXmlParser(IResourceManager resources) : this(resources.ContentFileExists)
    {
    }

    /// <summary>Accepts an image resource lookup without requiring a running client.</summary>
    public BookXmlParser(Func<ResPath, bool> imageExists)
    {
        _imageExists = imageExists;
    }

    /// <summary>Reads static game content and converts resource failures into reader diagnostics.</summary>
    public BookContentParseResult ParseResource(IResourceManager resources, string path, string locale)
    {
        string xml;
        try
        {
            xml = resources.ContentFileReadAllText(path);
        }
        catch (Exception exception)
        {
            return Failure(BookContentErrorKind.MissingContentResource,
                $"Cannot read book resource '{path}': {exception.Message}", locale);
        }

        return Parse(xml, locale);
    }

    /// <summary>
    /// Requires English content, falls back to it when the requested locale is absent,
    /// and preserves the position of pages with invalid content as page-local errors.
    /// </summary>
    public BookContentParseResult Parse(string xml, string locale)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return Failure(BookContentErrorKind.InvalidXml, "The book XML is empty.", locale);

        if (xml.Length > MaxDocumentCharacters)
            return Failure(BookContentErrorKind.ContentTooLarge, "The book XML exceeds the document size limit.", locale);

        try
        {
            // Проверяем весь синтаксис до обращения к ресурсам, включая непросматриваемые локали.
            var syntax = new BookXmlReader(xml);
            while (syntax.Current.Kind != TokenKind.EndOfDocument)
                syntax.Read();

            var reader = new BookXmlReader(xml);
            SkipWhitespace(reader);
            var root = RequireElement(reader, "book");
            ValidateAttributes(root);
            reader.Read();

            var locales = new Dictionary<string, List<BookPageData>>(StringComparer.OrdinalIgnoreCase);
            if (!root.Empty)
            {
                while (ReadChild(reader))
                {
                    var localized = RequireElement(reader, "locale");
                    ValidateAttributes(localized, "id");
                    var id = RequiredAttribute(localized, "id");
                    if (locales.ContainsKey(id))
                        throw InvalidStructure($"The locale '{id}' occurs more than once.");

                    var pages = new List<BookPageData>();
                    reader.Read();
                    if (!localized.Empty)
                    {
                        while (ReadChild(reader))
                        {
                            var page = RequireElement(reader, "page");
                            pages.Add(ParsePage(reader, page, id, pages.Count));
                        }

                        reader.Read();
                    }

                    locales.Add(id, pages);
                }

                reader.Read();
            }

            SkipWhitespace(reader);
            if (reader.Current.Kind != TokenKind.EndOfDocument)
                throw InvalidStructure("Only the book element is allowed in the document.");

            if (!locales.TryGetValue(EnglishLocale, out var english))
                return Failure(BookContentErrorKind.MissingEnglishLocale, "The book requires an en-US locale.", locale);

            if (locales.TryGetValue("ru-RU", out var russian) && russian.Count != english.Count)
            {
                return Failure(BookContentErrorKind.MismatchedPageCounts,
                    "The ru-RU and en-US locales must contain the same number of pages.", "ru-RU");
            }

            var selectedLocale = locales.ContainsKey(locale) ? locale : EnglishLocale;
            BookContentError? otherLocaleError = null;
            foreach (var (localeId, pages) in locales)
            {
                if (string.Equals(localeId, selectedLocale, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var page in pages)
                    otherLocaleError ??= page.Error;
            }

            return new BookContentParseResult(locales[selectedLocale].ToArray(), selectedLocale, otherLocaleError);
        }
        catch (SyntaxException exception)
        {
            return Failure(BookContentErrorKind.InvalidXml, exception.Message, locale);
        }
        catch (BookValidationException exception)
        {
            return Failure(exception.Kind, exception.Message, locale);
        }
    }

    private BookPageData ParsePage(BookXmlReader reader, Token page, string locale, int index)
    {
        try
        {
            ValidateAttributes(page);
            reader.Read();
            var nodes = new List<BookPageNode>();
            if (!page.Empty)
            {
                while (ReadChild(reader))
                {
                    var block = reader.Current;
                    if (block.Kind != TokenKind.StartElement)
                        throw InvalidStructure("Only supported blocks are allowed inside a page.");

                    switch (block.Name)
                    {
                        case "p":
                        case "h1":
                            ValidateAttributes(block);
                            var children = ParseInline(reader, block, 0);
                            nodes.Add(block.Name == "p" ? new BookParagraphNode(children) : new BookHeadingNode(children));
                            break;
                        case "image":
                            nodes.Add(ParseImage(reader, block));
                            break;
                        default:
                            throw InvalidStructure($"Unsupported page element <{block.Name}>.");
                    }
                }

                reader.Read();
            }

            return new BookPageData(nodes.ToArray());
        }
        catch (BookValidationException exception)
        {
            // Пропускаем остаток страницы без рекурсии, сохраняя индексы следующих страниц.
            reader.SkipElement(page);
            return new BookPageData(Array.Empty<BookPageNode>(),
                new BookContentError(exception.Kind, exception.Message, locale, index));
        }
    }

    private static IReadOnlyList<BookPageNode> ParseInline(BookXmlReader reader, Token parent, int depth)
    {
        if (depth > MaxInlineDepth)
            throw InvalidStructure("Inline formatting exceeds the nesting limit.");

        reader.Read();
        var nodes = new List<BookPageNode>();
        if (parent.Empty)
            return nodes.ToArray();

        while (reader.Current.Kind != TokenKind.EndElement)
        {
            var token = reader.Current;
            if (token.Kind == TokenKind.Text)
            {
                nodes.Add(new BookTextNode(token.Text));
                reader.Read();
                continue;
            }

            if (token.Kind != TokenKind.StartElement)
                throw InvalidStructure("Only literal text and supported inline elements are allowed here.");

            switch (token.Name)
            {
                case "b":
                    ValidateAttributes(token);
                    nodes.Add(new BookBoldNode(ParseInline(reader, token, depth + 1)));
                    break;
                case "i":
                    ValidateAttributes(token);
                    nodes.Add(new BookItalicNode(ParseInline(reader, token, depth + 1)));
                    break;
                case "color":
                    ValidateAttributes(token, "value");
                    var colorText = RequiredAttribute(token, "value", BookContentErrorKind.InvalidColor);
                    if (!Color.TryParse(colorText, out var color))
                        throw new BookValidationException(BookContentErrorKind.InvalidColor, $"Invalid text color '{colorText}'.");

                    nodes.Add(new BookColorNode(color, ParseInline(reader, token, depth + 1)));
                    break;
                default:
                    throw InvalidStructure($"Unsupported inline element <{token.Name}>.");
            }
        }

        reader.Read();
        return nodes.ToArray();
    }

    private BookImageNode ParseImage(BookXmlReader reader, Token image)
    {
        ValidateAttributes(image, "src");
        reader.Read();
        if (!image.Empty)
        {
            if (reader.Current.Kind != TokenKind.EndElement)
                throw InvalidStructure("An <image> cannot contain text or child elements.");

            reader.Read();
        }

        var source = RequiredAttribute(image, "src", BookContentErrorKind.InvalidImagePath);
        if (!source.StartsWith("/Textures/", StringComparison.Ordinal) ||
            source.IndexOfAny(new[] { '\\', ':', '?', '#', '%' }) >= 0)
        {
            throw new BookValidationException(BookContentErrorKind.InvalidImagePath, $"Invalid game image path '{source}'.");
        }

        foreach (var segment in source[1..].Split('/'))
        {
            if (string.IsNullOrWhiteSpace(segment) || segment is "." or "..")
                throw new BookValidationException(BookContentErrorKind.InvalidImagePath, $"Invalid game image path '{source}'.");

            foreach (var character in segment)
            {
                if (char.IsControl(character))
                    throw new BookValidationException(BookContentErrorKind.InvalidImagePath, "Image paths cannot contain control characters.");
            }
        }

        try
        {
            var path = new ResPath(source);
            if (_imageExists(path))
                return new BookImageNode(path);
        }
        catch (Exception exception)
        {
            throw new BookValidationException(BookContentErrorKind.MissingImageResource,
                $"Cannot read game image '{source}': {exception.Message}");
        }

        throw new BookValidationException(BookContentErrorKind.MissingImageResource, $"Missing game image '{source}'.");
    }

    private static bool ReadChild(BookXmlReader reader)
    {
        SkipWhitespace(reader);
        if (reader.Current.Kind == TokenKind.EndElement)
            return false;

        if (reader.Current.Kind != TokenKind.StartElement)
            throw InvalidStructure("Only child elements and whitespace are allowed here.");

        return true;
    }

    private static void SkipWhitespace(BookXmlReader reader)
    {
        while (reader.Current.Kind == TokenKind.Text && string.IsNullOrWhiteSpace(reader.Current.Text))
            reader.Read();
    }

    private static Token RequireElement(BookXmlReader reader, string name)
    {
        var token = reader.Current;
        if (token.Kind != TokenKind.StartElement || token.Name != name)
            throw InvalidStructure($"Expected <{name}> without a namespace.");

        return token;
    }

    private static void ValidateAttributes(Token element, string? allowed = null)
    {
        if (element.Attributes == null)
            return;

        foreach (var name in element.Attributes.Keys)
        {
            if (name != allowed)
                throw InvalidStructure($"Unsupported attribute '{name}' on <{element.Name}>.");
        }
    }

    private static string RequiredAttribute(Token element, string name,
        BookContentErrorKind kind = BookContentErrorKind.InvalidStructure)
    {
        if (element.Attributes == null || !element.Attributes.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            throw new BookValidationException(kind, $"<{element.Name}> requires a nonempty '{name}' attribute.");

        return value;
    }

    private static BookContentParseResult Failure(BookContentErrorKind kind, string details, string locale)
    {
        return new BookContentParseResult(Array.Empty<BookPageData>(), locale,
            new BookContentError(kind, details, locale));
    }

    private static BookValidationException InvalidStructure(string details)
    {
        return new BookValidationException(BookContentErrorKind.InvalidStructure, details);
    }

    private sealed class BookValidationException(BookContentErrorKind kind, string message) : Exception(message)
    {
        public BookContentErrorKind Kind { get; } = kind;
    }
}

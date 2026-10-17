#nullable enable
using Content.Client._Lust.Book;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Lust.Books;

[TestFixture]
public sealed class BookXmlParserTest
{
    private const string BilingualBook = """
        <book>
          <locale id="en-US">
            <page><h1>Title</h1><p>Text <b>bold <i>italic</i></b> <color value="#ff0000">red</color>.</p><image src="/Textures/Books/test.png" /></page>
            <page><p>Second page.</p></page>
          </locale>
          <locale id="ru-RU">
            <page><h1>Название</h1><p>Первая страница.</p></page>
            <page><p>Вторая страница.</p></page>
          </locale>
        </book>
        """;

    [Test]
    public void ParsesTwoPagesAndFormatting()
    {
        var result = new BookXmlParser(_ => true).Parse(BilingualBook, "en-US");

        Assert.That(result.HasErrors, Is.False);
        Assert.That(result.Locale, Is.EqualTo("en-US"));
        Assert.That(result.Pages, Has.Count.EqualTo(2));
        Assert.That(result.Pages[0].Nodes[0], Is.TypeOf<BookHeadingNode>());
        var paragraph = (BookParagraphNode) result.Pages[0].Nodes[1];
        var bold = (BookBoldNode) paragraph.Children[1];
        Assert.That(bold.Children[1], Is.TypeOf<BookItalicNode>());
        var color = (BookColorNode) paragraph.Children[3];
        Assert.That(color.Color, Is.EqualTo(Color.Red));
        Assert.That(((BookImageNode) result.Pages[0].Nodes[2]).Source.CanonPath,
            Is.EqualTo("/Textures/Books/test.png"));
    }

    [Test]
    public void SelectsRussianLocale()
    {
        var result = new BookXmlParser(_ => true).Parse(BilingualBook, "ru-RU");

        Assert.That(result.HasErrors, Is.False);
        Assert.That(result.Locale, Is.EqualTo("ru-RU"));
        var heading = (BookHeadingNode) result.Pages[0].Nodes[0];
        Assert.That(((BookTextNode) heading.Children[0]).Text, Is.EqualTo("Название"));
    }

    [TestCase("ru-RU")]
    [TestCase("de-DE")]
    public void MissingRequestedLocaleFallsBackToEnglish(string locale)
    {
        var result = new BookXmlParser(_ => true).Parse(
            "<book><locale id='en-US'><page><p>English</p></page></locale></book>", locale);

        Assert.That(result.HasErrors, Is.False);
        Assert.That(result.Locale, Is.EqualTo("en-US"));
        var paragraph = (BookParagraphNode) result.Pages[0].Nodes[0];
        Assert.That(((BookTextNode) paragraph.Children[0]).Text, Is.EqualTo("English"));
    }

    [TestCase("")]
    [TestCase("<book>")]
    [TestCase("<book><locale id='en-US'><page><p>broken</page></locale></book>")]
    [TestCase("<!DOCTYPE book [<!ENTITY x 'expanded'>]><book><locale id='en-US'><page><p>&x;</p></page></locale></book>")]
    [TestCase("<!DOCTYPE book SYSTEM 'https://example.com/book.dtd'><book />")]
    [TestCase("<!DOCTYPE book [<!ENTITY x SYSTEM 'file:///C:/Windows/win.ini'>]><book><locale id='en-US'><page><p>&x;</p></page></locale></book>")]
    public void MalformedXmlAndDtdReturnErrors(string xml)
    {
        var parser = new BookXmlParser(_ => throw new InvalidOperationException("Unexpected resource lookup."));
        BookContentParseResult? result = null;

        Assert.DoesNotThrow(() => result = parser.Parse(xml, "en-US"));
        Assert.That(result!.Error, Is.Not.Null);
        Assert.That(result.HasErrors, Is.True);
    }

    [TestCase("<book><locale id='ru-RU'><page /></locale></book>", BookContentErrorKind.MissingEnglishLocale)]
    [TestCase("<book><locale id='en-US'><page /></locale><locale id='ru-RU' /></book>", BookContentErrorKind.MismatchedPageCounts)]
    [TestCase("<book><locale id='en-US' /><locale id='en-US' /></book>", BookContentErrorKind.InvalidStructure)]
    [TestCase("<book extra='bad'><locale id='en-US' /></book>", BookContentErrorKind.InvalidStructure)]
    [TestCase("<book xmlns='external'><locale id='en-US' /></book>", BookContentErrorKind.InvalidStructure)]
    [TestCase("<book><locale id='en-US'><unknown /></locale></book>", BookContentErrorKind.InvalidStructure)]
    public void InvalidBookStructureReturnsContentError(string xml, BookContentErrorKind kind)
    {
        var result = new BookXmlParser(_ => true).Parse(xml, "en-US");

        Assert.That(result.Error?.Kind, Is.EqualTo(kind));
        Assert.That(result.HasErrors, Is.True);
    }

    [TestCase("<unknown />")]
    [TestCase("<p unknown='bad'>Text</p>")]
    [TestCase("<p><h1>Nested block</h1></p>")]
    [TestCase("<p><image src='/Textures/Books/test.png' /></p>")]
    [TestCase("<b>Outside paragraph</b>")]
    [TestCase("<image src='/Textures/Books/test.png'><p>Children</p></image>")]
    [TestCase("<p><b onclick='bad'>Text</b></p>")]
    [TestCase("<?instruction bad?>")]
    public void InvalidPagePreservesOtherPages(string invalidPage)
    {
        var result = new BookXmlParser(_ => true).Parse(
            $"<book><locale id='en-US'><page>{invalidPage}</page><page><p>Valid</p></page></locale></book>", "en-US");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Pages, Has.Count.EqualTo(2));
        Assert.That(result.Pages[0].Error?.Kind, Is.EqualTo(BookContentErrorKind.InvalidStructure));
        Assert.That(result.Pages[0].Error?.PageIndex, Is.Zero);
        Assert.That(result.Pages[0].Error?.Locale, Is.EqualTo("en-US"));
        Assert.That(result.Pages[0].Nodes, Is.Empty);
        Assert.That(result.Pages[1].Error, Is.Null);
        Assert.That(result.Pages[1].Nodes, Has.Count.EqualTo(1));
    }

    [TestCase("https://example.com/image.png")]
    [TestCase("file:///C:/image.png")]
    [TestCase("C:\\image.png")]
    [TestCase("//example.com/image.png")]
    [TestCase("Textures/image.png")]
    [TestCase("/Textures/../image.png")]
    [TestCase("/Textures/./image.png")]
    [TestCase("/Textures//image.png")]
    [TestCase("/Textures/image.png?remote=true")]
    [TestCase("/Textures/%2e%2e/image.png")]
    [TestCase("/Books/book.xml")]
    [TestCase("")]
    public void RejectsNonGameImagePathsBeforeResourceLookup(string source)
    {
        var parser = new BookXmlParser(_ => throw new InvalidOperationException("Unsafe resource lookup."));
        var result = parser.Parse(
            $"<book><locale id='en-US'><page><image src='{source}' /></page></locale></book>", "en-US");

        Assert.That(result.Pages[0].Error?.Kind, Is.EqualTo(BookContentErrorKind.InvalidImagePath));
    }

    [Test]
    public void MissingGameImageReturnsOwningPageError()
    {
        var result = new BookXmlParser(_ => false).Parse(BilingualBook, "en-US");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Pages[0].Error?.Kind, Is.EqualTo(BookContentErrorKind.MissingImageResource));
        Assert.That(result.Pages[1].Error, Is.Null);
    }

    [TestCase("not-a-color")]
    [TestCase("")]
    public void RejectsInvalidColors(string color)
    {
        var result = new BookXmlParser(_ => true).Parse(
            $"<book><locale id='en-US'><page><p><color value='{color}'>Text</color></p></page></locale></book>", "en-US");

        Assert.That(result.Pages[0].Error?.Kind, Is.EqualTo(BookContentErrorKind.InvalidColor));
    }

    [Test]
    public void AcceptsEmptyBook()
    {
        var result = new BookXmlParser(_ => true).Parse("<book><locale id='en-US' /></book>", "en-US");

        Assert.That(result.HasErrors, Is.False);
        Assert.That(result.Pages, Is.Empty);
    }

    [Test]
    public void ValidatesUnselectedLocale()
    {
        var result = new BookXmlParser(_ => true).Parse("""
            <book>
              <locale id='en-US'><page><p>Valid</p></page></locale>
              <locale id='ru-RU'><page><script /></page></locale>
            </book>
            """, "en-US");

        Assert.That(result.Error?.Kind, Is.EqualTo(BookContentErrorKind.InvalidStructure));
        Assert.That(result.Error?.Locale, Is.EqualTo("ru-RU"));
        Assert.That(result.Error?.PageIndex, Is.Zero);
        Assert.That(result.Pages[0].Error, Is.Null);
    }

    [Test]
    public void KeepsRichTextMarkupAsLiteralText()
    {
        var result = new BookXmlParser(_ => true).Parse(
            "<book><locale id='en-US'><page><p>[color=red]literal[/color] &amp; text</p></page></locale></book>", "en-US");

        var paragraph = (BookParagraphNode) result.Pages[0].Nodes[0];
        Assert.That(((BookTextNode) paragraph.Children[0]).Text, Is.EqualTo("[color=red]literal[/color] & text"));
    }
}

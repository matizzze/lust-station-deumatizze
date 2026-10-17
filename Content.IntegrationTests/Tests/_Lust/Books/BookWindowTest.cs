#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Client._Lust.Book;
using Content.Shared._Lust.Book;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Lust.Books;

[TestFixture]
public sealed class BookWindowTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: bookCover
  id: LustTestEditorCoverFirst
  name: paper-ui-blank-page-message
  coverState: cover_base
- type: bookCover
  id: LustTestEditorCoverSecond
  name: paper-ui-blank-page-message
  coverState: cover_strong
- type: bookCover
  id: LustTestEditorCoverOther
  name: paper-ui-blank-page-message
  coverState: cover_old
""";

    private static readonly ProtoId<BookCoverPrototype> FirstCover = "LustTestEditorCoverFirst";
    private static readonly ProtoId<BookCoverPrototype> SecondCover = "LustTestEditorCoverSecond";

    [TestCase(true, true, false, true)]
    [TestCase(true, false, false, false)]
    [TestCase(false, true, false, false)]
    [TestCase(true, true, true, false)]
    public async Task EditorRequiresWritableDynamicBookAndServerAuthorization(bool editable, bool authorized, bool isStatic, bool expected)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var client = pair.Client;
        BookWindow? window = null;
        await client.WaitPost(() =>
        {
            var book = CreateEditableBook();
            book.Editable = editable;
            book.ContentPath = isStatic ? "/Books/_Lust/static.xml" : null;
            window = new BookWindow();
            window.PopulatePages(book.Pages);
            window.UpdateEditor(book, authorized);
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(window!.IsEditing, Is.EqualTo(expected));
            Assert.That(window.FindControl<BoxContainer>("EditorContainer").Visible, Is.EqualTo(expected));
            Assert.That(window.FindControl<BoxContainer>("ReaderContainer").Visible, Is.EqualTo(!expected));
        });
        await client.WaitPost(() => window!.Dispose());
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SaveIncludesEveryEditedPageAndOnlyPermittedCover()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var client = pair.Client;
        BookWindow? window = null;
        List<string>? submitted = null;
        ProtoId<BookCoverPrototype>? submittedCover = null;
        var book = CreateEditableBook();
        bool saved = false;
        bool otherCoverSelected = true;
        int saves = 0;
        await client.WaitPost(() =>
        {
            window = new BookWindow();
            window.OnSaved += (pages, cover) =>
            {
                submitted = pages;
                submittedCover = cover;
                saves++;
            };
            window.PopulatePages(book.Pages);
            window.UpdateEditor(book, true);
            window.FindControl<TextEdit>("PageEditor").TextRope = new Rope.Leaf("Edited first");
            window.FindControl<ItemList>("PageSelector")[1].Selected = true;
            window.FindControl<TextEdit>("PageEditor").TextRope = new Rope.Leaf("Edited second");
            window.TrySelectPage(0);
            otherCoverSelected = window.TrySelectCover("LustTestEditorCoverOther");
            window.TrySelectCover(SecondCover);
            saved = window.TrySave();
            window.FindControl<TextEdit>("PageEditor").TextRope = new Rope.Leaf("Changed after submit");
            window.TrySelectPage(1);
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(saves, Is.EqualTo(1));
            Assert.That(submitted, Is.EqualTo(new[] { "Edited first", "Edited second" }));
            Assert.That(submittedCover, Is.EqualTo(SecondCover));
            Assert.That(otherCoverSelected, Is.False);
            Assert.That(window!.FindControl<OptionButton>("CoverSelector").ItemCount, Is.EqualTo(2));
            Assert.That(book.Pages, Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(book.SelectedCover, Is.EqualTo(FirstCover));
            Assert.That(window.IsEditing, Is.True);
        });
        await client.WaitPost(() =>
        {
            book.Pages = submitted!;
            book.SelectedCover = submittedCover;
            window!.PopulatePages(book.Pages);
            window.UpdateEditor(book, false);
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(window!.IsEditing, Is.False);
            Assert.That(GetPageText(window, "LeftPageContent"), Is.EqualTo("Edited first"));
            Assert.That(GetPageText(window, "RightPageContent"), Is.EqualTo("Edited second"));
        });
        await client.WaitPost(() => window!.Dispose());
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AddingPagesStopsAtServerLimitAndRetainsThePreviousPage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var client = pair.Client;
        BookWindow? window = null;
        bool added = false;
        bool pastLimit = true;
        List<string>? submitted = null;
        await client.WaitPost(() =>
        {
            var book = CreateEditableBook();
            book.MaxPages = 3;
            window = new BookWindow();
            window.OnSaved += (pages, _) => submitted = pages;
            window.PopulatePages(book.Pages);
            window.UpdateEditor(book, true);
            window.FindControl<TextEdit>("PageEditor").TextRope = new Rope.Leaf("Before adding");
            added = window.TryAddPage();
            window.FindControl<TextEdit>("PageEditor").TextRope = new Rope.Leaf("Third");
            pastLimit = window.TryAddPage();
            window.TrySave();
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(added, Is.True);
            Assert.That(pastLimit, Is.False);
            Assert.That(window!.SelectedPageIndex, Is.EqualTo(2));
            Assert.That(window.FindControl<Button>("AddPageButton").Disabled, Is.True);
            Assert.That(submitted, Is.EqualTo(new[] { "Before adding", "Second", "Third" }));
        });
        await client.WaitPost(() => window!.Dispose());
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ActiveDraftSurvivesUpdatesAndCancelRestoresLatestServerContent()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var client = pair.Client;
        var book = CreateEditableBook();
        BookWindow? window = null;
        int saves = 0;
        await client.WaitPost(() =>
        {
            window = new BookWindow();
            window.OnSaved += (_, _) => saves++;
            window.PopulatePages(book.Pages);
            window.UpdateEditor(book, true);
            window.FindControl<TextEdit>("PageEditor").TextRope = new Rope.Leaf("Unsaved draft");
            window.TrySelectCover(SecondCover);
            book.Pages = ["New server text", "Other server page"];
            window.PopulatePages(book.Pages);
            window.UpdateEditor(book, true);
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(Rope.Collapse(window!.FindControl<TextEdit>("PageEditor").TextRope), Is.EqualTo("Unsaved draft"));
            Assert.That(window.FindControl<OptionButton>("CoverSelector").SelectedId, Is.EqualTo(1));
        });
        await client.WaitPost(() =>
        {
            window!.CancelEditing();
            window.UpdateEditor(book, true);
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(saves, Is.Zero);
            Assert.That(window!.IsEditing, Is.False);
            Assert.That(GetPageText(window, "LeftPageContent"), Is.EqualTo("New server text"));
            Assert.That(book.SelectedCover, Is.EqualTo(FirstCover));
        });
        await client.WaitPost(() => window!.TryBeginEditing());
        await client.WaitAssertion(() =>
        {
            Assert.That(Rope.Collapse(window!.FindControl<TextEdit>("PageEditor").TextRope), Is.EqualTo("New server text"));
            Assert.That(window.FindControl<OptionButton>("CoverSelector").SelectedId, Is.Zero);
        });
        await client.WaitPost(() => window!.Dispose());
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SaveValidatesAggregateTextLimitAcrossAllPages()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var client = pair.Client;
        BookWindow? window = null;
        bool oversizedSaved = true;
        bool correctedSaved = false;
        await client.WaitPost(() =>
        {
            var book = CreateEditableBook();
            book.Pages = ["aa", "bb"];
            book.ContentSize = 4;
            window = new BookWindow();
            window.PopulatePages(book.Pages);
            window.UpdateEditor(book, true);
            window.FindControl<TextEdit>("PageEditor").TextRope = new Rope.Leaf("aaa");
            oversizedSaved = window.TrySave();
            window.FindControl<TextEdit>("PageEditor").TextRope = new Rope.Leaf("aa");
            correctedSaved = window.TrySave();
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(oversizedSaved, Is.False);
            Assert.That(correctedSaved, Is.True);
        });
        await client.WaitPost(() => window!.Dispose());
        await pair.CleanReturnAsync();
    }

    private static BookComponent CreateEditableBook()
    {
        return new BookComponent
        {
            Editable = true,
            Pages = ["First", "Second"],
            AvailableCovers = [FirstCover, SecondCover],
            SelectedCover = FirstCover,
        };
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public async Task PairsPagesAndStopsNavigationAtBothBoundaries(int pageCount)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var client = pair.Client;
        BookWindow? window = null;
        var pages = Enumerable.Range(0, pageCount).Select(index => $"Page {index}").ToArray();
        await client.WaitPost(() =>
        {
            window = new BookWindow();
            window.PopulatePages(pages);
        });

        var spreadCount = Math.Max(1, (pageCount + 1) / 2);
        for (var spread = 0; spread < spreadCount; spread++)
        {
            var current = spread;
            await client.WaitAssertion(() =>
            {
                Assert.That(window!.SpreadIndex, Is.EqualTo(current));
                Assert.That(window.SpreadCount, Is.EqualTo(spreadCount));
                Assert.That(window.FindControl<Button>("PreviousButton").Disabled, Is.EqualTo(current == 0));
                Assert.That(window.FindControl<Button>("NextButton").Disabled, Is.EqualTo(current == spreadCount - 1));
                Assert.That(GetPageText(window, "LeftPageContent"),
                    Is.EqualTo(current * 2 < pageCount ? pages[current * 2] : ""));
                Assert.That(GetPageText(window, "RightPageContent"),
                    Is.EqualTo(current * 2 + 1 < pageCount ? pages[current * 2 + 1] : ""));
            });
            if (spread + 1 < spreadCount)
                await client.WaitPost(() => window!.TryNextSpread());
        }

        bool pastEnd = true;
        bool beforeStart = true;
        await client.WaitPost(() =>
        {
            pastEnd = window!.TryNextSpread();
            while (window.TryPreviousSpread())
            {
            }
            beforeStart = window.TryPreviousSpread();
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(pastEnd, Is.False);
            Assert.That(beforeStart, Is.False);
            Assert.That(window!.SpreadIndex, Is.Zero);
        });
        await client.WaitPost(() => window!.Dispose());
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmptyPagesRetainTheirPositionsAndReplacingContentClampsTheSpread()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var client = pair.Client;
        BookWindow? window = null;
        await client.WaitPost(() =>
        {
            window = new BookWindow();
            window.PopulatePages(["", "Second", ""]);
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(GetPageText(window!, "LeftPageContent"), Is.Empty);
            Assert.That(GetPageText(window!, "RightPageContent"), Is.EqualTo("Second"));
        });
        await client.WaitPost(() => window!.TryNextSpread());
        await client.WaitAssertion(() =>
        {
            Assert.That(window!.SpreadIndex, Is.EqualTo(1));
            Assert.That(GetPageText(window, "LeftPageContent"), Is.Empty);
            Assert.That(GetPageText(window, "RightPageContent"), Is.Empty);
        });
        await client.WaitPost(() => window!.PopulatePages(["Replacement"]));
        await client.WaitAssertion(() =>
        {
            Assert.That(window!.SpreadIndex, Is.Zero);
            Assert.That(GetPageText(window, "LeftPageContent"), Is.EqualTo("Replacement"));
            Assert.That(GetPageText(window, "RightPageContent"), Is.Empty);
            Assert.That(window.FindControl<Button>("NextButton").Disabled, Is.True);
        });
        await client.WaitPost(() => window!.Dispose());
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task XmlTextStaysLiteralAndPageErrorsDoNotHideTheOtherPage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var client = pair.Client;
        BookWindow? window = null;
        var parsed = new BookXmlParser(_ => true).Parse("""
            <book><locale id="en-US">
              <page><unknown /></page>
              <page><p>[color=red]literal[/color] <b>bold</b></p></page>
            </locale></book>
            """, "en-US");
        await client.WaitPost(() =>
        {
            window = new BookWindow();
            window.Populate(parsed);
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(window!.FindControl<RichTextLabel>("LeftPageError").Visible, Is.True);
            Assert.That(window.FindControl<RichTextLabel>("RightPageError").Visible, Is.False);
            Assert.That(GetPageText(window, "RightPageContent"), Is.EqualTo("[color=red]literal[/color] bold"));
            var label = window.FindControl<BoxContainer>("RightPageContent").Children.OfType<RichTextLabel>().Single();
            Assert.That(label.Nodes.Any(node => node.Name == "color"), Is.False);
            Assert.That(label.Nodes.Any(node => node.Name == "bold"), Is.True);
        });
        await client.WaitPost(() => window!.Dispose());
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MissingBookResourceShowsContentErrorAndClearsPreviousPages()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var client = pair.Client;
        BookWindow? window = null;
        await client.WaitPost(() =>
        {
            window = new BookWindow();
            window.PopulatePages(["Old content", "Second page", "Last page"]);
            window.TryNextSpread();
            var resources = client.ResolveDependency<IResourceCache>();
            var parsed = new BookXmlParser(resources).ParseResource(resources,
                "/Books/_Lust/missing-book-window-test-resource.xml", "en-US");
            window.Populate(parsed);
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(window!.FindControl<RichTextLabel>("ContentError").Visible, Is.True);
            Assert.That(window.SpreadIndex, Is.Zero);
            Assert.That(GetPageText(window, "LeftPageContent"), Is.Empty);
            Assert.That(GetPageText(window, "RightPageContent"), Is.Empty);
            Assert.That(window.FindControl<Button>("PreviousButton").Disabled, Is.True);
            Assert.That(window.FindControl<Button>("NextButton").Disabled, Is.True);
        });
        await client.WaitPost(() => window!.Dispose());
        await pair.CleanReturnAsync();
    }

    private static string GetPageText(BookWindow window, string name)
    {
        var text = new List<string>();
        foreach (var label in window.FindControl<BoxContainer>(name).Children.OfType<RichTextLabel>())
            text.Add(label.GetFormattedMessage()?.ToString() ?? "");
        return string.Join("\n", text);
    }
}

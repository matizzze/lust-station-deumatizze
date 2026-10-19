#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server._Lust.Book;
using Content.Shared._Lust.Book;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Item;
using Content.Shared.Paper;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.IntegrationTests.Tests._Lust.Books;

[TestFixture]
public sealed class BookSystemTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: bookCover
  id: LustTestBookCoverFirst
  name: book-test-first
  coverState: cover_base
- type: bookCover
  id: LustTestBookCoverSecond
  name: book-test-second
  coverState: cover_strong
- type: bookCover
  id: LustTestBookCoverOther
  name: book-test-other
  coverState: cover_old
- type: entity
  id: LustTestRandomStoryPaper
  parent: Paper
  components:
  - type: PaperRandomStory
    template: GenericStory
- type: entity
  id: LustTestEditableBook
  components:
  - type: Appearance
  - type: Book
    editable: true
    availableCovers: [LustTestBookCoverFirst, LustTestBookCoverSecond]
    selectedCover: LustTestBookCoverFirst
  - type: UserInterface
    interfaces:
      enum.BookUiKey.Key:
        type: PaperBoundUserInterface
  - type: ActivatableUI
    key: enum.BookUiKey.Key
""";

    private static readonly ProtoId<BookCoverPrototype> FirstCover = "LustTestBookCoverFirst";
    private static readonly ProtoId<BookCoverPrototype> SecondCover = "LustTestBookCoverSecond";
    private static readonly ProtoId<TagPrototype> WriteTag = "Write";

    [TestCase("BookRandomStory", true)]
    [TestCase("LustTestRandomStoryPaper", false)]
    public async Task RandomStoryPopulatesBookPagesOrOrdinaryPaper(string prototype, bool isBook)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid story = default;

        await server.WaitPost(() => story = entMan.SpawnEntity(prototype, map.GridCoords));

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<BookComponent>(story), Is.EqualTo(isBook));
            Assert.That(entMan.HasComponent<PaperComponent>(story), Is.EqualTo(!isBook));
            if (isBook)
            {
                var book = entMan.GetComponent<BookComponent>(story);
                Assert.That(book.Pages, Has.Count.EqualTo(2));
                Assert.That(string.IsNullOrWhiteSpace(book.Pages[0]), Is.False);
                Assert.That(book.Pages[1], Is.Empty);
                return;
            }

            Assert.That(string.IsNullOrWhiteSpace(entMan.GetComponent<PaperComponent>(story).Content), Is.False);
        });

        await server.WaitPost(() => entMan.System<SharedMapSystem>().DeleteMap(map.MapId));
        await pair.CleanReturnAsync();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task SaveMessageUsesAuthoritativeValidationAndPreservesOtherEditors(bool valid)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        Entity<BookComponent> book = default;
        EntityUid actor = default;
        EntityUid otherActor = default;
        BookBoundUserInterfaceState? state = null;
        await server.WaitPost(() =>
        {
            (book, actor, var pen, _) = Arrange(entMan, map.GridCoords);
            var interaction = new InteractUsingEvent(actor, pen, book, map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(book, interaction);
            (_, otherActor, var otherPen, _) = Arrange(entMan, map.GridCoords);
            var otherInteraction = new InteractUsingEvent(otherActor, otherPen, book, map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(book, otherInteraction);

            var cover = valid ? SecondCover : new ProtoId<BookCoverPrototype>("LustTestBookCoverOther");
            var message = new BookSaveMessage(["Saved first", "Saved second", "Saved third"], cover)
            {
                Actor = actor,
                UiKey = BookUiKey.Key,
            };
            entMan.EventBus.RaiseLocalEvent(book, message);
            entMan.System<SharedUserInterfaceSystem>().TryGetUiState(book.Owner, BookUiKey.Key, out state);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(book.Comp.Pages, Is.EqualTo(valid
                ? new[] { "Saved first", "Saved second", "Saved third" }
                : new[] { "", "" }));
            Assert.That(book.Comp.SelectedCover, Is.EqualTo(valid ? SecondCover : FirstCover));
            Assert.That(state, Is.Not.Null);
            Assert.That(state!.EditingActors.Contains(entMan.GetNetEntity(actor)), Is.EqualTo(!valid));
            Assert.That(state.EditingActors, Does.Contain(entMan.GetNetEntity(otherActor)));
        });
        await server.WaitPost(() => entMan.System<SharedMapSystem>().DeleteMap(map.MapId));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SavedCoverIsIncludedInBookAndAppearanceNetworkStates()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        IComponentState? bookState = null;
        IComponentState? appearanceState = null;
        string? initialCover = null;
        bool saved = false;

        await server.WaitPost(() =>
        {
            (var book, var actor, var pen, _) = Arrange(entMan, map.GridCoords);
            entMan.System<SharedAppearanceSystem>().TryGetData(book, BookVisuals.Cover, out initialCover);
            var interaction = new InteractUsingEvent(actor, pen, book, map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(book, interaction);
            saved = entMan.System<BookSystem>().TrySave(book, actor, ["Updated"], SecondCover);
            bookState = entMan.GetComponentState(entMan.EventBus, book.Comp, null, default);
            appearanceState = entMan.GetComponentState(entMan.EventBus,
                entMan.GetComponent<AppearanceComponent>(book), null, default);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(initialCover, Is.EqualTo(FirstCover.Id));
            Assert.That(saved, Is.True);
            Assert.That(bookState, Is.TypeOf<BookComponent.BookComponent_AutoState>());
            Assert.That(((BookComponent.BookComponent_AutoState) bookState!).SelectedCover, Is.EqualTo(SecondCover));
            Assert.That(appearanceState, Is.TypeOf<AppearanceComponentState>());
            Assert.That(((AppearanceComponentState) appearanceState!).Data[BookVisuals.Cover], Is.EqualTo(SecondCover.Id));
        });

        await server.WaitPost(() => entMan.System<SharedMapSystem>().DeleteMap(map.MapId));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SavesAllPagesAndListedCover()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        Entity<BookComponent> book = default;
        bool pickedUp = false;
        bool handled = false;
        bool saved = false;
        var pages = new List<string> { "First page", "Second page", "Last page" };

        await server.WaitPost(() =>
        {
            (book, var actor, var pen, pickedUp) = Arrange(entMan, map.GridCoords);
            var interaction = new InteractUsingEvent(actor, pen, book, map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(book, interaction);
            handled = interaction.Handled;
            saved = entMan.System<BookSystem>().TrySave(book, actor, pages, SecondCover);
            pages[0] = "Changed after save";
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(pickedUp, Is.True);
            Assert.That(handled, Is.True);
            Assert.That(saved, Is.True);
            Assert.That(book.Comp.Pages, Is.EqualTo(new[] { "First page", "Second page", "Last page" }));
            Assert.That(book.Comp.SelectedCover, Is.EqualTo(SecondCover));
        });
        await server.WaitPost(() => entMan.System<SharedMapSystem>().DeleteMap(map.MapId));
        await pair.CleanReturnAsync();
    }

    [TestCase("tool")]
    [TestCase("actor")]
    [TestCase("pages")]
    [TestCase("text")]
    [TestCase("cover")]
    [TestCase("readonly")]
    [TestCase("distance")]
    [TestCase("closed")]
    [TestCase("blocked")]
    [TestCase("nullpage")]
    public async Task RejectedSavePreservesState(string rejection)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        Entity<BookComponent> book = default;
        bool saved = true;
        bool pickedUp = false;
        bool editorOpened = false;

        await server.WaitPost(() =>
        {
            (book, var actor, var pen, pickedUp) = Arrange(entMan, map.GridCoords);
            var interaction = new InteractUsingEvent(actor, pen, book, map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(book, interaction);
            editorOpened = entMan.System<SharedUserInterfaceSystem>().IsUiOpen(book.Owner, BookUiKey.Key, actor);
            var pages = new List<string> { "Updated" };
            var cover = SecondCover;
            switch (rejection)
            {
                case "tool":
                    entMan.System<TagSystem>().RemoveTag(pen, WriteTag);
                    break;
                case "actor":
                    (_, actor, _, _) = Arrange(entMan, map.GridCoords);
                    entMan.System<SharedUserInterfaceSystem>().OpenUi(book.Owner, BookUiKey.Key, actor);
                    break;
                case "pages":
                    pages = Enumerable.Repeat("", 33).ToList();
                    break;
                case "text":
                    pages = [new string('a', 7500), new string('b', 7501)];
                    break;
                case "cover":
                    cover = "LustTestBookCoverOther";
                    break;
                case "readonly":
                    book.Comp.Editable = false;
                    break;
                case "distance":
                    entMan.System<SharedTransformSystem>().SetCoordinates(actor, new EntityCoordinates(map.GridCoords.EntityId, new Vector2(20, 20)));
                    break;
                case "closed":
                    entMan.System<SharedUserInterfaceSystem>().CloseUi(book.Owner, BookUiKey.Key, actor);
                    break;
                case "blocked":
                    entMan.AddComponent<BlockWritingComponent>(actor);
                    break;
                case "nullpage":
                    pages = [null!];
                    break;
            }

            saved = entMan.System<BookSystem>().TrySave(book, actor, pages, cover);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(pickedUp, Is.True);
            Assert.That(editorOpened, Is.True);
            Assert.That(saved, Is.False);
            Assert.That(book.Comp.Pages, Is.EqualTo(new[] { "", "" }));
            Assert.That(book.Comp.SelectedCover, Is.EqualTo(FirstCover));
            Assert.That(entMan.System<SharedAppearanceSystem>().TryGetData<string>(book, BookVisuals.Cover, out var cover), Is.True);
            Assert.That(cover, Is.EqualTo(FirstCover.Id));
        });
        await server.WaitPost(() => entMan.System<SharedMapSystem>().DeleteMap(map.MapId));
        await pair.CleanReturnAsync();
    }

    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public async Task PenInteractionRequiresEditableBookAndWriteTool(bool editable, bool writeTool, bool blocked)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        bool open = true;
        await server.WaitPost(() =>
        {
            (var book, var actor, var pen, _) = Arrange(entMan, map.GridCoords);
            book.Comp.Editable = editable;
            if (!writeTool)
                entMan.System<TagSystem>().RemoveTag(pen, WriteTag);
            if (blocked)
                entMan.AddComponent<BlockWritingComponent>(actor);
            var interaction = new InteractUsingEvent(actor, pen, book, map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(book, interaction);
            open = entMan.System<SharedUserInterfaceSystem>().IsUiOpen(book.Owner, BookUiKey.Key, actor);
        });
        await server.WaitAssertion(() => Assert.That(open, Is.False));
        await server.WaitPost(() => entMan.System<SharedMapSystem>().DeleteMap(map.MapId));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DefaultActivationRevokesOnlyThatActorsEditorMode()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        Entity<BookComponent> book = default;
        EntityUid actor = default;
        EntityUid otherActor = default;
        BookBoundUserInterfaceState? state = null;
        await server.WaitPost(() =>
        {
            (book, actor, var pen, _) = Arrange(entMan, map.GridCoords);
            var interaction = new InteractUsingEvent(actor, pen, book, map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(book, interaction);
            (_, otherActor, var otherPen, _) = Arrange(entMan, map.GridCoords);
            var otherInteraction = new InteractUsingEvent(otherActor, otherPen, book, map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(book, otherInteraction);
            var beforeOpen = new BeforeActivatableUIOpenEvent(actor);
            entMan.EventBus.RaiseLocalEvent(book, beforeOpen);
            entMan.System<SharedUserInterfaceSystem>().TryGetUiState(book.Owner, BookUiKey.Key, out state);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(state, Is.Not.Null);
            Assert.That(state!.EditingActors, Does.Not.Contain(entMan.GetNetEntity(actor)));
            Assert.That(state.EditingActors, Does.Contain(entMan.GetNetEntity(otherActor)));
            Assert.That(book.Comp.Pages, Is.EqualTo(new[] { "", "" }));
        });
        await server.WaitPost(() => entMan.System<SharedMapSystem>().DeleteMap(map.MapId));
        await pair.CleanReturnAsync();
    }

    private static (Entity<BookComponent> Book, EntityUid Actor, EntityUid Pen, bool PickedUp) Arrange(IEntityManager entMan, EntityCoordinates coordinates)
    {
        var book = entMan.SpawnEntity("LustTestEditableBook", coordinates);
        var actor = entMan.SpawnEntity(null, coordinates);
        entMan.EnsureComponent<ComplexInteractionComponent>(actor);
        entMan.EnsureComponent<HandsComponent>(actor);
        var hands = entMan.System<SharedHandsSystem>();
        hands.AddHand(actor, "hand", HandLocation.Left);
        var pen = entMan.SpawnEntity(null, coordinates);
        entMan.AddComponent<ItemComponent>(pen);
        entMan.System<TagSystem>().AddTag(pen, WriteTag);
        var pickedUp = hands.TryPickup(actor, pen);
        return ((book, entMan.GetComponent<BookComponent>(book)), actor, pen, pickedUp);
    }
}

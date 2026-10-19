using Content.Shared._Lust.Book;
using Content.Shared.ActionBlocker;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Server._Lust.Book;

public sealed partial class BookSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedUserInterfaceSystem _userInterface = default!;

    private static readonly ProtoId<TagPrototype> WriteTag = "Write";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BookComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BookComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<BookComponent, InteractUsingEvent>(OnInteractUsing);
        Subs.BuiEvents<BookComponent>(BookUiKey.Key, subs =>
        {
            subs.Event<BookSaveMessage>(OnSave);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
        });
    }

    private void OnStartup(Entity<BookComponent> book, ref ComponentStartup args)
    {
        UpdateAppearance(book);
    }

    private void OnBeforeUiOpen(Entity<BookComponent> book, ref BeforeActivatableUIOpenEvent args)
    {
        book.Comp.EditingActors.Remove(args.User);
        UpdateUserInterface(book);
    }

    private void OnInteractUsing(Entity<BookComponent> book, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryOpenEditor(book, args.User, args.Used))
            args.Handled = true;
    }

    private void OnSave(Entity<BookComponent> book, ref BookSaveMessage args)
    {
        TrySave(book, args.Actor, args.Pages, args.Cover);
    }

    private void OnUiClosed(Entity<BookComponent> book, ref BoundUIClosedEvent args)
    {
        book.Comp.EditingActors.Remove(args.Actor);
        UpdateUserInterface(book);
    }

    /// <summary>Opens editor mode for an actor currently holding the specified writing tool.</summary>
    public bool TryOpenEditor(Entity<BookComponent> book, EntityUid actor, EntityUid tool)
    {
        if (!CanOpenEditor(book, actor, tool))
            return false;

        var attempt = new PaperWriteAttemptEvent(book);
        RaiseLocalEvent(actor, ref attempt);
        if (attempt.Cancelled)
            return false;

        if (!_userInterface.TryOpenUi(book.Owner, BookUiKey.Key, actor))
            return false;

        book.Comp.EditingActors.Add(actor);
        UpdateUserInterface(book);
        return true;
    }

    /// <summary>Checks whether the book, actor and active tool permit opening the editor.</summary>
    public bool CanOpenEditor(Entity<BookComponent> book, EntityUid actor, EntityUid tool)
    {
        if (Deleted(book) || Deleted(actor) || !book.Comp.Editable)
            return false;

        if (_hands.GetActiveItem(actor) != tool || !_tag.HasTag(tool, WriteTag))
            return false;

        return _actionBlocker.CanInteract(actor, book) && _interaction.InRangeAndAccessible(actor, book.Owner);
    }

    /// <summary>
    /// Saves a complete page list and listed cover after revalidating the actor's editor access.
    /// Rejected saves leave book content and editor state unchanged.
    /// </summary>
    public bool TrySave(Entity<BookComponent> book, EntityUid actor, List<string> pages, ProtoId<BookCoverPrototype> cover)
    {
        if (!CanSave(book, actor, pages, cover))
            return false;

        var attempt = new PaperWriteAttemptEvent(book);
        RaiseLocalEvent(actor, ref attempt);
        if (attempt.Cancelled)
            return false;

        book.Comp.Pages = new List<string>(pages);
        book.Comp.SelectedCover = cover;
        book.Comp.EditingActors.Remove(actor);
        Dirty(book);
        UpdateAppearance(book);
        UpdateUserInterface(book);
        return true;
    }

    /// <summary>Validates editor access, aggregate content limits and the selected cover.</summary>
    public bool CanSave(Entity<BookComponent> book, EntityUid actor, List<string> pages, ProtoId<BookCoverPrototype> cover)
    {
        if (Deleted(book) || Deleted(actor) || !book.Comp.Editable)
            return false;

        if (!book.Comp.EditingActors.Contains(actor) || !_userInterface.IsUiOpen(book.Owner, BookUiKey.Key, actor))
            return false;

        if (pages == null || pages.Count > book.Comp.MaxPages || book.Comp.ContentSize < 0)
            return false;

        if (!book.Comp.AvailableCovers.Contains(cover) || string.IsNullOrEmpty(cover.Id) || !_prototype.HasIndex(cover))
            return false;

        var remaining = book.Comp.ContentSize;
        foreach (var page in pages)
        {
            if (page == null || page.Length > remaining)
                return false;

            remaining -= page.Length;
        }

        return _actionBlocker.CanInteract(actor, book) && _interaction.InRangeAndAccessible(actor, book.Owner);
    }

    private void UpdateAppearance(Entity<BookComponent> book)
    {
        if (book.Comp.SelectedCover is not { } cover)
            return;

        _appearance.SetData(book, BookVisuals.Cover, cover.Id);
    }

    private void UpdateUserInterface(Entity<BookComponent> book)
    {
        _userInterface.SetUiState(book.Owner, BookUiKey.Key,
            new BookBoundUserInterfaceState(GetNetEntitySet(book.Comp.EditingActors)));
    }
}

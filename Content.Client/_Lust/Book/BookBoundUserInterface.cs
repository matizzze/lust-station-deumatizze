using Content.Shared._Lust.Book;
using JetBrains.Annotations;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Lust.Book;

/// <summary>Loads static XML or dynamic component pages into the book reader.</summary>
[UsedImplicitly]
public sealed class BookBoundUserInterface : BoundUserInterface
{
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private ILocalizationManager _loc = default!;

    private BookWindow? _window;

    public BookBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BookWindow>();
        _window.OnSaved += OnSaved;
        Update();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is BookBoundUserInterfaceState)
            Update();
    }

    public override void Update()
    {
        base.Update();
        if (_window == null || !EntMan.TryGetComponent<BookComponent>(Owner, out var book))
            return;

        if (EntMan.TryGetComponent<MetaDataComponent>(Owner, out var metadata))
            _window.Title = metadata.EntityName;

        if (book.ContentPath == null)
        {
            _window.PopulatePages(book.Pages);
            UpdateEditor(book);
            return;
        }

        var locale = _loc.DefaultCulture?.Name ?? "en-US";
        var content = new BookXmlParser(_resourceCache).ParseResource(_resourceCache, book.ContentPath, locale);
        if (content.Error != null)
            LogError(book.ContentPath, content.Error);

        foreach (var page in content.Pages)
        {
            if (page.Error != null)
                LogError(book.ContentPath, page.Error);
        }

        _window.Populate(content);
        UpdateEditor(book);
    }

    private void UpdateEditor(BookComponent book)
    {
        var authorized = PlayerManager.LocalEntity is { } actor && State is BookBoundUserInterfaceState state &&
                         state.EditingActors.Contains(EntMan.GetNetEntity(actor));
        _window?.UpdateEditor(book, authorized);
    }

    private void OnSaved(List<string> pages, ProtoId<BookCoverPrototype> cover)
    {
        SendMessage(new BookSaveMessage(pages, cover));
    }

    private static void LogError(string path, BookContentError error)
    {
        Logger.WarningS("book", $"Book '{path}', locale '{error.Locale}', page {error.PageIndex}: {error.Kind}: {error.Details}");
    }
}

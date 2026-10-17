using Content.Shared._Lust.Book;
using Robust.Shared.GameStates;

namespace Content.Client._Lust.Book;

public sealed partial class BookSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _userInterface = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BookComponent, AfterAutoHandleStateEvent>(OnBookState);
    }

    private void OnBookState(Entity<BookComponent> book, ref AfterAutoHandleStateEvent args)
    {
        // Страницы и состояние BUI могут применяться в разном порядке в одном сетевом обновлении.
        if (_userInterface.TryGetOpenUi(book.Owner, BookUiKey.Key, out var bui))
            bui.Update();
    }
}

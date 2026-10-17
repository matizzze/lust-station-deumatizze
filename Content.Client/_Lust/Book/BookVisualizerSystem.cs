using Content.Shared._Lust.Book;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Lust.Book;

public sealed partial class BookVisualizerSystem : VisualizerSystem<BookComponent>
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private const string CoverLayer = "cover";

    protected override void OnAppearanceChange(EntityUid uid, BookComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !SpriteSystem.LayerMapTryGet((uid, args.Sprite), CoverLayer, out var layer, false))
        {
            return;
        }

        if (!AppearanceSystem.TryGetData<string>(uid, BookVisuals.Cover, out var coverId, args.Component) ||
            !_prototype.TryIndex<BookCoverPrototype>(coverId, out var cover))
        {
            return;
        }

        SpriteSystem.LayerSetRsiState((uid, args.Sprite), layer, cover.CoverState);
        SpriteSystem.LayerSetColor((uid, args.Sprite), layer, cover.CoverColor);
    }
}

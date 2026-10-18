using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Lust.Book;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BookComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? ContentPath;

    [DataField, AutoNetworkedField]
    public List<string> Pages = ["", ""];

    [DataField, AutoNetworkedField]
    public List<ProtoId<BookCoverPrototype>> AvailableCovers = [];

    [DataField, AutoNetworkedField]
    public ProtoId<BookCoverPrototype>? SelectedCover;

    [DataField, AutoNetworkedField]
    public bool Editable;

    [DataField, AutoNetworkedField]
    public int MaxPages = 32;

    [DataField, AutoNetworkedField]
    public int ContentSize = 15000;

    [NonSerialized]
    public readonly HashSet<EntityUid> EditingActors = [];
}

[Serializable, NetSerializable]
public enum BookUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class BookBoundUserInterfaceState(HashSet<NetEntity> editingActors) : BoundUserInterfaceState
{
    public readonly HashSet<NetEntity> EditingActors = editingActors;
}

[Serializable, NetSerializable]
public sealed class BookSaveMessage(List<string> pages, ProtoId<BookCoverPrototype> cover) : BoundUserInterfaceMessage
{
    public readonly List<string> Pages = pages;
    public readonly ProtoId<BookCoverPrototype> Cover = cover;
}

[Serializable, NetSerializable]
public enum BookVisuals : byte
{
    Cover,
}

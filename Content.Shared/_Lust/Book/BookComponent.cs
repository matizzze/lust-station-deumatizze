using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Lust.Book;

/// <summary>Content and editing limits of a book, independent of ordinary paper.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BookComponent : Component
{
    /// <summary>Game resource containing localized static XML pages.</summary>
    [DataField, AutoNetworkedField]
    public string? ContentPath;

    /// <summary>Pages of an editable or dynamically generated book.</summary>
    [DataField, AutoNetworkedField]
    public List<string> Pages = new() { "", "" };

    /// <summary>Cover choices permitted by the book prototype.</summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<BookCoverPrototype>> AvailableCovers = [];

    /// <summary>Currently selected cover, if the book supports cover selection.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<BookCoverPrototype>? SelectedCover;

    /// <summary>Whether players may edit this book using a writing tool.</summary>
    [DataField, AutoNetworkedField]
    public bool Editable;

    /// <summary>Maximum number of submitted pages.</summary>
    [DataField, AutoNetworkedField]
    public int MaxPages = 32;

    /// <summary>Maximum total number of UTF-16 characters across all pages.</summary>
    [DataField, AutoNetworkedField]
    public int ContentSize = 15000;

    /// <summary>Server-authorized editors for the currently open interfaces.</summary>
    [NonSerialized]
    public readonly HashSet<EntityUid> EditingActors = [];
}

[Serializable, NetSerializable]
public enum BookUiKey : byte
{
    Key,
}

/// <summary>Per-actor editor authorization. Content and limits are read from BookComponent.</summary>
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

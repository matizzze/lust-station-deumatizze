using Robust.Shared.Prototypes;

namespace Content.Shared._Lust.Book;

[Prototype]
public sealed partial class BookCoverPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = null!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public string CoverState = null!;

    [DataField]
    public Color CoverColor = Color.White;
}

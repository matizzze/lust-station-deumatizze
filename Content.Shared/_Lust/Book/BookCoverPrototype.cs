using Robust.Shared.Prototypes;

namespace Content.Shared._Lust.Book;

/// <summary>Localized cover choice and its sprite appearance.</summary>
[Prototype]
public sealed partial class BookCoverPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Localization key displayed by the cover picker.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>State from the book's RSI used for the cover layer.</summary>
    [DataField(required: true)]
    public string CoverState = default!;

    /// <summary>Tint applied to the cover layer.</summary>
    [DataField]
    public Color CoverColor = Color.White;
}

using Content.Shared._Lust.Book; // Lust edit - Документация учитывает книжные страницы.
using Content.Shared.Paper;
using Content.Shared.StoryGen;
using Robust.Shared.Prototypes;

namespace Content.Server.Paper;

// Lust edit - Истории поддерживают книги без PaperComponent.
/// <summary>
/// Adds a randomly generated story to the first page of a <see cref="BookComponent"/>
/// or the content of a <see cref="PaperComponent"/> when the entity is not a book.
/// </summary>
[RegisterComponent, Access(typeof(PaperRandomStorySystem))]
public sealed partial class PaperRandomStoryComponent : Component
{
    /// <summary>
    /// The <see cref="StoryTemplatePrototype"/> ID to use for story generation.
    /// </summary>
    [DataField]
    public ProtoId<StoryTemplatePrototype> Template;
}

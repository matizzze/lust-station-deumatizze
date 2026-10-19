using Content.Shared._Lust.Book; // Lust edit - Книги хранят сгенерированный текст в собственных страницах.
using Content.Shared.Paper;
using Content.Shared.StoryGen;

namespace Content.Server.Paper;

public sealed partial class PaperRandomStorySystem : EntitySystem
{
    [Dependency] private StoryGeneratorSystem _storyGen = default!;
    [Dependency] private PaperSystem _paper = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperRandomStoryComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<PaperRandomStoryComponent> paperStory, ref MapInitEvent ev)
    {
        // Lust edit start - Истории поддерживают книжные страницы и обычную бумагу.
        if (!TryComp<BookComponent>(paperStory, out var book) && !HasComp<PaperComponent>(paperStory))
            return;
        // Lust edit end

        if (!_storyGen.TryGenerateStoryFromTemplate(paperStory.Comp.Template, out var story))
            return;

        // Lust edit start - Первая страница получает историю, остальные страницы сохраняются.
        if (book != null)
        {
            if (book.Pages.Count == 0)
                book.Pages.Add(story);
            else
                book.Pages[0] = story;

            Dirty(paperStory, book);
            return;
        }

        if (TryComp<PaperComponent>(paperStory, out var paper))
            _paper.SetContent((paperStory.Owner, paper), story);
        // Lust edit end
    }
}

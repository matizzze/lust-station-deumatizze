# Book System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a two-page-spread book reader, XML-backed static books with localized formatted content and images, and an in-game editable book with configurable cover choices.

**Architecture:** Add a Lust book component and server-authoritative system alongside the existing paper system. A client BUI parses trusted game XML resources and renders paired pages, while editable pages and selected cover are stored in the networked component. Migrate static book prototypes and adapt random story generation without changing ordinary paper or guidebooks.

**Tech Stack:** C#, RobustToolbox ECS and BUI, XAML, XML resources, YAML prototypes, Fluent localization, NUnit integration tests.

**Spec:** `docs/superpowers/specs/2026-10-18-book-system-design.md`

## Global Constraints

- Two pages are visible at once; next and previous change the spread without animation.
- Ready-book content is authored in XML and includes RU and EN translations.
- The editable book lets a player edit pages and choose among prototype-provided covers.
- Book interaction uses a separate `BookComponent`, server system, and UI; ordinary paper remains on `PaperComponent` and `PaperBoundUserInterface`.
- Base book prototypes do not inherit `PaperComponent`; `BookSystem` handles pen interaction and saved text.
- Accept only known XML elements and game resource paths; reject DTD, external entities, arbitrary UI markup, and external image URLs.
- Validate page count, total text size, write tool, interaction, and selected cover on the server.
- Use Lust naming and `Lust edit` markers for minimal changes to upstream files; put new fork-specific files under `_Lust`.
- Check for Rider MCP before implementation; use its permitted equivalents when available and avoid its prohibited terminal operations.
- Run no more than two test commands concurrently; stop any client process started for verification.

## Review Focus

- XML DTD/external entities, unknown elements, malformed nesting, and non-game image paths must produce a page error without crashing the client. Cover in `BookXmlParserTest`.
- Missing `ru-RU` must select `en-US`; absent `en-US` or mismatched localized page counts must produce a content error. Cover in `BookXmlParserTest`.
- Empty books, a one-page final spread, and previous/next boundaries must render correctly. Cover in `BookWindowTest` and manual client verification.
- Forged editor messages, non-writing tools, oversized page lists/text, and unavailable covers must leave component data unchanged. Cover in `BookSystemTest`.
- `BookRandomStory` must still receive generated text after the base book loses `PaperComponent`. Cover in `BookSystemTest`.

---

### Task 1: Shared book contract and server-authoritative editing

**Files:**
- Create: `Content.Shared/_Lust/Book/BookComponent.cs`
- Create: `Content.Shared/_Lust/Book/BookCoverPrototype.cs`
- Create: `Content.Server/_Lust/Book/BookSystem.cs`
- Test: `Content.IntegrationTests/Tests/_Lust/Books/BookSystemTest.cs`

**Interfaces:**
- Produces: `BookComponent` with `ContentPath`, `Pages`, `AvailableCovers`, `SelectedCover`, `Editable`, `MaxPages`, and `ContentSize`.
- Produces: `BookCoverPrototype` metadata (`Name`, `CoverState`, `CoverColor`) and `BookVisuals.Cover` appearance key, without client visual behavior yet.
- Produces: `BookUiKey.Key`, `BookBoundUserInterfaceState`, and `BookSaveMessage(List<string> pages, ProtoId<BookCoverPrototype> cover)`.
- Produces: `BookSystem.TrySave(Entity<BookComponent> book, EntityUid actor, List<string> pages, ProtoId<BookCoverPrototype> cover) -> bool`.
- Consumes: `PaperWriteAttemptEvent` to respect existing writing restrictions; `TagSystem` to require the existing `Write` tool tag; `SharedInteractionSystem` to revalidate actor access when saving.
- Prototype defaults: `MaxPages = 32`, `ContentSize = 15000`; editable books start with two blank pages.

- [ ] **Step 1: Write integration tests** for rejecting non-writable tools, wrong actors, excess pages, text over `ContentSize`, and cover IDs outside `AvailableCovers`; assert state is unchanged on rejection. Add a success case that saves all pages and a listed cover.
- [ ] **Step 2: Run the focused test** with `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --configuration Debug --filter "FullyQualifiedName~BookSystemTest"`; confirm the new test fails because the book contract/system is not implemented.
- [ ] **Step 3: Implement the shared component and UI network contract** in `BookComponent.cs`, including serializable state and messages; implement server `BookSystem.TrySave` with the validation above and dirty the component only on success.
- [ ] **Step 4: Implement book pen interaction** in `BookSystem`: require an editable book and the `Write` tool tag, raise `PaperWriteAttemptEvent`, then open `BookUiKey.Key` in edit mode. Default activation opens read mode.
- [ ] **Step 5: Run `BookSystemTest`** and confirm valid edits persist while every invalid case leaves the book unchanged.
- [ ] **Step 6: Commit** the shared contract and server interaction.

### Task 2: Cover prototype data and replicated visuals

**Files:**
- Modify: `Content.Shared/_Lust/Book/BookCoverPrototype.cs`
- Modify: `Content.Server/_Lust/Book/BookSystem.cs`
- Create: `Content.Client/_Lust/Book/BookVisualizerSystem.cs`
- Modify: `Content.Shared/_Lust/Book/BookComponent.cs`
- Create: `Resources/Prototypes/_Lust/Entities/Objects/Misc/book_covers.yml`
- Test: `Content.IntegrationTests/Tests/_Lust/Books/BookSystemTest.cs`

**Interfaces:**
- Produces: `BookCoverPrototype` with a localized display name, cover RSI state, and color.
- Produces: `BookComponent.AvailableCovers` as a list of `ProtoId<BookCoverPrototype>` and `SelectedCover` as the networked selected prototype ID.
- Produces: the client visualizer resolves the selected prototype and updates the mapped `cover` sprite layer through `SpriteSystem`.

- [ ] **Step 1: Add a visual-state integration case** that sets a listed cover and verifies the selected cover ID is replicated in the book state.
- [ ] **Step 2: Run the focused test** and confirm the new assertion fails before the visual contract exists.
- [ ] **Step 3: Add the cover prototype and appearance contract**; configure the client visualizer to update only the `cover` layer using the selected prototype's RSI state and color.
- [ ] **Step 4: Add a small set of selectable cover prototypes** using existing `Objects/Misc/books.rsi` states; add default choices to the editable book prototype in the later prototype task.
- [ ] **Step 5: Run `BookSystemTest`** and confirm selecting a permitted cover updates the networked and appearance state.
- [ ] **Step 6: Commit** cover data and visuals.

### Task 3: Localized XML model and safe parser

**Files:**
- Create: `Content.Client/_Lust/Book/BookXmlParser.cs`
- Create: `Content.Client/_Lust/Book/BookPageData.cs`
- Create: `Content.Client/_Lust/Book/BookContentParseResult.cs`
- Test: `Content.IntegrationTests/Tests/_Lust/Books/BookXmlParserTest.cs`

**Interfaces:**
- Produces: `BookXmlParser.Parse(string xml, string locale) -> BookContentParseResult`, where the result contains either validated pages or a user-displayable parse error.
- Produces: `BookPageData` containing the validated page nodes needed by the view (paragraph, heading, bold, italic, color, and image resource path).
- Consumes: `BookComponent.ContentPath` and the active client locale from the existing localization manager.

- [ ] **Step 1: Write `BookXmlParserTest` cases** for a two-page bilingual document, RU selection, missing-RU EN fallback, malformed XML, missing EN, mismatched page counts, unknown tags/attributes, DTD/external entity input, non-game image paths, and missing game image resources.
- [ ] **Step 2: Run the focused parser test** with `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --configuration Debug --filter "FullyQualifiedName~BookXmlParserTest"`; confirm it fails before parser implementation.
- [ ] **Step 3: Implement `BookXmlParser.Parse`** with a DTD-disabled XML reader and an allowlist for `book`, `locale`, `page`, `p`, `h1`, `b`, `i`, `color`, and `image`; require `en-US`, select the requested locale or EN fallback, and require equal page counts when RU exists.
- [ ] **Step 4: Validate image sources** as content resource paths and produce an error on the owning page when the resource is missing or invalid.
- [ ] **Step 5: Run `BookXmlParserTest`** and confirm every valid document yields page data and every invalid document returns an error result without an unhandled exception.
- [ ] **Step 6: Commit** the parser and its tests.

### Task 4: Read-only spread UI

**Files:**
- Create: `Content.Client/_Lust/Book/BookBoundUserInterface.cs`
- Create: `Content.Client/_Lust/Book/BookWindow.xaml`
- Create: `Content.Client/_Lust/Book/BookWindow.xaml.cs`
- Modify: `Content.Client/_Lust/Book/BookXmlParser.cs`
- Test: `Content.IntegrationTests/Tests/_Lust/Books/BookWindowTest.cs`

**Interfaces:**
- Consumes: `BookBoundUserInterfaceState`, `BookPageData`, and `BookXmlParser.Parse`.
- Produces: a reader that displays spread `n` as pages `2n` and `2n + 1`, with local `Previous` and `Next` navigation.

- [ ] **Step 1: Write `BookWindowTest`** for page pairing, one-page/odd-page handling, empty page handling, and disabled previous/next controls at both boundaries.
- [ ] **Step 2: Run the focused UI test** and confirm it fails before the new window exists.
- [ ] **Step 3: Build `BookWindow.xaml`** with two page containers, navigation controls, spread number, and a page-level content error control; do not add page-turn animation.
- [ ] **Step 4: Implement `BookBoundUserInterface`** to select XML content for static books or component `Pages` for dynamic/editable books, then populate both pages of the current spread.
- [ ] **Step 5: Render validated XML formatting nodes** as text controls and resource images; render dynamic/editable page strings through the existing permissive paper RichText markup path.
- [ ] **Step 6: Run `BookWindowTest`** and confirm navigation and odd/empty spread behavior.
- [ ] **Step 7: Commit** the reader UI.

### Task 5: In-game page editor and cover picker

**Files:**
- Modify: `Content.Client/_Lust/Book/BookWindow.xaml`
- Modify: `Content.Client/_Lust/Book/BookWindow.xaml.cs`
- Modify: `Content.Client/_Lust/Book/BookBoundUserInterface.cs`
- Modify: `Content.IntegrationTests/Tests/_Lust/Books/BookWindowTest.cs`
- Modify: `Content.IntegrationTests/Tests/_Lust/Books/BookSystemTest.cs`

**Interfaces:**
- Consumes: `BookBoundUserInterfaceState`, `BookSaveMessage`, and the cover prototype list.
- Produces: edit mode with page selection, per-page text editing, add-page control, cover selection, save, and cancel.

- [ ] **Step 1: Add editor tests** for entering edit mode from a writable book, saving all pages with a valid cover, adding a page up to `MaxPages`, and preserving unsaved state when canceled.
- [ ] **Step 2: Run the focused editor tests** and confirm they fail before editor controls and message handling exist.
- [ ] **Step 3: Add editor controls to `BookWindow.xaml`** and populate only when server state says the book is editable and the actor opened it with a writing tool.
- [ ] **Step 4: Implement local page editing and cover selection** in `BookWindow.xaml.cs`; send one `BookSaveMessage` containing the complete page list and selected cover on save.
- [ ] **Step 5: Route save through `BookSystem.TrySave`** and return the authoritative state to the BUI; reject invalid input without mutating the book.
- [ ] **Step 6: Run `BookWindowTest` and `BookSystemTest`** and confirm edit, add, save, cancel, and validation behavior.
- [ ] **Step 7: Commit** the editor flow.

### Task 6: Migrate ready books and dynamic story generation

**Files:**
- Modify: `Resources/Prototypes/Entities/Objects/Misc/books.yml`
- Modify: `Resources/Prototypes/Entities/Objects/Misc/books_author.yml`
- Modify: `Resources/Prototypes/_Sunrise/Catalog/Fills/Books/rulebook.yml`
- Create: `Resources/Prototypes/_Lust/Entities/Objects/Misc/editable_book.yml`
- Create: `Resources/Books/_Lust/<book-id>.xml` for each migrated static book
- Modify: `Content.Server/Paper/PaperRandomStorySystem.cs`
- Modify: `Content.Server/Paper/PaperRandomStoryComponent.cs`
- Modify: `Resources/Locale/en-US/_strings/paper/book-*.ftl`
- Modify: `Resources/Locale/ru-RU/_strings/paper/book-*.ftl`
- Modify: `Resources/Locale/en-US/_strings/_sunrise/paper/book-*.ftl` when referenced by a migrated book
- Modify: `Resources/Locale/ru-RU/_strings/_sunrise/paper/book-*.ftl` when referenced by a migrated book
- Create: `Resources/Locale/en-US/_strings/_lust/books.ftl`
- Create: `Resources/Locale/ru-RU/_strings/_lust/books.ftl`
- Test: `Content.IntegrationTests/Tests/_Lust/Books/BookSystemTest.cs`

**Interfaces:**
- Consumes: `BookComponent.ContentPath`, `BookComponent.Pages`, `BookCoverPrototype`, `BookUiKey`, and `BookSystem` from Tasks 1–5.
- Produces: migrated static books using XML, one editable prototype with configured limits and cover options, and generated random stories in `BookComponent.Pages`.

- [ ] **Step 1: Add a random-story integration case** asserting `BookRandomStory` receives generated content in its first `BookComponent.Pages` entry without `PaperComponent`.
- [ ] **Step 2: Run `BookSystemTest`** and confirm the random-story case fails before adapting the existing system.
- [ ] **Step 3: Convert each FTL text referenced by a `BookBase` descendant** in the listed prototype files into RU/EN `<locale>` sections, preserving existing wording, markup semantics, and prototype-to-content mappings.
- [ ] **Step 4: Update `BookBase`** in `books.yml` to use `BookUiKey.Key` and `BookBoundUserInterface`; remove its `PaperComponent` while retaining sprite, item, and combat components. Add a `Lust edit` marker to this upstream change.
- [ ] **Step 5: Replace per-book `Paper.content` with `Book.contentPath`** and remove `PaperComponent` declarations from book descendants; leave `Catalog/Fills/Paper/manuals.yml` and guidebooks unchanged.
- [ ] **Step 6: Adapt `PaperRandomStorySystem` and its component documentation** to set generated text into page zero of `BookComponent` for `BookRandomStory`; retain paper behavior for any non-book entity that still uses `PaperRandomStoryComponent`.
- [ ] **Step 7: Add the editable book prototype** with `Editable: true`, two blank pages, `MaxPages: 32`, `ContentSize: 15000`, and a list of selectable cover prototype IDs.
- [ ] **Step 8: Remove obsolete FTL book-text keys** only after confirming no prototype or other content still references them.
- [ ] **Step 9: Run the focused book tests and YAML linter**; confirm migrated prototypes load, random stories populate, and paper manuals remain paper.
- [ ] **Step 10: Commit** the prototype, XML, and localization migration.

### Task 7: Full verification and integration review

**Files:**
- Verify: all files from Tasks 1–6.

- [ ] **Step 1: Build the affected projects sequentially** with `dotnet build Content.Shared/Content.Shared.csproj --configuration Debug`, `dotnet build Content.Server/Content.Server.csproj --configuration Debug`, `dotnet build Content.Client/Content.Client.csproj --configuration Debug`, and `dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj --configuration Debug`.
- [ ] **Step 2: Run the focused book integration tests** one at a time using `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --configuration Debug --filter "FullyQualifiedName~BookSystemTest"`, then `BookXmlParserTest`, then `BookWindowTest`.
- [ ] **Step 3: Run the YAML linter** with `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --no-build`; build that project in Release first if it is not already built.
- [ ] **Step 4: Run the client** with `dotnet run --project Content.Client/Content.Client.csproj` and check runtime and IL verification; stop the process before finishing.
- [ ] **Step 5: Manually verify** RU/EN content, images, formatting, multiple spreads, odd/empty page behavior, pen editing, page addition, cover changes, and invalid XML feedback.
- [ ] **Step 6: Inspect `git diff --check`, run the upstream edit-marker review, and confirm ordinary paper/guidebook prototypes retain their existing UI.**
- [ ] **Step 7: Commit** final verification fixes.

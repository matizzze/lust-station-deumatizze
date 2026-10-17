# Client startup log repair

## Diagnostic source and root causes

Source: `C:\Users\apolo\Desktop\buiild_log_client.log.txt`. Its contents were treated as diagnostic data.

The recorded client startup failure is an IL sandbox rejection of references in the book feature:

- `BookXmlParser` used `System.Xml` and `System.Xml.Linq` readers and DOM types that content assemblies cannot access.
- Calls to `List<T>.AsReadOnly()` introduced the forbidden `System.Collections.ObjectModel.ReadOnlyCollection<T>` type.
- Resource exception filters named the forbidden `System.UnauthorizedAccessException` type.
- The nonempty collection expression in `BookComponent.Pages` caused the compiler to generate a call to `System.Runtime.InteropServices.CollectionsMarshal.SetCount<string>`.

## Implementation

`Content.Client/_Lust/Book/BookXmlReader.cs` adds a forward-only tokenizer retaining the current token and an iterative stack of open element names. It does not construct a DOM, use regular expressions, load external resources, or resolve custom entities. A complete syntax pass precedes semantic parsing and image lookups, including for unselected locales.

The tokenizer validates matching element names, one complete root, quoted and distinct attributes, XML comments, optional XML 1.0 declarations, CDATA placement, XML characters, and entity syntax. DTD and entity declarations are rejected. Only the five predefined named entities and valid decimal or hexadecimal XML character references are decoded, without recursive expansion. Processing instructions remain unsupported content, allowing an instruction inside a page to produce a page-local error. Declarations and document syntax failures produce document errors.

`Content.Client/_Lust/Book/BookXmlParser.cs` now uses recursive descent for the approved book structure: `book`, locales and pages, `p`/`h1` blocks, nested `b`/`i`/`color`, and resource images. The public API, locale selection and English fallback, RU/EN page-count requirement, validation of unselected locales, page indices, and error kinds are retained. Unsupported page content is skipped iteratively so later pages remain available. The existing 2,000,000 character document limit and 32-level inline nesting limit remain in place.

Attributes are allowlisted per supported element. Image paths still require `/Textures/`, reject traversal, empty segments, URL syntax and control characters, and must exist in the mounted resources. Narrow resource operations catch `Exception` and convert failures to content or page errors without naming a forbidden exception type. Output collections are arrays implementing `IReadOnlyList<T>` directly.

`Content.Shared/_Lust/Book/BookComponent.cs` initializes the two empty editable pages with an explicit `List<string>` initializer, avoiding the collection-expression lowering reported in the log.

Existing parser tests were read as the behavior specification and preserved without modifications. Applicable repository rules, C# ECS skills, naming, upstream isolation and documentation guidance were read. No dedicated XML or XML-security skill was present in the available repository skill set. Rider MCP was unavailable.

## Check status and remaining concerns

No tests, builds, client runs, YAML linter, code reviews, formatter, diff checks, or other validation commands were run, as explicitly requested to avoid resource-intensive checks. No process listing was inspected, and no verification process was started. Commit hooks were disabled for the local commit.

This is an implementation addressing the specific references reported by the supplied log. Compilation, actual IL sandbox acceptance and runtime behavior remain unverified. The reader intentionally implements the approved book XML subset rather than a general XML processor.

## Files

- `Content.Client/_Lust/Book/BookXmlParser.cs`
- `Content.Client/_Lust/Book/BookXmlReader.cs`
- `Content.Shared/_Lust/Book/BookComponent.cs`
- `.superpowers/sdd/2026-10-18-book-system/fix-client-log-report.md`

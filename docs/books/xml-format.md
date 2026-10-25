# Static book XML format

Static books use a restricted XML format. Element and attribute names are case-sensitive and cannot use namespaces. Text is literal: a string such as `[bold]text[/bold]` is printed as written, rather than interpreted as RichText. Use XML escapes such as `&lt;` and `&amp;` for reserved characters.

## Document and locales

The document has one `<book>` root containing `<locale id="en-US">` and optionally other locales. Each locale contains `<page>` elements. The `en-US` locale is required and is the fallback for an unavailable locale. Locale IDs cannot repeat (case-insensitive). If `ru-RU` is present, its page count must equal the `en-US` page count. Empty pages are allowed. The document limit is 2,000,000 characters.

```xml
<book>
  <locale id="en-US">
    <page>
      <h1>A <i>short</i> guide</h1>
      <h2>Getting started</h2>
      <p>First line.<br/>Second line with <b>bold</b> text.</p>
      <hr/>
      <p><color value="#204060">A colored note.</color></p>
    </page>
  </locale>
</book>
```

## Elements and attributes

| Element | Allowed contents | Allowed attributes |
| --- | --- | --- |
| `book` | One or more `locale` elements, including `en-US` | None |
| `locale` | Zero or more `page` elements | Required nonempty `id` |
| `page` | Page blocks | None |
| `p`, `h1`–`h6` | Inline contents | None |
| `b`, `i` | Inline contents | None |
| `color` | Inline contents | Required nonempty `value`, accepted by the game color parser |
| `br` | Empty | None |
| `hr` | Empty | None |
| `image` | Empty | Required nonempty `src` |
| `table` | One or more `tr` elements | None |
| `tr` | One or more `th` or `td` elements | None |
| `th`, `td` | Inline contents | None |
| `layout` | One or more `row` elements | None |
| `row` | One or more `column` elements | None |
| `column` | Page blocks | None |

Page blocks are `p`, `h1`–`h6`, `image`, `hr`, `table`, and `layout`. Inline contents are literal text, `b`, `i`, `color`, and `br`; inline formatting can nest. A `br` creates a line break; an `hr` creates a horizontal separator. Headings pass their level to the existing RichText heading renderer. Levels 4–6 continue its size scale locally with a fixed bold font size: levels 1–6 use sizes 24, 17, 14, 12, 11, and 10 respectively. These sizes are renderer settings, not author-controlled attributes. Consecutive paragraphs and headings share a text control and are separated by newlines.

Only whitespace may appear between structural child elements. Paragraphs, headings, cells, and columns may be empty. Empty elements may use self-closing syntax or an explicit empty closing tag, for example `<br></br>`; even whitespace inside `br`, `hr`, or `image` is rejected. Unknown elements, attributes, or invalid nesting produce a page-local error. Malformed XML and invalid document/locale structure can invalidate the whole book. An optional XML 1.0 declaration is accepted at the start of the document. Processing instructions, DTDs, and external entities are unsupported; XML comments are ignored. CDATA inside inline contents is treated as literal text.

Nesting shares a limit of 32 across block structure and inline formatting. A block directly inside a page starts at depth 0; each nested element increases depth by one. Images must reference existing game resources under `/Textures/`, for example `<image src="/Textures/Interface/Nano/button.svg.96dpi.png"/>`. Paths cannot contain empty segments, `.` or `..` segments, backslashes, colons, `?`, `#`, `%`, or control characters. The referenced file must also load as a texture to be displayed.

## Tables

Use this block inside a page or layout column:

```xml
<table>
  <tr><th>Item</th><th>Notes</th></tr>
  <tr><td>Pen</td><td><b>Blue</b> ink<br/>Keep capped.</td></tr>
  <tr><td>Spare paper</td></tr>
</table>
```

Header cells (`th`) are bold. All rows share the maximum cell count of any row. Short rows receive empty trailing cells, so later rows stay aligned. Empty rows are rejected; empty cells are allowed. Cells cannot contain page blocks, images, layouts, or nested tables. Column spans, row spans, borders, and custom style attributes are unsupported.

## Rows and columns

Use `layout` for vertically stacked rows, each containing horizontally arranged columns. Each column stacks ordinary page blocks vertically and may contain another `layout` within the depth limit.

```xml
<layout>
  <row>
    <column>
      <h2>Left column</h2>
      <p>Notes on the left.</p>
    </column>
    <column>
      <h3>Right column</h3>
      <p>Related information.<br/>Another line.</p>
      <hr/>
      <p>A final note.</p>
    </column>
  </row>
</layout>
```

Columns expand into the available row width using standard UI containers. Width, alignment, style, font, script, and URL attributes are unsupported. Keep the number of columns suitable for a book page.

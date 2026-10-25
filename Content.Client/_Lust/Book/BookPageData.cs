using Robust.Shared.Utility;

namespace Content.Client._Lust.Book;

/// <summary>A validated page, or a page-local error with no renderable nodes.</summary>
public sealed record BookPageData(IReadOnlyList<BookPageNode> Nodes, BookContentError? Error = null);

/// <summary>Book content nodes are data only and never instantiate arbitrary UI markup.</summary>
public abstract record BookPageNode;

/// <summary>Literal text; renderers must not interpret this as rich-text markup.</summary>
public sealed record BookTextNode(string Text) : BookPageNode;

/// <summary>A paragraph containing validated inline nodes.</summary>
public sealed record BookParagraphNode(IReadOnlyList<BookPageNode> Children) : BookPageNode;

/// <summary>A heading containing validated inline nodes.</summary>
public sealed record BookHeadingNode(int Level, IReadOnlyList<BookPageNode> Children) : BookPageNode;

/// <summary>An explicit inline line break.</summary>
public sealed record BookLineBreakNode : BookPageNode;

/// <summary>A horizontal block separator.</summary>
public sealed record BookHorizontalRuleNode : BookPageNode;

/// <summary>A table whose rows share the same column layout.</summary>
public sealed record BookTableNode(IReadOnlyList<BookTableRowNode> Rows) : BookPageNode;

/// <summary>A nonempty row of table cells.</summary>
public sealed record BookTableRowNode(IReadOnlyList<BookTableCellNode> Cells) : BookPageNode;

/// <summary>A table cell containing validated inline nodes.</summary>
public sealed record BookTableCellNode(bool IsHeader, IReadOnlyList<BookPageNode> Children) : BookPageNode;

/// <summary>A vertical sequence of layout rows.</summary>
public sealed record BookLayoutNode(IReadOnlyList<BookLayoutRowNode> Rows) : BookPageNode;

/// <summary>A horizontal sequence of layout columns.</summary>
public sealed record BookLayoutRowNode(IReadOnlyList<BookLayoutColumnNode> Columns) : BookPageNode;

/// <summary>A vertical sequence of page blocks, including nested layouts.</summary>
public sealed record BookLayoutColumnNode(IReadOnlyList<BookPageNode> Children) : BookPageNode;

/// <summary>Bold inline text.</summary>
public sealed record BookBoldNode(IReadOnlyList<BookPageNode> Children) : BookPageNode;

/// <summary>Italic inline text.</summary>
public sealed record BookItalicNode(IReadOnlyList<BookPageNode> Children) : BookPageNode;

/// <summary>Inline text with a validated color.</summary>
public sealed record BookColorNode(Color Color, IReadOnlyList<BookPageNode> Children) : BookPageNode;

/// <summary>An image from an existing game texture resource.</summary>
public sealed record BookImageNode(ResPath Source) : BookPageNode;

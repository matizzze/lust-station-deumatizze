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
public sealed record BookHeadingNode(IReadOnlyList<BookPageNode> Children) : BookPageNode;

/// <summary>Bold inline text.</summary>
public sealed record BookBoldNode(IReadOnlyList<BookPageNode> Children) : BookPageNode;

/// <summary>Italic inline text.</summary>
public sealed record BookItalicNode(IReadOnlyList<BookPageNode> Children) : BookPageNode;

/// <summary>Inline text with a validated color.</summary>
public sealed record BookColorNode(Color Color, IReadOnlyList<BookPageNode> Children) : BookPageNode;

/// <summary>An image from an existing game texture resource.</summary>
public sealed record BookImageNode(ResPath Source) : BookPageNode;

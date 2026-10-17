using System.Text;

namespace Content.Client._Lust.Book;

/// <summary>
/// Reads book XML tokens without a DOM or external entity resolution. Only the current token and
/// open element names are retained; syntax validation and invalid-page recovery are iterative.
/// </summary>
internal sealed class BookXmlReader
{
    private readonly string _xml;
    private readonly List<string> _elements = new();
    private readonly int _start;
    private int _position;
    private bool _hasRoot;

    internal Token Current { get; private set; } = new(TokenKind.EndOfDocument);

    internal BookXmlReader(string xml)
    {
        ValidateCharacters(xml);
        _xml = xml.Replace("\r\n", "\n").Replace('\r', '\n');
        _start = _xml.Length > 0 && _xml[0] == '\uFEFF' ? 1 : 0;
        _position = _start;
        Read();
    }

    internal void Read()
    {
        while (_position < _xml.Length)
        {
            if (At("<!--"))
            {
                _position += 4;
                var end = FindEnd("-->");
                if (_xml.IndexOf("--", _position, end - _position, StringComparison.Ordinal) >= 0 ||
                    end > _position && _xml[end - 1] == '-')
                {
                    throw Error("Invalid XML comment.");
                }

                _position = end + 3;
                continue;
            }

            if (At("<![CDATA["))
            {
                if (_elements.Count == 0)
                    throw Error("CDATA is only allowed inside an element.");

                _position += 9;
                var end = FindEnd("]]>");
                Current = new Token(TokenKind.Text, text: _xml[_position..end], depth: _elements.Count);
                _position = end + 3;
                return;
            }

            if (At("<!"))
                throw Error("DTD and entity declarations are not allowed.");

            if (At("<?"))
            {
                var start = _position;
                _position += 2;
                var target = ReadName();
                if (string.Equals(target, "xml", StringComparison.OrdinalIgnoreCase))
                {
                    if (target != "xml" || start != _start)
                        throw Error("An XML declaration is only allowed at the start of the document.");

                    ReadDeclaration();
                    continue;
                }

                if (!At("?>") && !SkipWhitespace())
                    throw Error("A processing instruction requires whitespace after its target.");

                _position = FindEnd("?>") + 2;
                Current = new Token(TokenKind.Instruction, target, depth: _elements.Count);
                return;
            }

            if (At("</"))
            {
                _position += 2;
                var name = ReadName();
                SkipWhitespace();
                Require(">");
                if (_elements.Count == 0 || _elements[^1] != name)
                    throw Error($"Unexpected closing element </{name}>.");

                _elements.RemoveAt(_elements.Count - 1);
                Current = new Token(TokenKind.EndElement, name, depth: _elements.Count);
                return;
            }

            if (At("<"))
            {
                _position++;
                var name = ReadName();
                var attributes = ReadAttributes(false);
                var empty = At("/>");
                Require(empty ? "/>" : ">");
                if (_elements.Count == 0)
                {
                    if (_hasRoot)
                        throw Error("The document must contain exactly one root element.");

                    _hasRoot = true;
                }

                Current = new Token(TokenKind.StartElement, name, attributes: attributes,
                    empty: empty, depth: _elements.Count);
                if (!empty)
                    _elements.Add(name);

                return;
            }

            var outsideRoot = _elements.Count == 0;
            var text = ReadValue(null);
            if (outsideRoot)
            {
                foreach (var character in text)
                {
                    if (!IsWhitespace(character))
                        throw Error("Text is not allowed outside the root element.");
                }
            }

            Current = new Token(TokenKind.Text, text: text, depth: _elements.Count);
            return;
        }

        if (_elements.Count != 0 || !_hasRoot)
            throw Error("The XML document is incomplete.");

        Current = new Token(TokenKind.EndOfDocument);
    }

    internal void SkipElement(Token element)
    {
        if (element.Empty)
        {
            if (Current == element)
                Read();

            return;
        }

        while (Current.Kind != TokenKind.EndOfDocument)
        {
            if (Current.Kind == TokenKind.EndElement && Current.Depth == element.Depth)
            {
                Read();
                return;
            }

            Read();
        }
    }

    private Dictionary<string, string> ReadAttributes(bool declaration)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        while (true)
        {
            var separated = SkipWhitespace();
            if (declaration ? At("?>") : At(">") || At("/>"))
                return attributes;

            if (!separated)
                throw Error("Attributes must be separated by whitespace.");

            var name = ReadName();
            SkipWhitespace();
            Require("=");
            SkipWhitespace();
            if (_position >= _xml.Length || _xml[_position] is not ('\'' or '"'))
                throw Error("Attribute values must be quoted.");

            var quote = _xml[_position++];
            var value = ReadValue(quote, !declaration);
            Require(quote.ToString());
            if (!attributes.TryAdd(name, value))
                throw Error($"Duplicate attribute '{name}'.");
        }
    }

    private void ReadDeclaration()
    {
        var attributes = ReadAttributes(true);
        Require("?>");
        if (!attributes.TryGetValue("version", out var version) || version != "1.0")
            throw Error("Only XML version 1.0 is supported.");

        var index = 0;
        var hasStandalone = false;
        foreach (var (name, value) in attributes)
        {
            switch (name)
            {
                case "version" when index == 0:
                    break;
                case "encoding" when index == 1 && !hasStandalone:
                    for (var i = 0; i < value.Length; i++)
                    {
                        var character = value[i];
                        if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                            continue;

                        if (i == 0 || character is not (>= '0' and <= '9' or '.' or '_' or '-'))
                            throw Error("Invalid XML encoding name.");
                    }

                    if (value.Length == 0)
                        throw Error("Invalid XML encoding name.");

                    break;
                case "standalone" when index > 0 && value is "yes" or "no":
                    hasStandalone = true;
                    break;
                default:
                    throw Error($"Unsupported XML declaration attribute '{name}'.");
            }

            index++;
        }
    }

    private string ReadName()
    {
        var start = _position;
        if (_position >= _xml.Length || !IsNameStart(_xml[_position]))
            throw Error("Expected an XML name.");

        _position++;
        while (_position < _xml.Length)
        {
            var character = _xml[_position];
            if (!IsNameStart(character) && character is not (>= '0' and <= '9' or '-' or '.' or '\u00B7' or
                >= '\u0300' and <= '\u036F' or >= '\u203F' and <= '\u2040'))
            {
                break;
            }

            _position++;
        }

        return _xml[start.._position];
    }

    private string ReadValue(char? quote, bool allowEntities = true)
    {
        var text = new StringBuilder();
        while (_position < _xml.Length)
        {
            var character = _xml[_position];
            if (quote != null && character == quote || quote == null && character == '<')
                break;

            if (character == '<')
                throw Error("A literal '<' is not allowed in an attribute value.");

            if (quote == null && At("]]>"))
                throw Error("The sequence ']]>' is only allowed to close CDATA.");

            if (character == '&')
            {
                if (!allowEntities)
                    throw Error("Entity references are not allowed in an XML declaration.");

                if (_elements.Count == 0 && quote == null)
                    throw Error("Entity references are not allowed outside the root element.");

                AppendEntity(text);
                continue;
            }

            text.Append(quote != null && IsWhitespace(character) ? ' ' : character);
            _position++;
        }

        return text.ToString();
    }

    private void AppendEntity(StringBuilder text)
    {
        _position++;
        var start = _position;
        var end = _xml.IndexOf(';', start);
        if (end < 0)
            throw Error("Unterminated entity reference.");

        var entity = _xml[start..end];
        _position = end + 1;
        switch (entity)
        {
            case "amp": text.Append('&'); return;
            case "lt": text.Append('<'); return;
            case "gt": text.Append('>'); return;
            case "apos": text.Append('\''); return;
            case "quot": text.Append('"'); return;
        }

        if (!entity.StartsWith('#'))
            throw Error($"Unsupported entity reference '&{entity};'.");

        var hexadecimal = entity.Length > 1 && entity[1] == 'x';
        var firstDigit = hexadecimal ? 2 : 1;
        var radix = hexadecimal ? 16 : 10;
        var scalar = 0;
        if (firstDigit == entity.Length)
            throw Error("An XML character reference requires digits.");

        for (var index = firstDigit; index < entity.Length; index++)
        {
            var character = entity[index];
            var digit = character switch
            {
                >= '0' and <= '9' => character - '0',
                >= 'a' and <= 'f' => character - 'a' + 10,
                >= 'A' and <= 'F' => character - 'A' + 10,
                _ => -1,
            };
            if (digit < 0 || digit >= radix || scalar > (0x10FFFF - digit) / radix)
                throw Error("Invalid XML character reference.");

            scalar = scalar * radix + digit;
        }

        if (!IsXmlCharacter(scalar))
            throw Error("The character reference is not a valid XML character.");

        if (scalar <= 0xFFFF)
        {
            text.Append((char) scalar);
            return;
        }

        scalar -= 0x10000;
        text.Append((char) (0xD800 + (scalar >> 10)));
        text.Append((char) (0xDC00 + (scalar & 0x3FF)));
    }

    private static void ValidateCharacters(string xml)
    {
        for (var index = 0; index < xml.Length; index++)
        {
            var scalar = (int) xml[index];
            if (scalar is >= 0xD800 and <= 0xDBFF && index + 1 < xml.Length && xml[index + 1] is >= '\uDC00' and <= '\uDFFF')
            {
                index++;
                continue;
            }

            if (!IsXmlCharacter(scalar))
                throw new SyntaxException("The document contains an invalid XML character.");
        }
    }

    private static bool IsXmlCharacter(int scalar)
    {
        return scalar is 0x9 or 0xA or 0xD or >= 0x20 and <= 0xD7FF or
            >= 0xE000 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF;
    }

    private static bool IsNameStart(char character)
    {
        return character is ':' or '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
            >= '\u00C0' and <= '\u00D6' or >= '\u00D8' and <= '\u00F6' or
            >= '\u00F8' and <= '\u02FF' or >= '\u0370' and <= '\u037D' or
            >= '\u037F' and <= '\u1FFF' or >= '\u200C' and <= '\u200D' or
            >= '\u2070' and <= '\u218F' or >= '\u2C00' and <= '\u2FEF' or
            >= '\u3001' and <= '\uD7FF' or >= '\uF900' and <= '\uFDCF' or >= '\uFDF0' and <= '\uFFFD';
    }

    private static bool IsWhitespace(char character)
    {
        return character is ' ' or '\t' or '\n' or '\r';
    }

    private bool SkipWhitespace()
    {
        var start = _position;
        while (_position < _xml.Length && IsWhitespace(_xml[_position]))
            _position++;

        return _position != start;
    }

    private bool At(string value)
    {
        return _position + value.Length <= _xml.Length &&
            string.CompareOrdinal(_xml, _position, value, 0, value.Length) == 0;
    }

    private void Require(string value)
    {
        if (!At(value))
            throw Error($"Expected '{value}'.");

        _position += value.Length;
    }

    private int FindEnd(string terminator)
    {
        var end = _xml.IndexOf(terminator, _position, StringComparison.Ordinal);
        if (end < 0)
            throw Error($"Missing '{terminator}'.");

        return end;
    }

    private SyntaxException Error(string details)
    {
        return new SyntaxException($"{details} Character {_position}.");
    }

    internal sealed class Token(TokenKind kind, string name = "", string text = "",
        Dictionary<string, string>? attributes = null, bool empty = false, int depth = 0)
    {
        internal TokenKind Kind { get; } = kind;
        internal string Name { get; } = name;
        internal string Text { get; } = text;
        internal Dictionary<string, string>? Attributes { get; } = attributes;
        internal bool Empty { get; } = empty;
        internal int Depth { get; } = depth;
    }

    internal enum TokenKind : byte
    {
        EndOfDocument,
        StartElement,
        EndElement,
        Text,
        Instruction,
    }

    internal sealed class SyntaxException(string message) : Exception(message)
    {
    }
}

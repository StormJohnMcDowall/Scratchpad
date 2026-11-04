using System;
using System.Collections.Generic;
using ColorCode;
using ColorCode.Common;
using ColorCode.Parsing;

namespace Scratchpad.Core
{
    /// <summary>What a piece of highlighted code is; the preview picks a colour per kind.</summary>
    public enum CodeTokenKind
    {
        Plain,
        Keyword,
        String,
        Comment,
        Number,
        TypeName,
        Preprocessor,
        MarkupDelimiter,
        MarkupElement,
        MarkupAttribute,
        MarkupAttributeValue,
        MarkupEntity,
        JsonKey
    }

    public struct CodeToken
    {
        public CodeToken(string text, CodeTokenKind kind)
        {
            Text = text;
            Kind = kind;
        }

        public string Text { get; }

        public CodeTokenKind Kind { get; }
    }

    public static class CodeHighlighter
    {
        private static readonly Dictionary<string, ILanguage> LanguagesByTag = new Dictionary<string, ILanguage>(StringComparer.OrdinalIgnoreCase)
        {
            ["csharp"] = Languages.CSharp,
            ["cs"] = Languages.CSharp,
            ["c#"] = Languages.CSharp,
            ["json"] = new SafeJson(),
            ["js"] = Languages.JavaScript,
            ["javascript"] = Languages.JavaScript,
            ["html"] = Languages.Html,
            ["htm"] = Languages.Html,
            ["xml"] = Languages.Xml,
            ["css"] = Languages.Css,
        };

        private static readonly Dictionary<string, CodeTokenKind> Kinds = new Dictionary<string, CodeTokenKind>
        {
            [ScopeName.Keyword] = CodeTokenKind.Keyword,
            [ScopeName.ControlKeyword] = CodeTokenKind.Keyword,
            [ScopeName.PseudoKeyword] = CodeTokenKind.Keyword,
            [ScopeName.JsonConst] = CodeTokenKind.Keyword,
            [ScopeName.String] = CodeTokenKind.String,
            [ScopeName.StringCSharpVerbatim] = CodeTokenKind.String,
            [ScopeName.StringEscape] = CodeTokenKind.String,
            [ScopeName.JsonString] = CodeTokenKind.String,
            [ScopeName.Comment] = CodeTokenKind.Comment,
            [ScopeName.HtmlComment] = CodeTokenKind.Comment,
            [ScopeName.XmlComment] = CodeTokenKind.Comment,
            [ScopeName.XmlDocComment] = CodeTokenKind.Comment,
            [ScopeName.XmlDocTag] = CodeTokenKind.Comment,
            [ScopeName.Number] = CodeTokenKind.Number,
            [ScopeName.JsonNumber] = CodeTokenKind.Number,
            [ScopeName.ClassName] = CodeTokenKind.TypeName,
            [ScopeName.Type] = CodeTokenKind.TypeName,
            [ScopeName.TypeVariable] = CodeTokenKind.TypeName,
            [ScopeName.PreprocessorKeyword] = CodeTokenKind.Preprocessor,
            [ScopeName.HtmlTagDelimiter] = CodeTokenKind.MarkupDelimiter,
            [ScopeName.HtmlOperator] = CodeTokenKind.MarkupDelimiter,
            [ScopeName.XmlDelimiter] = CodeTokenKind.MarkupDelimiter,
            [ScopeName.HtmlElementName] = CodeTokenKind.MarkupElement,
            [ScopeName.XmlName] = CodeTokenKind.MarkupElement,
            [ScopeName.CssSelector] = CodeTokenKind.MarkupElement,
            [ScopeName.HtmlAttributeName] = CodeTokenKind.MarkupAttribute,
            [ScopeName.XmlAttribute] = CodeTokenKind.MarkupAttribute,
            [ScopeName.CssPropertyName] = CodeTokenKind.MarkupAttribute,
            [ScopeName.HtmlAttributeValue] = CodeTokenKind.MarkupAttributeValue,
            [ScopeName.XmlAttributeValue] = CodeTokenKind.MarkupAttributeValue,
            [ScopeName.XmlAttributeQuotes] = CodeTokenKind.MarkupAttributeValue,
            [ScopeName.CssPropertyValue] = CodeTokenKind.MarkupAttributeValue,
            [ScopeName.HtmlEntity] = CodeTokenKind.MarkupEntity,
            [ScopeName.JsonKey] = CodeTokenKind.JsonKey,
        };

        /// <summary>
        /// Whether a fenced block's info string names a language the preview colours. Only the
        /// first word counts, so "c# {.x}" is C#.
        /// </summary>
        public static bool IsSupported(string infoString)
        {
            return FindLanguage(infoString) != null;
        }

        /// <summary>
        /// Splits code into coloured pieces whose texts join back to exactly the input, with
        /// neighbouring pieces of the same kind merged. Null when the language isn't supported or
        /// ColorCode fails, so the caller can leave the block plain.
        /// </summary>
        public static IReadOnlyList<CodeToken> Tokenize(string code, string infoString)
        {
            var language = FindLanguage(infoString);
            if (language == null || code == null)
            {
                return null;
            }

            var tokens = new List<CodeToken>();
            if (code.Length == 0)
            {
                return tokens;
            }

            try
            {
                new TokenCollector(tokens).Collect(code, language);
            }
            catch (Exception)
            {
                return null;
            }

            return tokens;
        }

        private static ILanguage FindLanguage(string infoString)
        {
            if (string.IsNullOrWhiteSpace(infoString))
            {
                return null;
            }

            var firstWord = infoString.Trim().Split((char[])null, 2, StringSplitOptions.RemoveEmptyEntries)[0];
            return LanguagesByTag.TryGetValue(firstWord, out var language) ? language : null;
        }

        // ColorCode's own JSON grammar with its string pattern replaced. Its "[^"\\]*(?:\\[^\r\n]|
        // [^"\\]*)*" nests [^"\\]* inside a repeat, which backtracks exponentially when a quote
        // never closes and froze the preview on truncated JSON. These alternatives can't match the
        // same character, so a failed match stays linear. A distinct Id keeps ColorCode's
        // compiled-language cache from mixing it up with the built-in "json".
        private sealed class SafeJson : ILanguage
        {
            private const string StringPattern = @"""(?:[^""\\]|\\.)*""";

            public string Id => "scratchpad-json";

            public string Name => "JSON";

            public string CssClassName => "json";

            public string FirstLinePattern => null;

            public IList<LanguageRule> Rules { get; } = new List<LanguageRule>
            {
                new LanguageRule(@"[,\{]\s*(" + StringPattern + @")\s*:", new Dictionary<int, string> { { 1, ScopeName.JsonKey } }),
                new LanguageRule(StringPattern, new Dictionary<int, string> { { 0, ScopeName.JsonString } }),
                new LanguageRule(@"-?(?:0|[1-9][0-9]*)(?:\.[0-9]*)?(?:[eE][-+]?[0-9]+)?", new Dictionary<int, string> { { 0, ScopeName.JsonNumber } }),
                new LanguageRule(@"\b(true|false|null)\b", new Dictionary<int, string> { { 1, ScopeName.JsonConst } }),
            };

            public bool HasAlias(string lang)
            {
                return false;
            }
        }

        // ColorCode only hands out its parser to formatters, so this is a "formatter" that keeps
        // the scopes instead of writing HTML. Parse calls Write several times with consecutive
        // fragments of the code, each with scope indexes relative to that fragment.
        private sealed class TokenCollector : CodeColorizerBase
        {
            private readonly List<CodeToken> _tokens;

            public TokenCollector(List<CodeToken> tokens)
                : base(null, null)
            {
                _tokens = tokens;
            }

            public void Collect(string code, ILanguage language)
            {
                languageParser.Parse(code, language, Write);
            }

            protected override void Write(string parsedSourceCode, IList<Scope> scopes)
            {
                if (string.IsNullOrEmpty(parsedSourceCode))
                {
                    return;
                }

                var kinds = new CodeTokenKind[parsedSourceCode.Length];
                Paint(kinds, scopes, CodeTokenKind.Plain);

                var start = 0;
                for (var i = 1; i <= kinds.Length; i++)
                {
                    if (i == kinds.Length || kinds[i] != kinds[start])
                    {
                        Add(parsedSourceCode.Substring(start, i - start), kinds[start]);
                        start = i;
                    }
                }
            }

            // Parents first, then children over them, so the innermost scope wins. A scope with no
            // colour of its own (an embedded <script> block, say) keeps its parent's.
            private static void Paint(CodeTokenKind[] kinds, IList<Scope> scopes, CodeTokenKind inherited)
            {
                if (scopes == null)
                {
                    return;
                }

                foreach (var scope in scopes)
                {
                    var kind = scope.Name != null && Kinds.TryGetValue(scope.Name, out var mapped) ? mapped : inherited;
                    var end = Math.Min(scope.Index + scope.Length, kinds.Length);
                    for (var i = Math.Max(scope.Index, 0); i < end; i++)
                    {
                        kinds[i] = kind;
                    }
                    Paint(kinds, scope.Children, kind);
                }
            }

            private void Add(string text, CodeTokenKind kind)
            {
                var last = _tokens.Count - 1;
                if (last >= 0 && _tokens[last].Kind == kind)
                {
                    _tokens[last] = new CodeToken(_tokens[last].Text + text, kind);
                }
                else
                {
                    _tokens.Add(new CodeToken(text, kind));
                }
            }
        }
    }
}

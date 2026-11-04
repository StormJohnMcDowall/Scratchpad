# Code Highlighting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Colour fenced code blocks in the rendered preview by language (C#, JSON, JS, HTML, plus XML and CSS), Visual Studio style, in both themes.

**Architecture:** `Scratchpad.Core` gains `CodeHighlighter`, which wraps ColorCode.Core's parser and returns a flat list of `CodeToken`s (text + kind) that concatenate back to the input exactly; and `PreviewSourceMap`'s `SourceBlock` learns each fenced block's language tag (Markdig.Wpf drops it when rendering). The Shell, after each render, pairs preview units with source blocks (the same 1:1 pairing double-click already uses), and replaces each tagged code paragraph's `Run`/`LineBreak` inlines with one `Run` per token whose `Foreground` is a resource *reference* to a per-kind brush, so live theme switches recolour it.

**Tech Stack:** C#; `Scratchpad.Core` (`netstandard2.0`) + `Scratchpad.Core.Tests` (`net10.0`, xUnit); WPF `Scratchpad` (`net48`, compiles here via `EnableWindowsTargeting`, can't run); new package **ColorCode.Core 2.0.15** (MIT, no dependencies on netstandard2.0) in `Scratchpad.Core`.

**Spec:** No separate design doc. Agreed in chat on 2026-11-04; the requirements are copied into Global Constraints below.

## Global Constraints

- Preview only. The editor stays a plain TextBox.
- Highlight only fenced blocks whose info string's first word is (case-insensitive) `csharp`, `cs`, `c#`, `json`, `js`, `javascript`, `html`, `htm`, `xml`, `css`.
- Untagged fences, unrecognised tags, indented code blocks and inline `` `code` `` render exactly as today.
- Colours: Visual Studio default palette, darkened where needed to stay readable (~4.5:1+) on Markdig.Wpf's light code background `#D3D3D3`; VS dark palette in dark (code background `#2F2A19`). Live theme switch recolours highlighted code.
- Unchanged: code-block background, code font family/size settings, double-click-to-edit precision, keep-your-place, task checkboxes.
- No on/off setting.
- Never crash the preview: any highlighter failure leaves that block plain.

## Review Focus

- Double-clicking a short token (say the `i` in `int i = 0`) must put the caret on *that* character, not the first `i` in the block. Today each code line is one Run, which `LocateInBlock` finds by text; per-token Runs would make it find the wrong occurrence (Task 3, Step 3 + manual step).
- A token that spans lines (a `/* … */` comment, an HTML comment) must keep its line breaks and stay coloured on every line (Task 1 round-trip test; Task 3 splits on `\n`).
- A fenced block inside a list item or block quote must still be highlighted, and must not shift the pairing for later blocks (Task 2 nested test; manual step).
- Re-showing the preview with unchanged text reuses Markdig.Wpf's already-highlighted document. Re-highlighting must be idempotent, not double-colour or lose text (Task 3 rebuilds from the current inlines; manual step).
- Unterminated or junk input (`"abc`, `<div`, `{` alone, tag `CSharp`, tag `c# {.x}`) must not throw and must round-trip text exactly (Task 1 tests).

---

### Task 1: `CodeHighlighter` in Core

**Files:**
- Modify: `Scratchpad.Core/Scratchpad.Core.csproj` (add `<PackageReference Include="ColorCode.Core" Version="2.0.15" />`)
- Create: `Scratchpad.Core/CodeHighlighter.cs`
- Test: `Scratchpad.Core.Tests/CodeHighlighterTests.cs`

**Interfaces:**
- Produces:
  - `public enum CodeTokenKind { Plain, Keyword, String, Comment, Number, TypeName, Preprocessor, MarkupDelimiter, MarkupElement, MarkupAttribute, MarkupAttributeValue, MarkupEntity, JsonKey }`
  - `public struct CodeToken { public CodeToken(string text, CodeTokenKind kind); public string Text { get; } public CodeTokenKind Kind { get; } }`
  - `public static class CodeHighlighter`
    - `public static bool IsSupported(string infoString)`: true when the first whitespace-delimited word of `infoString` is one of the Global Constraints tags (case-insensitive); false for null/blank.
    - `public static IReadOnlyList<CodeToken> Tokenize(string code, string infoString)`: null when unsupported, when `code` is null, or when ColorCode throws. Otherwise tokens whose `Text`s concatenate to exactly `code`, with adjacent same-kind tokens merged and no empty tokens. Empty `code` gives an empty list.

**ColorCode facts (probed 2026-11-04):** derive a private class from `ColorCode.CodeColorizerBase` (`base(null, null)` works), call `languageParser.Parse(code, language, (parsed, scopes) => …)`. The callback fires **several times with consecutive fragments**. Scope `Index` is relative to that fragment, and scopes nest via `Scope.Children`. Language ids: `ColorCode.Languages.CSharp`, `.JavaScript`, `.Html`, `.Xml`, `.Css`, and `Languages.FindById("json")`. Scope names are `ColorCode.Common.ScopeName` constants.

**Scope → kind map** (anything else: a child inherits its parent's kind; a top-level scope is Plain):
- Keyword: `Keyword`, `ControlKeyword`, `PseudoKeyword`, `JsonConst`
- String: `String`, `StringCSharpVerbatim`, `StringEscape`, `JsonString`
- Comment: `Comment`, `HtmlComment`, `XmlComment`, `XmlDocComment`, `XmlDocTag`
- Number: `Number`, `JsonNumber`
- TypeName: `ClassName`, `Type`, `TypeVariable`
- Preprocessor: `PreprocessorKeyword`
- MarkupDelimiter: `HtmlTagDelimiter`, `HtmlOperator`, `XmlDelimiter`
- MarkupElement: `HtmlElementName`, `XmlName`, `CssSelector`
- MarkupAttribute: `HtmlAttributeName`, `XmlAttribute`, `CssPropertyName`
- MarkupAttributeValue: `HtmlAttributeValue`, `XmlAttributeValue`, `XmlAttributeQuotes`, `CssPropertyValue`
- MarkupEntity: `HtmlEntity`
- JsonKey: `JsonKey`

Approach: per fragment, fill a `CodeTokenKind[]` the fragment's length with Plain, paint scopes parent-first then children (innermost wins), run-length encode, append to the result merging with the previous token when kinds match.

- [ ] **Step 1: Write the failing tests** (helper `string Joined(IReadOnlyList<CodeToken> t) => string.Concat(t.Select(x => x.Text))`, and `CodeTokenKind KindOf(tokens, "for")` = the kind of the token whose Text equals the given text)

```csharp
[Theory]
[InlineData("csharp")] [InlineData("cs")] [InlineData("C#")] [InlineData("CSharp")]
[InlineData("json")] [InlineData("js")] [InlineData("JavaScript")] [InlineData("html")]
[InlineData("htm")] [InlineData("xml")] [InlineData("css")] [InlineData("c# {.x}")] [InlineData("  json  ")]
public void IsSupported_KnownTags(string tag) => Assert.True(CodeHighlighter.IsSupported(tag));

[Theory]
[InlineData(null)] [InlineData("")] [InlineData("   ")] [InlineData("python")] [InlineData("text")] [InlineData("csharpx")]
public void IsSupported_OtherTags(string tag) => Assert.False(CodeHighlighter.IsSupported(tag));

[Fact] public void Tokenize_Unsupported_ReturnsNull() => Assert.Null(CodeHighlighter.Tokenize("x = 1", "python"));
[Fact] public void Tokenize_Empty_ReturnsEmpty() => Assert.Empty(CodeHighlighter.Tokenize("", "csharp"));

[Fact]
public void Tokenize_CSharp_ColoursKeywordsNumbersCommentsStrings()
{
    var code = "for (int i = 0; i < 10; i++)\n{\n  // Code to execute.\n  var s = \"hi\";\n}";
    var tokens = CodeHighlighter.Tokenize(code, "csharp");
    Assert.Equal(code, Joined(tokens));
    Assert.Equal(CodeTokenKind.Keyword, KindOf(tokens, "for"));
    Assert.Equal(CodeTokenKind.Keyword, KindOf(tokens, "int"));
    Assert.Equal(CodeTokenKind.Number, KindOf(tokens, "10"));
    Assert.Equal(CodeTokenKind.Comment, KindOf(tokens, "// Code to execute."));
    Assert.Equal(CodeTokenKind.String, KindOf(tokens, "\"hi\""));
}

[Fact]
public void Tokenize_Json_KeysAndValues()
{
    var code = "{\"a\": 1, \"b\": [true, \"x\"]}";
    var tokens = CodeHighlighter.Tokenize(code, "json");
    Assert.Equal(code, Joined(tokens));
    Assert.Equal(CodeTokenKind.JsonKey, KindOf(tokens, "\"a\""));
    Assert.Equal(CodeTokenKind.Number, KindOf(tokens, "1"));
    Assert.Equal(CodeTokenKind.Keyword, KindOf(tokens, "true"));
    Assert.Equal(CodeTokenKind.String, KindOf(tokens, "\"x\""));
}

[Fact]
public void Tokenize_Html_TagsAttributesEntities()
{
    var code = "<div class=\"x\">a &amp; b</div>";
    var tokens = CodeHighlighter.Tokenize(code, "html");
    Assert.Equal(code, Joined(tokens));
    Assert.Equal(CodeTokenKind.MarkupElement, KindOf(tokens, "div"));
    Assert.Equal(CodeTokenKind.MarkupAttribute, KindOf(tokens, "class"));
    Assert.Equal(CodeTokenKind.MarkupAttributeValue, KindOf(tokens, "\"x\""));
    Assert.Equal(CodeTokenKind.MarkupEntity, KindOf(tokens, "&amp;"));
}

[Fact]
public void Tokenize_MultiLineComment_StaysOneCommentTokenWithItsNewlines()
{
    var code = "/* one\ntwo */\nint x;";
    var tokens = CodeHighlighter.Tokenize(code, "js");
    Assert.Equal(code, Joined(tokens));
    Assert.Equal(CodeTokenKind.Comment, KindOf(tokens, "/* one\ntwo */"));
}

[Theory]
[InlineData("\"abc", "csharp")] [InlineData("<div", "html")] [InlineData("{", "json")]
[InlineData("/* never closed", "css")] [InlineData("<a><b></a>", "xml")]
public void Tokenize_JunkInput_RoundTripsWithoutThrowing(string code, string tag)
{
    var tokens = CodeHighlighter.Tokenize(code, tag);
    Assert.Equal(code, Joined(tokens));
    Assert.DoesNotContain(tokens, t => t.Text.Length == 0);
}

[Fact]
public void Tokenize_AdjacentSameKindTokensAreMerged()
{
    var tokens = CodeHighlighter.Tokenize("a b c", "csharp");
    for (var i = 1; i < tokens.Count; i++) Assert.NotEqual(tokens[i - 1].Kind, tokens[i].Kind);
}
```

- [ ] **Step 2: Run them, expect a compile failure** (`CodeHighlighter` not defined)
Run: `dotnet test Scratchpad.Core.Tests --filter CodeHighlighterTests`

- [ ] **Step 3: Add the package reference and implement `CodeHighlighter.cs`** as specified in the Interfaces, map and approach above. Wrap the whole parse in `try/catch (Exception)` → null.

- [ ] **Step 4: Run tests, expect PASS** (same command), then the whole suite: `dotnet test Scratchpad.Core.Tests` → all green.

- [ ] **Step 5: Commit** `feat: CodeHighlighter tokenises fenced code via ColorCode`

---

### Task 2: Fenced-code language on `SourceBlock`

**Files:**
- Modify: `Scratchpad.Core/PreviewSourceMap.cs`
- Test: `Scratchpad.Core.Tests/PreviewSourceMapTests.cs`

**Interfaces:**
- Produces: `SourceBlock` gains `public string CodeLanguage { get; }` and a constructor overload `SourceBlock(int start, int end, string codeLanguage)`. The existing two-arg constructor stays, with null language. `Build` sets it to `FencedCodeBlock.Info` (Markdig already splits off the arguments, so `c# {.x}` gives `c#`) for fenced blocks with a non-empty info string, and null for every other block, including indented code.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Build_FencedCode_CarriesItsLanguage()
{
    var blocks = PreviewSourceMap.Build("Para\n\n```csharp\nint x;\n```\n\n```\nplain\n```\n\n    indented\n");
    Assert.Equal(4, blocks.Count);
    Assert.Null(blocks[0].CodeLanguage);
    Assert.Equal("csharp", blocks[1].CodeLanguage);
    Assert.Null(blocks[2].CodeLanguage);
    Assert.Null(blocks[3].CodeLanguage);
}

[Fact]
public void Build_FencedCodeInsideListAndQuote_CarriesItsLanguage()
{
    var blocks = PreviewSourceMap.Build("- item\n\n  ```json\n  {}\n  ```\n\n> ```js\n> x\n> ```\n\nAfter\n");
    Assert.Equal(4, blocks.Count);
    Assert.Equal("json", blocks[1].CodeLanguage);
    Assert.Equal("js", blocks[2].CodeLanguage);
    Assert.Null(blocks[3].CodeLanguage);
}
```

- [ ] **Step 2: Run, expect compile failure:** `dotnet test Scratchpad.Core.Tests --filter PreviewSourceMapTests`
- [ ] **Step 3: Implement** in `Collect`: `block is FencedCodeBlock fenced && !string.IsNullOrWhiteSpace(fenced.Info) ? fenced.Info : null`. Update the `Build` doc comment to mention it.
- [ ] **Step 4: Run, expect PASS**, then the full suite.
- [ ] **Step 5: Commit** `feat: source map records each fenced block's language`

---

### Task 3: Colour the preview (Shell)

**Files:**
- Modify: `Scratchpad/Themes/Light.xaml`, `Scratchpad/Themes/Dark.xaml` (brushes + header comment)
- Modify: `Scratchpad/MainWindow.xaml.cs` (`RenderPreview`, new `HighlightCodeBlocks`, `ClickedSourceOffset`)

**Interfaces:**
- Consumes: `CodeHighlighter.IsSupported/Tokenize`, `CodeTokenKind`, `SourceBlock.CodeLanguage`, existing `_previewNavigator.GetUnits()`, `PreviewSourceMap.Build`.
- Produces: brush keys `PreviewCode{Kind}Brush` for every `CodeTokenKind` except `Plain` (e.g. `PreviewCodeKeywordBrush`, `PreviewCodeJsonKeyBrush`), defined in **both** theme dictionaries.

| Kind | Light | Dark |
|---|---|---|
| Keyword | `#0000FF` | `#569CD6` |
| String | `#A31515` | `#D69D85` |
| Comment | `#006400` | `#57A64A` |
| Number | `#05603F` | `#B5CEA8` |
| TypeName | `#135A6E` | `#4EC9B0` |
| Preprocessor | `#545454` | `#9B9B9B` |
| MarkupDelimiter | `#0000FF` | `#A0A0A0` |
| MarkupElement | `#800000` | `#569CD6` |
| MarkupAttribute | `#A00000` | `#9CDCFE` |
| MarkupAttributeValue | `#0000FF` | `#CE9178` |
| MarkupEntity | `#A00000` | `#D69D85` |
| JsonKey | `#1C5490` | `#9CDCFE` |

(All light values are ≥4.9:1 on `#D3D3D3`, and all dark values ≥3.6:1 on `#2F2A19`, checked when this plan was written.)

- [ ] **Step 1: Brushes.** Add the table to both XAML files. Amend Light.xaml's header comment: the code-token brushes are the second exception, used by the preview in both themes.

- [ ] **Step 2: `private void HighlightCodeBlocks()`**, called in `RenderPreview` straight after `PreviewViewer.Markdown = …` (before fonts and theme). Behaviour:
  - Return if `PreviewViewer.Document` is null. Get `units = _previewNavigator.GetUnits()` and `map = PreviewSourceMap.Build(EditorTextBox.Text)`. If the counts differ, return (fail soft, same rule as `ClickedSourceOffset`).
  - For each index where `units[i]` is a `Paragraph` whose `Style` is `ReferenceEquals` the `CodeBlockStyleKey` style and `CodeHighlighter.IsSupported(map[i].CodeLanguage)`:
    - Rebuild the plain text from its inlines (`Run` → `Text`, `LineBreak` → `"\n"`). If any other inline type is present, skip this paragraph.
    - `Tokenize`. If null, skip. Otherwise build the new inline list first: per token, split `Text` on `'\n'`, add a `Run` per non-empty piece and a `LineBreak` between pieces. Non-Plain runs get `SetResourceReference(TextElement.ForegroundProperty, "PreviewCode" + kind + "Brush")`. Then `Inlines.Clear()` and add them.
  - Wrap each paragraph's work in `try/catch (Exception)` so one bad block leaves the rest alone.
  - Comment why rebuilding from the current inlines makes it idempotent: Markdig.Wpf keeps the same FlowDocument for unchanged text, so a re-shown preview arrives already highlighted.
  - Note: Plain runs carry no local Foreground, so they keep inheriting the paragraph ink that `ApplyPreviewTheme` sets. `ApplyPreviewTheme`/`ApplyPreviewFonts` need no change, because they only touch code *paragraphs* and inline-code runs (`CodeStyleKey`).

- [ ] **Step 3: Double-click precision.** In `ClickedSourceOffset`, before calling `LocateInBlock`: if the clicked run's parent is a code-block-style `Paragraph`, widen the search to the whole line. Concatenate the sibling `Run`s between the nearest `LineBreak`s on each side into `lineText`, and add the preceding siblings' text lengths to the offset to get `offsetInLine`. Then call `LocateInBlock(text, map[index], lineText, offsetInLine)`. This reproduces the pre-highlighting behaviour, where one run was one line. Put it in a small static helper `CodeLineAround(Run run, int offsetInRun, out int offsetInLine) → string` beside `IndexOfUnit`.

- [ ] **Step 4: Build:** `dotnet build Scratchpad.sln` → 0 errors; `dotnet test Scratchpad.Core.Tests` → green.

- [ ] **Step 5: Commit** `feat: syntax-highlight fenced code in the preview`

- [ ] **Step 6: Manual Windows checklist** (for the user, written into the hand-off message):
  1. A ` ```csharp ` block shows blue `for`/`int`, green comment, dark-red string; ` ```json `, ` ```js `, ` ```html ` blocks are coloured too.
  2. An untagged fence, a ` ```python ` fence, an indented block and inline `` `code` `` look exactly as before.
  3. Switch the theme in Settings with the preview showing: the code recolours immediately, light ↔ dark.
  4. Double-click the `i` in `int i = 0`: the editor opens with the caret right before that `i`.
  5. Toggle editor → preview → editor → preview without editing: the colours are still right and no text is duplicated or missing.
  6. A tagged block inside a list item and one inside a `>` quote are coloured, and blocks after them still double-click to the right place.
  7. Change the code font and size in Settings: the coloured code follows.

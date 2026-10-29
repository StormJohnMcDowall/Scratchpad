# Settings Panel & Font Family Choice — Design

## Intent

Let the user pick the editor's font, and separately the preview's body-text
and code-fence fonts, from fonts actually installed on their machine —
never a font that might not be present, since the target machine has no
admin rights to install one. This is the first feature to need a
persistent "Settings" surface in the app. It's explicitly meant to be a
foundation other future settings (the roadmap already includes a
dark-mode toggle) will likely join, not a one-off font dialog.

## Goals

- Independently choose: (1) the editor's font, (2) the preview's normal
  text font, (3) the preview's code font.
- The normal-text choice covers all non-code prose in the preview,
  including headings — not just plain paragraphs — so the preview reads
  as one consistent typeface rather than paragraphs changing while
  headings silently don't.
- The code-font choice covers all monospace code text in the preview,
  fenced blocks and inline `code spans` alike — not fenced blocks only.
- Font lists reflect what's actually installed on the machine, detected
  at runtime — never a hardcoded name that might not exist there.
- The editor and code-fence pickers are narrowed to monospace/fixed-pitch
  fonts only; the normal-text picker excludes symbol/decorative fonts but
  is otherwise unfiltered.
- A "Settings" entry point — a small toolbar button — opens a
  general-purpose Settings window, not one named or scoped as "Font
  Settings", so future settings have a natural home instead of spawning
  new ad hoc UI.
- Choices persist across restarts, like every other preference in this
  app.
- If the user never opens Settings, nothing about how the app looks or
  behaves changes — each picker's default must match today's actual
  rendered appearance exactly.

## Non-goals

- Choosing proportional-font properties for anything besides preview body
  text (e.g. no picker for heading sizes/weights).
- Any settings besides fonts in this increment — dark mode and the other
  roadmap items get their own design pass when their turn comes, even
  though they'll likely end up living in this same window.
- A plugin-style/dynamically-extensible settings framework. This
  increment organizes the window so another section can be added later
  without restructuring, but does not build generic infrastructure for
  sections it doesn't yet need.
- Per-document or per-file font settings — these are global, machine-wide
  preferences, consistent with every other setting in this app.

## Anti-goals

None identified beyond the non-goals above.

## Constraints

- No admin rights on the target machine — no font may be installed to
  satisfy a choice; only already-installed fonts are ever offered.
- Must fit the existing Core/Shell split: `Scratchpad.Core` stays
  `netstandard2.0` with no WPF dependency; anything touching WPF's font
  APIs lives in the `Scratchpad` (WPF) project, like every other
  platform-specific piece of this app.

## Decided (by the user)

- A Settings window, opened by a small button in the toolbar — not a
  tray-menu item, not controls visible in the toolbar all the time.
- Three independent font choices: editor, preview normal text, preview
  code-fence text — each its own persisted setting.
- Font lists are detected from the machine's installed fonts, not a
  hardcoded list.
- The editor and code-fence lists are narrowed to monospace fonts; the
  exact detection mechanism is left to the builder.
- The normal-text list's filtering approach is explicitly delegated to
  the builder's UX judgment.
- The Settings window is deliberately general-purpose: this feature adds
  a "Fonts" section to it, but the window itself is expected to grow more
  sections as more roadmap items (e.g. dark mode) get built.
- The normal-text font choice applies to the preview's headings as well
  as its paragraphs (confirmed after a builder check raised it as a real
  scope ambiguity — Markdig.Wpf styles each heading level as its own
  resource, so this was not a given).
- The code font choice applies to inline code spans as well as fenced
  code blocks (same reasoning — Markdig.Wpf keeps these as two separate
  style resources).

## Decided (my call, as the builder)

- **Monospace detection**: WPF's font APIs expose no direct "is this
  fixed-pitch" flag. Detect it by comparing two characters' glyph advance
  widths (via `GlyphTypeface`) at the same font — equal widths mean
  monospace. This is a per-installed-font check that can run once and be
  cached for the life of the process.
- **Symbol-font filtering for the normal-text list**: WPF's
  `GlyphTypeface.Symbol` flag identifies symbol/dingbat fonts (Wingdings,
  Webdings, Symbol, Marlett, etc.) directly — exclude any family whose
  glyph typeface reports `Symbol == true`. No other filtering; a long,
  alphabetically-sorted list is an acceptable outcome here, unlike the
  monospace lists where the point of filtering is to shortlist a usually-
  small, meaningfully different category of font.
- **Settings window behavior**: non-modal, and changes apply live — no
  OK/Cancel/Apply buttons. This matches the rest of the app's existing
  philosophy of no explicit save/confirm step (continuous autosave,
  immediate-toggle Always-on-Top). Closing the window just closes it;
  there is nothing to discard.
- **Visual grouping, not a framework**: the window groups its one current
  section under a "Fonts" heading, so a second section added later reads
  naturally — without building a generic plugin/section-registration
  mechanism now.
- **Defaults match current appearance**: the actual current default font
  families (editor: `Consolas`, already explicit in XAML; preview
  body/code: whatever Markdig.Wpf's bundled default style specifies) get
  inspected directly during planning, so a user who never opens Settings
  sees no visual change at all.
- **A persisted font that's no longer installed falls back to the
  default, the same way `FontSizeStore` already clamps a persisted value
  that's technically well-formed but out of range.** A font name is a
  string that deserializes fine even if that family isn't installed
  anymore — plausible on a no-admin machine where the installed-font set
  isn't fully within the user's control. The store's `Load()` validates
  each loaded family against the live installed-font list (the same list
  the Settings window's pickers are built from) and substitutes the
  default for any that aren't found, so a combo box never opens on a
  blank/invalid selection and the app never silently renders in a font
  that quietly stopped existing.

## What's left to the builder (the plan)

- Exact Settings window layout (XAML structure, window sizing, how the
  "Fonts" section is presented).
- The toolbar button's icon/label and placement.
- The precise glyph-comparison algorithm (which two characters to
  compare) for monospace detection, and any caching strategy.
- Exact persistence file/schema (a new store, following the existing
  `FontSizeStore`/`WindowPlacementStore` pattern).
- How a chosen font family applies to `EditorTextBox.FontFamily` versus
  the preview's styling. A builder check already confirmed, from
  Markdig.Wpf 0.5.0.1's resources, that it keys headings and code
  separately rather than through one cascading property: `Heading1StyleKey`
  through `Heading6StyleKey` and `DocumentStyleKey` for prose, and
  `CodeStyleKey` (inline) versus `CodeBlockStyleKey` (fenced) for code —
  all seven prose keys need the normal-text family, both code keys need
  the code family. The exact override mechanism (merged resource
  dictionary vs. per-style `Setter`) is still for the plan to decide.

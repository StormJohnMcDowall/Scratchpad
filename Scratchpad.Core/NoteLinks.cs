using System;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Scratchpad.Core
{
    /// <summary>
    /// Links in the note: which ones may be opened, and which one sits under a position in the
    /// markdown source (for Ctrl+click in the editor).
    /// </summary>
    public static class NoteLinks
    {
        /// <summary>
        /// True only for absolute http, https and mailto URLs. Anything else (file paths, UNC
        /// shares, javascript:, relative links, Markdig.Wpf's "#" fallback) must never reach the
        /// shell: text pasted into the note could otherwise run a program.
        /// </summary>
        public static bool IsOpenable(string url)
        {
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto;
        }

        /// <summary>
        /// The string to hand the shell for an openable URL: its parsed, escaped absolute form,
        /// so a quote or space that came from the note can't break out of the browser's quoted
        /// argument. Null when <see cref="IsOpenable"/> is false.
        /// </summary>
        public static string LaunchUrl(string url)
        {
            return IsOpenable(url) ? new Uri(url, UriKind.Absolute).AbsoluteUri : null;
        }

        /// <summary>
        /// The URL of the link whose source (from <c>[</c> to <c>)</c>, or the whole
        /// <c>&lt;…&gt;</c> autolink) contains <paramref name="index"/>, or null. Images aren't
        /// links. Email autolinks get "mailto:" as the preview gives them.
        /// </summary>
        public static string UrlAt(string markdown, int index)
        {
            var document = Markdown.Parse(markdown, NotePipeline.Create());
            foreach (var inline in document.Descendants<Inline>())
            {
                if (inline.Span.Start > index || inline.Span.End < index)
                {
                    continue;
                }

                if (inline is LinkInline link && !link.IsImage)
                {
                    return link.Url;
                }
                if (inline is AutolinkInline autolink)
                {
                    return autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url;
                }
            }

            return null;
        }
    }
}

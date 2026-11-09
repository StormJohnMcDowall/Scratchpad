using Markdig;
using Markdig.Extensions.EmphasisExtras;

namespace Scratchpad.Core
{
    /// <summary>
    /// The one Markdig pipeline for the note. The preview renders with it, and PreviewSourceMap
    /// and TaskListToggler parse with it, so all three must agree on what the markdown means
    /// (checkbox positions, block boundaries).
    /// </summary>
    public static class NotePipeline
    {
        /// <summary>
        /// Task lists plus ~~strikethrough~~. Only strikethrough out of the emphasis extras:
        /// the rest would turn a stray ~ ^ ++ or == in a note into subscript, superscript,
        /// underline or highlight.
        /// </summary>
        public static MarkdownPipeline Create()
        {
            return new MarkdownPipelineBuilder()
                .UseTaskLists()
                .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
                .Build();
        }
    }
}

using System.Linq;
using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;

namespace Scratchpad.Core
{
    public static class TaskListToggler
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();

        /// <summary>
        /// Flips the Nth task-list checkbox (0-based, in document order) between checked and
        /// unchecked, returning the updated markdown. Out-of-range indexes return the text
        /// unchanged.
        /// </summary>
        public static string Toggle(string markdown, int taskIndex)
        {
            var document = Markdown.Parse(markdown, Pipeline);

            // TaskList.Span is not reliably set to an absolute document offset by this Markdig
            // version, so the containing ParagraphBlock's span is used instead — its Span.Start
            // lands exactly on the task list item's '[' character.
            var taskParagraphs = document.Descendants<ParagraphBlock>()
                .Where(p => p.Inline?.FirstChild is TaskList)
                .ToList();

            if (taskIndex < 0 || taskIndex >= taskParagraphs.Count)
            {
                return markdown;
            }

            var stateIndex = taskParagraphs[taskIndex].Span.Start + 1;
            var isChecked = markdown[stateIndex] != ' ';
            var newChar = isChecked ? ' ' : 'x';

            return markdown.Substring(0, stateIndex) + newChar + markdown.Substring(stateIndex + 1);
        }
    }
}

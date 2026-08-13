using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Rendering;

namespace FlashStickNote.Controls;

public sealed class HttpLinkElementGenerator : LinkElementGenerator
{
    private static readonly Regex HttpRegex = new(
        @"\bhttps?://[\w\d\._/\-~%@()+:?&=#!]*[\w\d/]");

    public HttpLinkElementGenerator()
        : base(HttpRegex)
    {
    }
}

namespace FlashStickNote.Controls;

internal static class LineCutSelector
{
    public static (int StartOffset, int EndOffset) GetRange(string text, int caretOffset)
    {
        var offset = Math.Clamp(caretOffset, 0, text.Length);
        var start = offset;
        while (start > 0 && text[start - 1] is not '\r' and not '\n')
        {
            start--;
        }

        var end = offset;
        while (end < text.Length && text[end] is not '\r' and not '\n')
        {
            end++;
        }

        if (end < text.Length)
        {
            end++;
            if (text[end - 1] == '\r' && end < text.Length && text[end] == '\n')
            {
                end++;
            }
        }
        else if (start == end && start > 0)
        {
            start--;
            if (start > 0 && text[start] == '\n' && text[start - 1] == '\r')
            {
                start--;
            }
        }

        return (start, end);
    }
}

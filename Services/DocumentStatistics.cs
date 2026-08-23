namespace FlashStickNote.Services;

internal readonly record struct DocumentStatistics(int LineCount, int CharacterCount);

internal static class DocumentStatisticsCalculator
{
    public static DocumentStatistics Calculate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new DocumentStatistics(0, 0);
        }

        var lineCount = 1;
        var characterCount = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (!char.IsWhiteSpace(character))
            {
                characterCount++;
            }

            if (character == '\r')
            {
                lineCount++;
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }
            }
            else if (character == '\n')
            {
                lineCount++;
            }
        }

        return new DocumentStatistics(lineCount, characterCount);
    }
}

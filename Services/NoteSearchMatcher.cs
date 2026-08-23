using System.Text.RegularExpressions;

namespace FlashStickNote.Services;

internal sealed class NoteSearchMatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private readonly string _term;
    private readonly bool _wholeWord;
    private readonly StringComparison _comparison;
    private readonly Regex? _regex;

    public string? Error { get; }

    public NoteSearchMatcher(string? term, bool useRegex, bool wholeWord, bool matchCase)
    {
        _term = term?.Trim() ?? "";
        _wholeWord = wholeWord;
        _comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (!useRegex || _term.Length == 0)
        {
            return;
        }

        var pattern = wholeWord
            ? $"(?<![\\p{{L}}\\p{{N}}_])(?:{_term})(?![\\p{{L}}\\p{{N}}_])"
            : _term;
        var options = RegexOptions.CultureInvariant;
        if (!matchCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        try
        {
            _regex = new Regex(pattern, options, RegexTimeout);
        }
        catch (ArgumentException)
        {
            Error = "正则表达式无效";
        }
    }

    public bool IsMatch(string? value)
    {
        if (_term.Length == 0)
        {
            return true;
        }

        if (Error != null)
        {
            return false;
        }

        var text = value ?? "";
        if (_regex != null)
        {
            try
            {
                return _regex.IsMatch(text);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        if (!_wholeWord)
        {
            return text.IndexOf(_term, _comparison) >= 0;
        }

        var offset = 0;
        while (offset <= text.Length - _term.Length)
        {
            var index = text.IndexOf(_term, offset, _comparison);
            if (index < 0)
            {
                return false;
            }

            var atStart = index == 0 || !IsWordCharacter(text[index - 1]);
            var afterIndex = index + _term.Length;
            var atEnd = afterIndex == text.Length || !IsWordCharacter(text[afterIndex]);
            if (atStart && atEnd)
            {
                return true;
            }

            offset = index + 1;
        }

        return false;
    }

    private static bool IsWordCharacter(char character)
        => char.IsLetterOrDigit(character) || character == '_';
}

using FlashStickNote.Services;
using FlashStickNote.Controls;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

var defaultMatcher = new NoteSearchMatcher("note", useRegex: false, wholeWord: false, matchCase: false);
Assert(defaultMatcher.IsMatch("My NOTEbook"), "Default search should ignore case and match substrings.");

var wholeWordMatcher = new NoteSearchMatcher("note", useRegex: false, wholeWord: true, matchCase: false);
Assert(wholeWordMatcher.IsMatch("A note."), "Whole-word search should match a complete word.");
Assert(!wholeWordMatcher.IsMatch("notebook"), "Whole-word search should not match a longer word.");

var caseMatcher = new NoteSearchMatcher("Note", useRegex: false, wholeWord: false, matchCase: true);
Assert(caseMatcher.IsMatch("Note"), "Case-sensitive search should match identical case.");
Assert(!caseMatcher.IsMatch("note"), "Case-sensitive search should reject different case.");

var regexMatcher = new NoteSearchMatcher(@"^todo-\d+$", useRegex: true, wholeWord: false, matchCase: false);
Assert(regexMatcher.IsMatch("TODO-42"), "Regex search should honor IgnoreCase when case matching is off.");
Assert(!regexMatcher.IsMatch("todo-abc"), "Regex search should enforce the supplied pattern.");

var invalidRegexMatcher = new NoteSearchMatcher("[", useRegex: true, wholeWord: false, matchCase: false);
Assert(!string.IsNullOrEmpty(invalidRegexMatcher.Error), "Invalid regular expressions should report an error.");
Assert(!invalidRegexMatcher.IsMatch("anything"), "Invalid regular expressions must not match or throw.");

var firstLine = LineCutSelector.GetRange("one\r\ntwo\r\nthree", 1);
Assert(firstLine == (0, 5), "Cutting the first line should include its CRLF delimiter.");

var middleLine = LineCutSelector.GetRange("one\r\ntwo\r\nthree", 6);
Assert(middleLine == (5, 10), "Cutting a middle line should include its CRLF delimiter.");

var finalLine = LineCutSelector.GetRange("one\r\ntwo", 6);
Assert(finalLine == (5, 8), "Cutting the final line should select through the document end.");

var finalEmptyLine = LineCutSelector.GetRange("one\r\n", 5);
Assert(finalEmptyLine == (3, 5), "Cutting a final empty line should remove the preceding CRLF.");

Console.WriteLine("All NoteSearchMatcher tests passed.");

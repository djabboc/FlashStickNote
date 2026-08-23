using FlashStickNote.Services;

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

Console.WriteLine("All NoteSearchMatcher tests passed.");

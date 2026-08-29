using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlashStickNote;
using FlashStickNote.Controls;
using FlashStickNote.Models;
using FlashStickNote.Services;
using FlashStickNote.ViewModels;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            RunLogicTests();
            RunStorageTests();
            RunWindowInteractionTests();
            Console.WriteLine("All FlashStickNote tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void RunLogicTests()
    {
        var defaultMatcher = new NoteSearchMatcher("note", false, false, false);
        Assert(defaultMatcher.IsMatch("My NOTEbook"), "Default search should ignore case and match substrings.");

        var wholeWordMatcher = new NoteSearchMatcher("note", false, true, false);
        Assert(wholeWordMatcher.IsMatch("A note."), "Whole-word search should match a complete word.");
        Assert(!wholeWordMatcher.IsMatch("notebook"), "Whole-word search should not match a longer word.");

        var caseMatcher = new NoteSearchMatcher("Note", false, false, true);
        Assert(caseMatcher.IsMatch("Note"), "Case-sensitive search should match identical case.");
        Assert(!caseMatcher.IsMatch("note"), "Case-sensitive search should reject different case.");

        var regexMatcher = new NoteSearchMatcher(@"^todo-\d+$", true, false, false);
        Assert(regexMatcher.IsMatch("TODO-42"), "Regex search should honor IgnoreCase when case matching is off.");
        Assert(!regexMatcher.IsMatch("todo-abc"), "Regex search should enforce the supplied pattern.");

        var invalidRegexMatcher = new NoteSearchMatcher("[", true, false, false);
        Assert(!string.IsNullOrEmpty(invalidRegexMatcher.Error), "Invalid regular expressions should report an error.");
        Assert(!invalidRegexMatcher.IsMatch("anything"), "Invalid regular expressions must not match or throw.");

        Assert(LineCutSelector.GetRange("one\r\ntwo\r\nthree", 1) == (0, 5), "Cutting the first line should include its CRLF delimiter.");
        Assert(LineCutSelector.GetRange("one\r\ntwo\r\nthree", 6) == (5, 10), "Cutting a middle line should include its CRLF delimiter.");
        Assert(LineCutSelector.GetRange("one\r\ntwo", 6) == (5, 8), "Cutting the final line should select through the document end.");
        Assert(LineCutSelector.GetRange("one\r\n", 5) == (3, 5), "Cutting a final empty line should remove the preceding CRLF.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var legacyConfig = JsonSerializer.Deserialize<AppConfig>("{\"showLineFeed\":true}", options);
        Assert(legacyConfig?.ShowEndOfLine == true, "Legacy line-feed configuration should migrate to showEndOfLine.");

        var serializedConfig = JsonSerializer.Serialize(new AppConfig { ShowEndOfLine = true }, options);
        Assert(serializedConfig.Contains("\"showEndOfLine\":true", StringComparison.Ordinal), "New configuration should serialize showEndOfLine.");
        Assert(!serializedConfig.Contains("showLineFeed", StringComparison.Ordinal), "New configuration should not serialize legacy line-feed settings.");

        var defaultFontConfig = new AppConfig();
        Assert(defaultFontConfig.FontFamily == "Lucida Fax", "The default font should prefer Lucida Fax.");
        Assert(defaultFontConfig.FontFallbackFamilies.SequenceEqual(new[] { "Microsoft JhengHei", "Arial", "Microsoft YaHei" }),
            "The default font fallbacks must preserve the configured priority.");
        var serializedFontConfig = JsonSerializer.Serialize(defaultFontConfig, options);
        Assert(serializedFontConfig.Contains("\"fontFallbackFamilies\":[\"Microsoft JhengHei\",\"Arial\",\"Microsoft YaHei\"]", StringComparison.Ordinal),
            "Font fallback families should serialize as an ordered configuration array.");
        var defaultShortcutConfig = new ShortcutConfig();
        Assert(defaultShortcutConfig.FocusSearch == "Ctrl+F", "Ctrl+F should be the default search-focus shortcut.");
        Assert(defaultShortcutConfig.FocusNoteList == "Ctrl+B", "Ctrl+B should be the default note-list shortcut.");
        Assert(defaultShortcutConfig.CyclePinnedNotes == "F2", "F2 should be the default pinned-note cycle shortcut.");
        Assert(defaultShortcutConfig.TogglePin == "Ctrl+P", "Ctrl+P should be the default pin toggle shortcut.");
        Assert(new AppConfig().PinnedNotes.Count == 0, "Pinned-note configuration should default to an empty list.");
        var defaultCaretConfig = new AppConfig();
        Assert(defaultCaretConfig.CaretBlinkInterval == 530, "The default caret blink interval should be 530ms.");
        Assert(Math.Abs(defaultCaretConfig.CaretOpacity - 0.8) < 0.0001, "The default caret opacity should be 80%.");
        Assert(Math.Abs(NoteTextEditor.NormalizeCaretOpacity(0.35) - 0.35) < 0.0001, "Valid caret opacity should be preserved.");
        Assert(NoteTextEditor.NormalizeCaretOpacity(-0.2) == 0, "Caret opacity should not become negative.");
        Assert(NoteTextEditor.NormalizeCaretOpacity(1.2) == 1, "Caret opacity should not exceed 100%.");
        Assert(Math.Abs(NoteTextEditor.NormalizeCaretOpacity(double.NaN) - 0.8) < 0.0001, "Invalid caret opacity should use the default.");
        Assert(NoteTextEditor.NormalizeCaretBlinkInterval(0) == 0, "A zero caret blink interval should keep the caret visible.");
        Assert(NoteTextEditor.NormalizeCaretBlinkInterval(40) == 100, "Positive caret blink intervals should have a usable lower bound.");
        Assert(NoteTextEditor.NormalizeCaretBlinkInterval(2400) == 2000, "Caret blink intervals should have an upper bound.");

        Assert(DocumentStatisticsCalculator.Calculate("") == new DocumentStatistics(0, 0), "Empty content should have zero lines and characters.");
        var mixedLineEndings = DocumentStatisticsCalculator.Calculate("abc\r\n你好 \t!\rb\n");
        Assert(mixedLineEndings.LineCount == 4, "CRLF, CR, and LF should each count as one logical line break.");
        Assert(mixedLineEndings.CharacterCount == 7, "Character count should exclude whitespace and include Chinese characters and punctuation.");
    }

    private static void RunStorageTests()
    {
        var root = Path.Combine(Path.GetTempPath(), $"FlashStickNote-StorageTests-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var storage = new NoteStorage(root, "notes", "txt");
            var note = new Note { Title = "First title", Content = "First content" };

            Assert(storage.Save(note), "Saving a new text note should succeed.");
            var originalPath = note.StoredFileName ?? throw new InvalidOperationException("The saved note must have a path.");
            Assert(File.Exists(originalPath), "The first text note file should exist.");

            note.Title = "Renamed title";
            Assert(storage.Save(note), "Renaming a text note should succeed.");
            var renamedPath = note.StoredFileName ?? throw new InvalidOperationException("The renamed note must have a path.");
            Assert(!string.Equals(originalPath, renamedPath, StringComparison.OrdinalIgnoreCase), "Renaming should choose a new text-file path.");
            Assert(!File.Exists(originalPath), "The original file should be removed after the replacement is safely written.");
            Assert(File.ReadAllText(renamedPath) == "First content", "The renamed note must retain its content.");

            Directory.CreateDirectory(Path.Combine(storage.Dir, "Blocked.txt"));
            note.Title = "Blocked";
            Assert(!storage.Save(note), "Saving to a path occupied by a directory should fail.");
            Assert(string.Equals(note.StoredFileName, renamedPath, StringComparison.OrdinalIgnoreCase), "A failed save must retain the original stored path.");
            Assert(File.Exists(renamedPath), "A failed save must retain the original file.");
            Assert(!Directory.EnumerateFiles(storage.Dir, "*.tmp").Any(), "A failed save must clean up its temporary file.");

            var duplicateTitleContent = new Note { Title = "XXX", Content = "XXX" };
            Assert(storage.Save(duplicateTitleContent), "Saving content equal to its title should succeed.");
            var duplicatePath = duplicateTitleContent.StoredFileName ?? throw new InvalidOperationException("The duplicate-title note must have a path.");
            Assert(storage.LoadFile(duplicatePath)?.Content == "XXX", "Loading text content equal to its title must preserve the content.");

            duplicateTitleContent.Content = "XXX\r\nYYY";
            Assert(storage.Save(duplicateTitleContent), "Saving a second line after duplicate-title content should succeed.");
            Assert(storage.LoadFile(duplicatePath)?.Content == "XXX\r\nYYY", "Loading multi-line content beginning with its title must preserve every line.");

            Assert(storage.Delete(note), "Deleting a stored note should move it to the recycle directory.");
            Assert(note.StoredFileName == null, "A successfully recycled note should clear its stored path.");
            Assert(!File.Exists(renamedPath), "The active note file should be removed after recycling.");
            Assert(Directory.EnumerateFiles(storage.RecycleDir, "Renamed title.txt").Any(), "The recycled note should exist in the recycle directory.");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static void RunWindowInteractionTests()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            RunWindowInteractionTest(hideTitleBar: false);
            RunWindowInteractionTest(hideTitleBar: true);
            RunWindowBoundsLiveSaveTest();
            RunWindowBoundsPersistenceTest();
        }
        finally
        {
            app.Shutdown();
        }
    }

    private static void RunWindowInteractionTest(bool hideTitleBar)
    {
        WriteTestConfiguration(hideTitleBar);
        var window = new MainWindow();
        try
        {
            window.Show();
            window.UpdateLayout();
            Assert(window.WindowStartupLocation == WindowStartupLocation.Manual,
                "Configured window coordinates should use manual startup positioning.");
            Assert(Math.Abs(window.Width - 940) < 0.1 && Math.Abs(window.Height - 640) < 0.1,
                "Configured window dimensions should be applied as absolute values.");
            Assert(Math.Abs(window.Left - 96) < 0.1 && Math.Abs(window.Top - 84) < 0.1,
                "Configured window coordinates should be applied as absolute values.");

            var menuBar = FindNamed<Border>(window, "MenuBarBackground");
            var fileMenuButton = FindNamed<Button>(window, "FileMenuButton");
            var searchPanel = FindNamed<Border>(window, "SearchPanel");
            var noteList = FindNamed<ListBox>(window, "NoteList");
            Assert(menuBar.ActualWidth > fileMenuButton.ActualWidth, "The menu bar must have a blank area outside the File button.");

            var blankMenuPoint = menuBar.TranslatePoint(new Point(menuBar.ActualWidth - 4, menuBar.ActualHeight / 2), window);
            var menuBlankHit = window.InputHitTest(blankMenuPoint) as DependencyObject;
            Assert(!HasAncestor<Button>(menuBlankHit), "Clicking the right blank menu-bar area must not target the File button.");
            Assert(MainWindow.CanDragFrom(menuBlankHit), "The menu-bar blank area must be draggable in both title-bar modes.");
            InvokePrivate(window, "FileMenuButton_Click", fileMenuButton, new RoutedEventArgs());
            var fileMenu = fileMenuButton.ContextMenu ?? throw new InvalidOperationException("The File button must have a menu.");
            Assert(fileMenu.IsOpen, "The File menu must open from the File button.");
            fileMenu.IsOpen = false;

            var searchBlankPoint = searchPanel.TranslatePoint(new Point(searchPanel.ActualWidth - 4, searchPanel.ActualHeight - 4), window);
            var searchBlankHit = window.InputHitTest(searchBlankPoint) as DependencyObject;
            Assert(!HasAncestor<Button>(searchBlankHit), "Clicking below the search controls must not target the File button.");

            var blankListPoint = noteList.TranslatePoint(new Point(noteList.ActualWidth / 2, noteList.ActualHeight - 4), window);
            var listBlankHit = window.InputHitTest(blankListPoint) as DependencyObject;
            Assert(MainWindow.CanDragFrom(listBlankHit), $"The note-list blank area must be draggable in both title-bar modes. Hit: {DescribeAncestors(listBlankHit)}");

            Assert(!MainWindow.CanOpenNoteContextMenu(null), "Right-clicking blank list space must not open the note context menu.");

            var viewModel = (MainViewModel)window.DataContext;
            var editor = FindNamed<NoteTextEditor>(window, "ContentBox");
            var searchBox = FindNamed<TextBox>(window, "SearchBox");

            viewModel.NewNote();
            var pinnedA = viewModel.SelectedNote ?? throw new InvalidOperationException("Pin test requires note A.");
            pinnedA.Title = "Pinned A";
            pinnedA.UpdatedAt = DateTime.Now.AddMinutes(-1);
            viewModel.NewNote();
            var pinnedB = viewModel.SelectedNote ?? throw new InvalidOperationException("Pin test requires note B.");
            pinnedB.Title = "Pinned B";
            viewModel.NewNote();
            var normalNote = viewModel.SelectedNote ?? throw new InvalidOperationException("Pin test requires a normal note.");
            normalNote.Title = "Normal note";
            viewModel.TogglePin(pinnedB);
            viewModel.TogglePin(pinnedA);
            Assert(viewModel.Notes.Take(2).All(note => note.IsPinned), "Pinned notes must sort before ordinary notes.");
            Assert(ConfigService.LoadConfig().PinnedNotes.Count == 2, "Pin changes must persist two note identifiers in configuration.");
            Assert(pinnedA.StoredFileName != null && pinnedB.StoredFileName != null,
                "Pinned notes must be written before their pin keys are persisted.");
            var restartConfig = ConfigService.LoadConfig();
            var restarted = new MainViewModel(new NoteStorage(
                ConfigService.BaseDir,
                restartConfig.NotesDir,
                restartConfig.NotesFormat));
            try
            {
                Assert(restarted.Notes.Single(note => note.Id == pinnedA.Id).IsPinned &&
                    restarted.Notes.Single(note => note.Id == pinnedB.Id).IsPinned,
                    "Pinned notes must remain pinned after reloading the application state.");
            }
            finally
            {
                restarted.Dispose();
            }

            viewModel.SelectedNote = normalNote;
            InvokePrivate(window, "ToggleSelectedNotePin");
            Assert(normalNote.IsPinned, "Ctrl+P command must pin the currently selected note.");
            InvokePrivate(window, "ToggleSelectedNotePin");
            Assert(!normalNote.IsPinned, "Ctrl+P command must remove the current pin on a second invocation.");

            viewModel.SelectedNote = normalNote;
            viewModel.SearchText = "no matching note";
            editor.Focus();
            InvokePrivate(window, "FocusNoteList");
            PumpDispatcher(TimeSpan.FromMilliseconds(50));
            Assert(viewModel.SearchText == "", "Focusing the note list must clear a filter that hides the current note.");
            Assert(noteList.IsKeyboardFocusWithin, "Ctrl+B behavior must move keyboard focus to the note list.");
            Assert(noteList.ItemContainerGenerator.ContainerFromItem(normalNote) is ListBoxItem listItem && listItem.IsKeyboardFocused,
                "Ctrl+B behavior must focus the current ListBoxItem rather than another control.");

            var selectedIndex = noteList.SelectedIndex;
            var expectedNext = noteList.Items[Math.Min(selectedIndex + 1, noteList.Items.Count - 1)] as Note;
            var downKey = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(window),
                Environment.TickCount,
                Key.Down)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };
            InvokePrivate(window, "NoteList_PreviewKeyDown", noteList, downKey);
            Assert(ReferenceEquals(viewModel.SelectedNote, expectedNext),
                "Down arrow in list mode must browse the next visible note.");
            Assert(noteList.IsKeyboardFocusWithin,
                "List navigation must retain keyboard focus in the note list.");

            InvokePrivate(window, "FocusNoteList");
            Assert(editor.TextArea.IsKeyboardFocused,
                "Pressing Ctrl+B from the list must return to the previous editor focus.");
            searchBox.Text = "find me";
            InvokePrivate(window, "FocusSearch");
            Assert(searchBox.IsKeyboardFocused && searchBox.SelectedText == "find me",
                "Ctrl+F behavior must focus and select the search text.");

            viewModel.SelectedNote = pinnedA;
            editor.Focus();
            InvokePrivate(window, "CyclePinnedNotes");
            PumpDispatcher(TimeSpan.FromMilliseconds(50));
            Assert(ReferenceEquals(viewModel.SelectedNote, pinnedB), "F2 must cycle from the last pinned note to the first pinned note.");
            Assert(editor.TextArea.IsKeyboardFocused, "F2 must retain editor focus after changing the selected pinned note.");
            Assert(noteList.ItemContainerGenerator.ContainerFromItem(pinnedB) != null,
                "F2 must scroll the target pinned note into the visible list range.");

            viewModel.NewNote();
            var undoNote = viewModel.SelectedNote ?? throw new InvalidOperationException("Undo test requires a selected note.");
            editor.Document.Insert(0, "Undo this text");
            Assert(undoNote.Content == "Undo this text", "Editing note A should update its content.");

            var otherUndoNote = new Note { Title = "Undo target" };
            viewModel.Notes.Add(otherUndoNote);
            viewModel.SelectedNote = otherUndoNote;
            editor.Document.Insert(0, "Other note text");
            viewModel.SelectedNote = undoNote;
            editor.Undo();
            Assert(editor.Text == "", "Undo history must survive switching away from and back to a note.");
            Assert(undoNote.Content == "", "Undo after returning must update the original note.");

            viewModel.NewNote();
            var emptyDraft = viewModel.SelectedNote ?? throw new InvalidOperationException("New note should select an empty draft.");
            var noteCount = viewModel.Notes.Count;
            ((UIElement)listBlankHit!).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
            {
                RoutedEvent = UIElement.PreviewMouseRightButtonDownEvent,
            });
            Assert(viewModel.Notes.Count == noteCount && viewModel.Notes.Contains(emptyDraft), "Right-clicking blank list space must not remove an empty draft.");
            Assert(ReferenceEquals(viewModel.SelectedNote, emptyDraft), "Right-clicking blank list space must preserve the selected empty draft.");

            var otherNote = new Note { Title = "Context target" };
            viewModel.Notes.Add(otherNote);
            noteList.ScrollIntoView(otherNote);
            window.UpdateLayout();
            PumpDispatcher(TimeSpan.FromMilliseconds(100));
            var otherItem = (ListBoxItem?)noteList.ItemContainerGenerator.ContainerFromItem(otherNote)
                ?? throw new InvalidOperationException("The context-menu target must have a list item.");
            var rightClick = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
            {
                RoutedEvent = UIElement.PreviewMouseRightButtonDownEvent,
                Source = otherItem,
            };
            InvokePrivate(window, "NoteList_PreviewMouseRightButtonDown", noteList, rightClick);
            Assert(otherItem.IsSelected, "Right-clicking a note must select the context-menu target.");
            viewModel.SelectedNote = otherNote;
            Assert(viewModel.Notes.Contains(emptyDraft), "Right-clicking another note must not remove an empty draft.");
            Assert(ReferenceEquals(viewModel.SelectedNote, otherNote), "Selecting a context-menu target must keep that target selected.");

            editor.ConfigureCaret("line", 2, Brushes.Black, 0.35, 100);
            editor.Focus();
            Assert(editor.TextArea.IsKeyboardFocused, "The caret blink test requires editor keyboard focus.");
            Assert(Math.Abs(GetCustomCaretOpacity(editor) - 0.35) < 0.0001, "Configured caret opacity should be applied to the custom brush.");
            PumpDispatcher(TimeSpan.FromMilliseconds(170));
            Assert(!GetCustomCaretVisibility(editor), "A focused caret with a 100ms interval should become hidden after one timer tick.");

            editor.Document.Insert(0, "Blink");
            editor.TextArea.Caret.Offset = editor.Document.TextLength;
            Assert(GetCustomCaretVisibility(editor), "Moving the caret should make it visible immediately.");

            editor.ConfigureCaret("line", 2, Brushes.Black, 0.35, 0);
            PumpDispatcher(TimeSpan.FromMilliseconds(170));
            Assert(GetCustomCaretVisibility(editor), "A zero caret blink interval should keep the focused caret visible.");
        }
        finally
        {
            InvokePrivate(window, "ExitApp");
        }
    }
    private static void RunWindowBoundsLiveSaveTest()
    {
        WriteTestConfiguration(hideTitleBar: false, rememberWindowBounds: true);
        var window = new MainWindow();
        try
        {
            window.Show();
            window.UpdateLayout();
            window.Width = 1000;
            window.Height = 720;
            window.Left = 200;
            window.Top = 170;
            window.UpdateLayout();
            PumpDispatcher(TimeSpan.FromMilliseconds(800));

            var savedConfig = ConfigService.LoadConfig();
            Assert(Math.Abs(savedConfig.WindowWidth - 1000) < 0.1 && Math.Abs(savedConfig.WindowHeight - 720) < 0.1,
                "Moving or resizing the window should save dimensions without requiring exit.");
            Assert(savedConfig.WindowLeft is { } left && savedConfig.WindowTop is { } top &&
                Math.Abs(left - 200) < 0.1 && Math.Abs(top - 170) < 0.1,
                "Moving or resizing the window should save coordinates without requiring exit.");
        }
        finally
        {
            InvokePrivate(window, "ExitApp");
        }
    }


    private static void RunWindowBoundsPersistenceTest()
    {
        WriteTestConfiguration(hideTitleBar: false, rememberWindowBounds: true);
        var window = new MainWindow();
        try
        {
            window.Show();
            window.UpdateLayout();
            window.Width = 980;
            window.Height = 700;
            window.Left = 180;
            window.Top = 160;
            window.UpdateLayout();
        }
        finally
        {
            InvokePrivate(window, "ExitApp");
        }

        var persistedConfig = ConfigService.LoadConfig();
        Assert(Math.Abs(persistedConfig.WindowWidth - 980) < 0.1 && Math.Abs(persistedConfig.WindowHeight - 700) < 0.1,
            "Remembered window dimensions should be saved on normal exit.");
        Assert(persistedConfig.WindowLeft is { } left && persistedConfig.WindowTop is { } top &&
            Math.Abs(left - 180) < 0.1 && Math.Abs(top - 160) < 0.1,
            "Remembered window coordinates should be saved on normal exit.");
    }

    private static void WriteTestConfiguration(bool hideTitleBar, bool rememberWindowBounds = false)
    {
        var notesDir = Path.Combine(Path.GetTempPath(), "FlashStickNote-WpfTests");
        if (Directory.Exists(notesDir))
        {
            Directory.Delete(notesDir, true);
        }

        var config = new AppConfig
        {
            AllowMultiInstance = true,
            ConfirmDelete = false,
            HideTitleBar = hideTitleBar,
            WindowWidth = 940,
            WindowHeight = 640,
            WindowLeft = 96,
            WindowTop = 84,
            RememberWindowBounds = rememberWindowBounds,
            NotesDir = notesDir,
        };
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "conf.json"), JsonSerializer.Serialize(config, options));
        var shortcuts = new ShortcutConfig
        {
            ToggleWindow = "Ctrl+Shift+F24",
            NewNote = "Ctrl+Shift+F23",
            HideWindow = "Ctrl+Shift+F22",
        };
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "shortcut.json"), JsonSerializer.Serialize(shortcuts, options));
    }
    private static bool GetCustomCaretVisibility(NoteTextEditor editor)
    {
        var renderer = typeof(NoteTextEditor).GetField("_caretRenderer", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(editor)
            ?? throw new InvalidOperationException("The editor must keep its custom caret renderer.");
        var isVisible = renderer.GetType().GetProperty("IsVisible", BindingFlags.Instance | BindingFlags.Public)?.GetValue(renderer)
            ?? throw new InvalidOperationException("The custom caret renderer must expose its visibility.");
        return (bool)isVisible;
    }

    private static double GetCustomCaretOpacity(NoteTextEditor editor)
    {
        var renderer = typeof(NoteTextEditor).GetField("_caretRenderer", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(editor)
            ?? throw new InvalidOperationException("The editor must keep its custom caret renderer.");
        var brush = renderer.GetType().GetProperty("Brush", BindingFlags.Instance | BindingFlags.Public)?.GetValue(renderer) as Brush
            ?? throw new InvalidOperationException("The custom caret renderer must expose its brush.");
        return brush.Opacity;
    }

    private static T FindNamed<T>(FrameworkElement root, string name) where T : FrameworkElement
        => root.FindName(name) as T ?? throw new InvalidOperationException($"Missing named element: {name}");

    private static bool HasAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node != null)
        {
            if (node is T)
            {
                return true;
            }

            node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : null;
        }

        return false;
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static string DescribeAncestors(DependencyObject? node)
    {
        var names = new List<string>();
        while (node != null)
        {
            names.Add(node.GetType().Name);
            node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : null;
        }

        return string.Join(" > ", names);
    }

    private static void InvokePrivate(object target, string name, params object[] arguments)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing private method: {name}");
        method.Invoke(target, arguments);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

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
            window.UpdateLayout();
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
        }
        finally
        {
            InvokePrivate(window, "ExitApp");
        }
    }

    private static void WriteTestConfiguration(bool hideTitleBar)
    {
        var config = new AppConfig
        {
            AllowMultiInstance = true,
            ConfirmDelete = false,
            HideTitleBar = hideTitleBar,
            NotesDir = Path.Combine(Path.GetTempPath(), "FlashStickNote-WpfTests"),
        };
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "conf.json"), JsonSerializer.Serialize(config, options));
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "shortcut.json"), "{\"toggleWindow\":\"Ctrl+Shift+F24\",\"newNote\":\"Ctrl+Shift+F23\",\"hideWindow\":\"Ctrl+Shift+F22\",\"fontZoom\":\"Ctrl+Wheel\",\"toggleWordWrap\":\"Alt+Z\"}");
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

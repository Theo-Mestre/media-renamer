using MediaRenamer.Core.Services;
using MediaRenamer.Theme;
using MediaRenamer.UI;
using MediaRenamer.UI.Screens;
using Spectre.Console;

var themes = ThemeManager.LoadAllThemes();
var savedState = SessionStorage.Load();

var initialTheme = themes.FirstOrDefault(t =>
    string.Equals(t.Name, savedState.LastThemeName, StringComparison.OrdinalIgnoreCase))
    ?? themes.First();

var session = new AppSession(initialTheme);

if (savedState.LastPipelineConfig != null)
{
    session.PipelineConfig.CopyFrom(savedState.LastPipelineConfig);
}

// 1. CLI argument takes priority; otherwise restore last saved workspace from session.json
if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
{
    string candidatePath = args[0].Trim().Trim('"');

    if (Directory.Exists(candidatePath))
    {
        session.RootDirectory = Path.GetFullPath(candidatePath);
        session.RescanWorkspace();
    }
    else if (File.Exists(candidatePath))
    {
        string? parentDir = Path.GetDirectoryName(Path.GetFullPath(candidatePath));
        if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
        {
            session.RootDirectory = parentDir;
            session.RescanWorkspace();
            string targetFull = Path.GetFullPath(candidatePath);
            session.SelectedFiles = session.AllFiles
                .Where(f => string.Equals(f.FullPath, targetFull, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
else if (!string.IsNullOrWhiteSpace(savedState.LastRootDirectory) && Directory.Exists(savedState.LastRootDirectory))
{
    session.RootDirectory = Path.GetFullPath(savedState.LastRootDirectory);
    session.RescanWorkspace();
}

WindowStyler.ApplyWindowTheme(session.CurrentTheme, " ");

bool isOnMainMenu = false;
WindowStyler.StartResizeWatcher(() =>
{
    if (isOnMainMenu)
    {
        Console.SetCursorPosition(0, 0);
        AnsiConsole.Clear();
        ThemeRenderer.RenderDashboard(session);
        AnsiConsole.MarkupLine($"  [{session.CurrentTheme.MutedHex}](Viewport resized to {Console.WindowWidth}x{Console.WindowHeight} — press any arrow key to refresh menu)[/]");
    }
});

while (true)
{
    AnsiConsole.Clear();
    ThemeRenderer.RenderDashboard(session);

    var theme = session.CurrentTheme;
    var menuOptions = new List<MenuOption<int>>
    {
        new(1, "Set Target Folder & Scan", '1'),
        new(2, "Select Files (Multi-Select / Audit)", '2'),
        new(3, "Configure Rename Stack & Auto-Numbering", '3'),
        new(4, "Preview & Apply Rename", '4')
    };

    if (session.SelectedFiles.Count > 0)
        menuOptions.Add(new(5, $"[{theme.MutedHex}]Clear Selection ({session.SelectedFiles.Count})[/]", '5'));

    if (session.Engine.CanUndo)
        menuOptions.Add(new(6, $"[{theme.WarningHex}]Undo Last Rename ({session.Engine.LastBatchCount} files)[/]", '6'));

    menuOptions.Add(new(7, "Switch Visual Theme", '7'));
    menuOptions.Add(new(8, "Exit", '8'));

    isOnMainMenu = true;
    int choice = InteractiveMenu.Prompt(
        $"  [{theme.CategoryLabelHex}]Actions[/]",
        menuOptions,
        theme,
        pageSize: WindowStyler.GetResponsivePageSize(reservedHeaderRows: 14, minRows: 5));

    isOnMainMenu = false;
    AnsiConsole.Clear();

    switch (choice)
    {
        case 1:
            AnsiConsole.WriteLine();
            string inputPath = AnsiConsole.Prompt(
                new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Folder Path[/] [{theme.MutedHex}]>[/]")
                    .DefaultValue(session.RootDirectory)).Trim('"');

            if (Directory.Exists(inputPath))
            {
                session.RootDirectory = Path.GetFullPath(inputPath);
                session.RescanWorkspace();
                session.SaveState();
                AnsiConsole.MarkupLine($"  [{theme.SuccessHex}]✓ Scanned {session.AllFiles.Count} media files across all subfolders.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"  [{theme.ErrorHex}]✗ Directory does not exist.[/]");
            }
            ThemeRenderer.Pause(theme);
            break;

        case 2:
            AnsiConsole.WriteLine();
            FileSelectionScreen.Show(session);
            break;

        case 3:
            PipelineConfigScreen.Show(session);
            break;

        case 4:
            AnsiConsole.WriteLine();
            PreviewScreen.Show(session);
            break;

        case 5:
            session.ClearSelection();
            break;

        case 6:
            AnsiConsole.WriteLine();
            if (AnsiConsole.Confirm($"  [{theme.WarningHex}]Revert the last batch of {session.Engine.LastBatchCount} renamed file(s)?[/]"))
            {
                var undoResult = session.Engine.UndoLastBatch();
                AnsiConsole.MarkupLine($"  [{theme.SuccessHex}]✓ Reverted {undoResult.Succeeded} file(s).[/]");
                foreach (string err in undoResult.Errors)
                {
                    AnsiConsole.MarkupLine($"  [{theme.ErrorHex}]✗ {Markup.Escape(err)}[/]");
                }
                session.RescanWorkspace();
            }
            ThemeRenderer.Pause(theme);
            break;

        case 7:
            AnsiConsole.WriteLine();
            themes = ThemeManager.LoadAllThemes();
            var themeChoices = themes
                .Select((t, i) => new MenuOption<int>(i, $"{t.Name} [{t.MutedHex}]({t.BackgroundHex})[/]", i < 9 ? (char)('1' + i) : null))
                .Append(new MenuOption<int>(-1, "Cancel (Esc)", '0', IsBackOption: true))
                .ToList();

            int themeIdx = InteractiveMenu.Prompt($"  [{theme.CategoryLabelHex}]Themes[/]", themeChoices, theme, backValue: -1, allowEscape: true);
            if (themeIdx >= 0)
            {
                session.CurrentTheme = themes[themeIdx];
                session.SaveState();
                WindowStyler.ApplyWindowTheme(session.CurrentTheme, " ");
            }
            break;

        case 8:
            session.SaveState();
            return;
    }
}
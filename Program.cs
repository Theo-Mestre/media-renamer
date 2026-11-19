using MediaRenamer.Theme;
using MediaRenamer.UI;
using MediaRenamer.UI.Screens;
using Spectre.Console;

var themes = ThemeManager.LoadAllThemes();
var session = new AppSession(themes.First());

// Handle CLI argument launch: MediaRenamer.exe "X:\Fan Art"
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
    var menuChoices = new List<int> { 1, 2, 3, 4 };
    if (session.Engine.CanUndo)
        menuChoices.Add(5);
    menuChoices.AddRange([6, 7]);

    isOnMainMenu = true;
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<int>()
            .Title($"  [{theme.CategoryLabelHex}]Actions[/]")
            .PageSize(WindowStyler.GetResponsivePageSize(reservedHeaderRows: 14, minRows: 5))
            .HighlightStyle(new Style(
                foreground: theme.ToColor(theme.BackgroundHex),
                background: theme.ToColor(theme.PrimaryHex),
                decoration: Decoration.Bold))
            .UseConverter(id => id switch
            {
                1 => "  Set Target Folder & Scan",
                2 => "  Select Files (Multi-Select)",
                3 => "  Configure Rename Rules & Auto-Numbering",
                4 => "  Preview & Apply Rename",
                5 => $"  [{theme.WarningHex}]Undo Last Rename ({session.Engine.LastBatchCount} files)[/]",
                6 => "  Switch Visual Theme",
                7 => "  Exit",
                _ => string.Empty
            })
            .AddChoices(menuChoices));

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

        case 6:
            AnsiConsole.WriteLine();
            themes = ThemeManager.LoadAllThemes();
            session.CurrentTheme = AnsiConsole.Prompt(
                new SelectionPrompt<AppTheme>()
                    .Title($"  [{theme.CategoryLabelHex}]Themes[/]")
                    .HighlightStyle(new Style(
                        foreground: theme.ToColor(theme.BackgroundHex),
                        background: theme.ToColor(theme.PrimaryHex)))
                    .UseConverter(t => $"  {t.Name} [{t.MutedHex}]({t.BackgroundHex})[/]")
                    .AddChoices(themes));

            WindowStyler.ApplyWindowTheme(session.CurrentTheme, " ");
            break;

        case 7:
            return;
    }
}
using MediaRenamer.Core.Models;
using MediaRenamer.Theme;
using Spectre.Console;

namespace MediaRenamer.UI;

public static class ThemeRenderer
{
    public static void RenderDashboard(AppSession session)
    {
        if (OperatingSystem.IsWindows())
        {
            WindowStyler.SyncBufferToWindowSize();
        }

        var theme = session.CurrentTheme;

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Align(
            new Markup($"[underline bold {theme.HeaderAccentHex}]Media Batch Renamer[/]"),
            HorizontalAlignment.Center));
        AnsiConsole.WriteLine();

        // Capture the exact row where the left panel starts so both top borders align pixel-for-pixel
        int topRow = Console.CursorTop;

        bool isWideLayout = Console.WindowWidth >= 96;
        int leftColumnWidth = isWideLayout
            ? Math.Max(56, (Console.WindowWidth / 2) + 4)
            : Console.WindowWidth - 4;

        // 1. Render Left-Top Panel: Workspace & Pipeline
        var leftPanel = BuildWorkspacePanel(session, leftColumnWidth);
        AnsiConsole.Write(new Padder(leftPanel, new Padding(2, 0, 0, 0)));
        AnsiConsole.WriteLine();

        int menuStartLeft = Console.CursorLeft;
        int menuStartTop = Console.CursorTop;

        // 2. Render Right Full-Height Panel starting on the exact same topRow
        if (isWideLayout)
        {
            int rightStartCol = leftColumnWidth + 4;
            int rightWidth = Math.Max(26, Console.WindowWidth - rightStartCol - 2);
            int totalHeight = Math.Max(10, Console.WindowHeight - topRow - 1);

            var rightPanel = BuildFullHeightSelectionPanel(session, rightWidth, totalHeight);

            using var writer = new StringWriter();
            var virtualConsole = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Yes,
                ColorSystem = ColorSystemSupport.TrueColor,
                Out = new AnsiConsoleOutput(writer)
            });
            virtualConsole.Profile.Width = rightWidth;
            virtualConsole.Write(rightPanel);

            string[] renderedLines = writer.ToString().Split(["\r\n", "\n"], StringSplitOptions.None);
            for (int i = 0; i < renderedLines.Length && (topRow + i) < Console.WindowHeight; i++)
            {
                Console.SetCursorPosition(rightStartCol, topRow + i);
                Console.Write(renderedLines[i]);
            }

            Console.SetCursorPosition(menuStartLeft, menuStartTop);
        }
    }

    private static Panel BuildWorkspacePanel(AppSession session, int targetWidth)
    {
        var theme = session.CurrentTheme;
        var cfg = session.PipelineConfig;

        var layout = new Table()
            .NoBorder()
            .Expand()
            .AddColumn(new TableColumn(string.Empty).RightAligned().Width(12).PadRight(2))
            .AddColumn(new TableColumn(string.Empty).LeftAligned());

        int maxPathWidth = Math.Max(16, targetWidth - 22);
        string displayRoot = session.RootDirectory.Length > maxPathWidth
            ? "..." + session.RootDirectory[^(maxPathWidth - 3)..]
            : session.RootDirectory;

        string scopeLabel = cfg.Scope == CountScope.NameAndArtist ? "Name + Artist" : "Name Only";
        string autoCountText = cfg.AutoCount
            ? $"[{theme.SuccessHex}]✓ Enabled[/] [{theme.MutedHex}]({scopeLabel})[/]"
            : $"[{theme.MutedHex}]Disabled[/]";

        string findText = string.IsNullOrEmpty(cfg.FindText)
            ? $"[{theme.MutedHex}]None[/]"
            : $"[{theme.TextHex}]'{Markup.Escape(cfg.FindText)}'[/] [{theme.MutedHex}]→[/] [{theme.SecondaryHex}]'{Markup.Escape(cfg.ReplaceText)}'[/]";

        string affixesText = (string.IsNullOrEmpty(cfg.Prefix) && string.IsNullOrEmpty(cfg.Suffix))
            ? $"[{theme.MutedHex}]None[/]"
            : $"[{theme.MutedHex}]Pre:[/] [{theme.TextHex}]{Markup.Escape(cfg.Prefix ?? "-")}[/]  [{theme.MutedHex}]Suf:[/] [{theme.TextHex}]{Markup.Escape(cfg.Suffix ?? "-")}[/]";

        string linkedText = cfg.LinkedNumberOverride.HasValue
            ? $"[{theme.WarningHex}]Set {cfg.LinkedNumberOverride.Value.ToString($"D{cfg.PaddingDigits}")}[/]"
            : $"[{theme.MutedHex}]Off[/]";

        string undoText = session.Engine.CanUndo
            ? $"[{theme.SuccessHex}]{session.Engine.LastBatchCount} file(s)[/]"
            : $"[{theme.MutedHex}]Empty[/]";

        int unformattedCount = session.AllFiles.Count(f => !f.Parsed.IsConforming);
        string auditBadge = unformattedCount > 0
            ? $"  [{theme.WarningHex}]({unformattedCount} unformatted)[/]"
            : session.AllFiles.Count > 0
                ? $"  [{theme.SuccessHex}](100% clean)[/]"
                : string.Empty;

        layout.AddRow($"[{theme.CategoryLabelHex}]Workspace[/]", $"[{theme.PrimaryHex}]{Markup.Escape(displayRoot)}[/]");
        layout.AddRow($"[{theme.CategoryLabelHex}]Selection[/]",$"[{theme.SuccessHex}]✓ {session.SelectedFiles.Count} selected[/] [{theme.MutedHex}]/ {session.AllFiles.Count} total[/]{auditBadge}");
        layout.AddRow($"[{theme.CategoryLabelHex}]Replace[/]", findText);
        layout.AddRow($"[{theme.CategoryLabelHex}]Affixes[/]", affixesText);
        layout.AddRow($"[{theme.CategoryLabelHex}]Formatting[/]", $"[{theme.SecondaryHex}]{cfg.CaseMode}[/]  [{theme.MutedHex}]Auto-Count:[/] {autoCountText}");
        layout.AddRow($"[{theme.CategoryLabelHex}]Group Sync[/]", $"{linkedText}  [{theme.MutedHex}]Undo:[/] {undoText}");

        return new Panel(layout)
        {
            Header = new PanelHeader($" [{theme.MutedHex}]Workspace & Pipeline ({Markup.Escape(theme.Name)})[/] ", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: theme.ToColor(theme.BorderHex)),
            Padding = new Padding(1, 0, 1, 0),
            Width = targetWidth
        };
    }

    private static Panel BuildFullHeightSelectionPanel(AppSession session, int width, int height)
    {
        var theme = session.CurrentTheme;
        var selected = session.SelectedFiles;

        // Subtract 2 rows for top/bottom rounded borders
        int innerRows = Math.Max(3, height - 2);
        int maxTextWidth = Math.Max(16, width - 5);

        var lines = new List<string>(innerRows);

        if (selected.Count == 0)
        {
            lines.Add($"[{theme.MutedHex}]No files selected yet.[/]");
            lines.Add($"[{theme.MutedHex}]Use 'Select Files' on the left.[/]");
        }
        else
        {
            bool hasOverflow = selected.Count > innerRows;
            int itemsToShow = hasOverflow ? innerRows - 1 : selected.Count;

            for (int i = 0; i < itemsToShow; i++)
            {
                var item = selected[i];
                string folder = item.RelativeFolder == "." ? "root" : item.RelativeFolder;
                string fullLabel = $"{folder}/{item.FileName}";

                if (fullLabel.Length > maxTextWidth)
                {
                    int availName = Math.Max(8, maxTextWidth - folder.Length - 4);
                    string trimmed = item.FileName.Length > availName
                        ? item.FileName[..availName] + "..."
                        : item.FileName;

                    lines.Add($"[{theme.FolderBadgeHex}]{Markup.Escape(folder)}[/][{theme.MutedHex}]/[/][{theme.TextHex}]{Markup.Escape(trimmed)}[/]");
                }
                else
                {
                    lines.Add($"[{theme.FolderBadgeHex}]{Markup.Escape(folder)}[/][{theme.MutedHex}]/[/][{theme.TextHex}]{Markup.Escape(item.FileName)}[/]");
                }
            }

            if (hasOverflow)
            {
                int remaining = selected.Count - itemsToShow;
                lines.Add($"[{theme.HeaderAccentHex}]... and {remaining} more file(s) selected[/]");
            }
        }

        // Pad remaining lines so the panel stretches all the way to the bottom of the screen
        while (lines.Count < innerRows)
        {
            lines.Add(string.Empty);
        }

        // Note: Spectre wraps lines automatically if Console.SetCursorPosition is used with AnsiConsole.Write,
        // so we render each row of the panel explicitly at rightStartCol to keep the left column untouched.
        return new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader($" [{theme.MutedHex}]Current Selection ({selected.Count})[/] ", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: theme.ToColor(theme.BorderHex)),
            Padding = new Padding(1, 0, 1, 0),
            Width = width,
            Height = height
        };
    }

    public static void Pause(AppTheme theme)
    {
        AnsiConsole.MarkupLine($"\n  [{theme.MutedHex}]Press any key to continue...[/]");
        Console.ReadKey(true);
    }
}
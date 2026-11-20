using MediaRenamer.Core.Models;
using Spectre.Console;

namespace MediaRenamer.UI.Screens;

public static class PreviewScreen
{
    public static void Show(AppSession session)
    {
        var theme = session.CurrentTheme;

        if (session.SelectedFiles.Count == 0)
        {
            AnsiConsole.MarkupLine($"  [{theme.WarningHex}]! No files selected. Select files first.[/]");
            ThemeRenderer.Pause(theme);
            return;
        }

        var previews = session.Engine.BuildPreview(session.AllFiles, session.SelectedFiles, session.PipelineConfig);

        int readyCount = previews.Count(p => p.HasChanged && !p.HasCollision);
        int collisionCount = previews.Count(p => p.HasCollision);
        int unchangedCount = previews.Count(p => !p.HasChanged);

        int pageSize = WindowStyler.GetResponsivePageSize(reservedHeaderRows: 13, minRows: 5);
        int totalPages = Math.Max(1, (int)Math.Ceiling(previews.Count / (double)pageSize));
        int currentPage = 0;

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Align(
                new Markup($"[underline bold {theme.HeaderAccentHex}]Batch Rename Preview[/]"),
                HorizontalAlignment.Center));
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine(
                $"  [{theme.SuccessHex}]✓ {readyCount} Ready[/]   " +
                $"[{theme.ErrorHex}]✗ {collisionCount} Collisions[/]   " +
                $"[{theme.MutedHex}]- {unchangedCount} Unchanged[/]   " +
                $"[{theme.PrimaryHex}](Page {currentPage + 1} of {totalPages})[/]");
            AnsiConsole.WriteLine();

            var table = new Table()
                .Border(theme.GetTableBorder())
                .BorderColor(theme.ToColor(theme.BorderHex))
                .AddColumn($"[{theme.HeaderAccentHex}]Folder[/]")
                .AddColumn($"[{theme.HeaderAccentHex}]Current[/]")
                .AddColumn($"[{theme.HeaderAccentHex}]Proposed[/]")
                .AddColumn($"[{theme.HeaderAccentHex}]Status[/]");

            foreach (var p in previews.Skip(currentPage * pageSize).Take(pageSize))
            {
                string folder = p.Source.RelativeFolder == "." ? "root" : p.Source.RelativeFolder;
                string status = p.HasCollision
                    ? $"[{theme.ErrorHex}]✗ COLLISION[/]"
                    : p.HasChanged
                        ? $"[{theme.SuccessHex}]✓ READY[/]"
                        : $"[{theme.MutedHex}]- UNCHANGED[/]";

                string newNameDisplay = p.HasChanged
                    ? $"[{theme.PrimaryHex}]{Markup.Escape(p.ProposedFileName)}[/]"
                    : $"[{theme.MutedHex}]{Markup.Escape(p.ProposedFileName)}[/]";

                table.AddRow(
                    $"[{theme.FolderBadgeHex}]{Markup.Escape(folder)}[/]",
                    $"[{theme.TextHex}]{Markup.Escape(p.Source.FileName)}[/]",
                    newNameDisplay,
                    status);
            }

            AnsiConsole.Write(new Padder(table, new Padding(2, 0, 2, 1)));

            if (readyCount == 0)
            {
                AnsiConsole.MarkupLine($"  [{theme.WarningHex}]! No valid changes to apply.[/]");
                ThemeRenderer.Pause(theme);
                return;
            }

            string pageHint = totalPages > 1 ? $"[{theme.PrimaryHex}][[N]][/] Next Page  •  [{theme.PrimaryHex}][[P]][/] Prev Page  •  " : string.Empty;
            AnsiConsole.MarkupLine(
                $"  {pageHint}[{theme.SuccessHex}][[Enter / A]][/] Apply to {readyCount} file(s)  •  [{theme.MutedHex}][[Esc]][/] Cancel");

            var key = Console.ReadKey(intercept: true);

            if ((key.Key == ConsoleKey.N || key.Key == ConsoleKey.RightArrow) && currentPage < totalPages - 1)
            {
                currentPage++;
            }
            else if ((key.Key == ConsoleKey.P || key.Key == ConsoleKey.LeftArrow) && currentPage > 0)
            {
                currentPage--;
            }
            else if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Q)
            {
                return;
            }
            else if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.A || key.Key == ConsoleKey.Y)
            {
                var result = session.Engine.ExecuteRenames(previews);
                AnsiConsole.MarkupLine($"\n  [{theme.SuccessHex}]✓ Renamed {result.Succeeded} file(s) (selection retained for follow-up passes).[/]");

                foreach (string err in result.Errors)
                {
                    AnsiConsole.MarkupLine($"  [{theme.ErrorHex}]✗ {Markup.Escape(err)}[/]");
                }

                session.RescanAndRetainSelection(result.AppliedRecords);
                ThemeRenderer.Pause(theme);
                return;
            }
        }
    }
}
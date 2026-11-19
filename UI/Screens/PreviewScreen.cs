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

        var table = new Table()
            .Border(theme.GetTableBorder())
            .BorderColor(theme.ToColor(theme.BorderHex))
            .AddColumn($"[{theme.HeaderAccentHex}]Folder[/]")
            .AddColumn($"[{theme.HeaderAccentHex}]Current[/]")
            .AddColumn($"[{theme.HeaderAccentHex}]Proposed[/]")
            .AddColumn($"[{theme.HeaderAccentHex}]Status[/]");

        foreach (var p in previews)
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
                status
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        int readyCount = previews.Count(p => p.HasChanged && !p.HasCollision);
        if (readyCount == 0)
        {
            AnsiConsole.MarkupLine($"  [{theme.WarningHex}]! No valid changes to apply.[/]");
            ThemeRenderer.Pause(theme);
            return;
        }

        if (AnsiConsole.Confirm($"  [{theme.TextHex}]Apply rename to[/] [{theme.HeaderAccentHex}]{readyCount}[/] [{theme.TextHex}]file(s)?[/]"))
        {
            var result = session.Engine.ExecuteRenames(previews);
            AnsiConsole.MarkupLine($"  [{theme.SuccessHex}]✓ Renamed {result.Succeeded} file(s).[/]");

            foreach (string err in result.Errors)
            {
                AnsiConsole.MarkupLine($"  [{theme.ErrorHex}]✗ {Markup.Escape(err)}[/]");
            }

            session.RescanWorkspace();
        }

        ThemeRenderer.Pause(theme);
    }
}
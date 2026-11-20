using MediaRenamer.Core.Models;
using Spectre.Console;

namespace MediaRenamer.UI.Screens;

public static class FileSelectionScreen
{
    private sealed record SelectableEntry(string Label, MediaFileItem? File, bool IsGroupHeader = false);

    public static void Show(AppSession session)
    {
        var theme = session.CurrentTheme;

        if (session.AllFiles.Count == 0)
        {
            session.RescanWorkspace();
            if (session.AllFiles.Count == 0)
            {
                AnsiConsole.MarkupLine($"  [{theme.WarningHex}]! No media files found. Set a target folder first.[/]");
                ThemeRenderer.Pause(theme);
                return;
            }
        }

        int nonConformingCount = session.AllFiles.Count(f => !f.Parsed.IsConforming);

        int scopeMode = AnsiConsole.Prompt(
            new SelectionPrompt<int>()
                .Title($"  [{theme.CategoryLabelHex}]Selection Source[/]")
                .HighlightStyle(new Style(
                    foreground: theme.ToColor(theme.BackgroundHex),
                    background: theme.ToColor(theme.PrimaryHex),
                    decoration: Decoration.Bold))
                .UseConverter(m => m switch
                {
                    1 => $"  All Indexed Files ({session.AllFiles.Count})",
                    2 => $"  [{theme.WarningHex}]Audit: Non-Conforming Files Only ({nonConformingCount} need cleanup)[/]",
                    _ => string.Empty
                })
                .AddChoices(1, 2));

        bool auditMode = scopeMode == 2;
        var basePool = auditMode
            ? session.AllFiles.Where(f => !f.Parsed.IsConforming).ToList()
            : session.AllFiles;

        if (basePool.Count == 0)
        {
            AnsiConsole.MarkupLine($"  [{theme.SuccessHex}]✓ All {session.AllFiles.Count} files already conform to 'Name - Artist - 001.ext'![/]");
            ThemeRenderer.Pause(theme);
            return;
        }

        string filter = AnsiConsole.Prompt(
            new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Filter[/] [{theme.MutedHex}](folder, name, or artist; Enter for all {basePool.Count}) >[/]")
                .AllowEmpty());

        var filtered = string.IsNullOrWhiteSpace(filter)
            ? basePool
            : basePool.Where(f =>
                f.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                f.RelativeFolder.Contains(filter, StringComparison.OrdinalIgnoreCase))
              .ToList();

        if (filtered.Count == 0)
        {
            AnsiConsole.MarkupLine($"  [{theme.WarningHex}]! No files matched that filter.[/]");
            ThemeRenderer.Pause(theme);
            return;
        }

        var selectedSet = session.SelectedFiles.Select(f => f.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var prompt = new MultiSelectionPrompt<SelectableEntry>()
            .Title($"  [{theme.CategoryLabelHex}]Select Files[/]")
            .PageSize(WindowStyler.GetResponsivePageSize(reservedHeaderRows: 12, minRows: 6))
            .NotRequired()
            .HighlightStyle(new Style(foreground: theme.ToColor(theme.PrimaryHex)))
            .MoreChoicesText($"  [{theme.MutedHex}](Scroll down for more files)[/]")
            .InstructionsText($"  [{theme.MutedHex}](Press <space> to toggle individual or group, <enter> to confirm)[/]")
            .UseConverter(entry => entry.Label);

        var groupHeader = new SelectableEntry(
            $"[bold {theme.HeaderAccentHex}]Select / Deselect All ({filtered.Count} files)[/]",
            File: null,
            IsGroupHeader: true);

        var childEntries = filtered.Select(item =>
        {
            string folderTag = item.RelativeFolder == "." ? "root" : item.RelativeFolder;
            string reasonBadge = auditMode
                ? $" [{theme.WarningHex}][[{Markup.Escape(item.Parsed.NonConformingReason)}]][/]"
                : string.Empty;

            string label = $"[{theme.FolderBadgeHex}]{Markup.Escape(folderTag)}[/] [{theme.MutedHex}]/[/] [{theme.TextHex}]{Markup.Escape(item.FileName)}[/]{reasonBadge}";
            return new SelectableEntry(label, item);
        }).ToList();

        prompt.AddChoiceGroup(groupHeader, childEntries);

        foreach (var entry in childEntries)
        {
            if (entry.File != null && selectedSet.Contains(entry.File.FullPath))
            {
                prompt.Select(entry);
            }
        }

        session.SelectedFiles = AnsiConsole.Prompt(prompt)
            .Where(e => !e.IsGroupHeader && e.File != null)
            .Select(e => e.File!)
            .ToList();
    }
}
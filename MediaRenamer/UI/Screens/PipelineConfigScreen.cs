using MediaRenamer.Core.Abstractions;
using MediaRenamer.Core.Models;
using MediaRenamer.Theme;
using MediaRenamer.UI.Operations;
using Spectre.Console;

namespace MediaRenamer.UI.Screens;

public static class PipelineConfigScreen
{
    public static void Show(AppSession session)
    {
        var config = session.PipelineConfig;
        var theme = session.CurrentTheme;

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Align(
                new Markup($"[underline bold {theme.HeaderAccentHex}]Rename Operation Stack[/]"),
                HorizontalAlignment.Center));
            AnsiConsole.WriteLine();

            RenderOperationStackAndLivePreview(session);

            string scopeDisplay = config.Scope switch
            {
                CountScope.NameAndArtist => "Name + Artist",
                CountScope.NameOnly => "License + Character (Ignore Artist)",
                CountScope.CharacterOnly => "Character Only (Ignore License & Artist)",
                _ => "Name + Artist"
            };

            var options = new List<MenuOption<int>>
            {
                new(1, $"[{theme.SuccessHex}]+ Add Operation[/] [{theme.MutedHex}]({OperationEditorRegistry.All.Count} available types)[/]", '1'),
                new(2, $"[{theme.PrimaryHex}]✎ Edit / Reorder / Remove Operations ({config.Operations.Count})[/]", '2'),
                new(3, $"[{theme.HeaderAccentHex}]👁 Full Step-by-Step Inspector (Original | After Step | Final)[/]", '3'),
                new(4, $"[{theme.HeaderAccentHex}]Presets (Load / Save / Delete)[/]", '4'),
                new(5, $"Smart Auto-Count      [{theme.MutedHex}]→[/] {(config.AutoCount ? $"[{theme.SuccessHex}]✓ ENABLED[/]" : $"[{theme.MutedHex}]DISABLED[/]")}", '5'),
                new(6, $"Compact Sequence Gaps [{theme.MutedHex}]→[/] {(config.CompactGaps ? $"[{theme.WarningHex}]✓ ON (001, 003 → 001, 002)[/]" : $"[{theme.MutedHex}]OFF[/]")}", '6'),
                new(7, $"Auto-Count Scope      [{theme.MutedHex}]→[/] [{theme.PrimaryHex}]{Markup.Escape(scopeDisplay)}[/]", '7'),
                new(8, $"Linked Group Index    [{theme.MutedHex}]→[/] {(config.LinkedNumberOverride.HasValue ? $"[{theme.WarningHex}]{config.LinkedNumberOverride.Value}[/]" : $"[{theme.MutedHex}]Disabled[/]")}", '8'),
                new(9, $"[{theme.WarningHex}]Reset Stack to Default[/]", '9'),
                new(0, $"[{theme.SuccessHex}]✓ Done (Esc)[/]", '0', IsBackOption: true)
            };

            int choice = InteractiveMenu.Prompt(
                $"  [{theme.CategoryLabelHex}]Stack & Numbering Controls[/]",
                options,
                theme,
                backValue: 0,
                allowEscape: true,
                pageSize: 10);

            switch (choice)
            {
                case 1:
                    AddOperationFlow(config, theme);
                    session.SaveState();
                    break;
                case 2:
                    ManageExistingOperations(config, theme);
                    session.SaveState();
                    break;
                case 3:
                    ShowStepByStepPreview(session);
                    break;
                case 4:
                    ManagePresets(config, session.Presets, theme);
                    session.SaveState();
                    break;
                case 5:
                    config.AutoCount = !config.AutoCount;
                    session.SaveState();
                    break;
                case 6:
                    config.CompactGaps = !config.CompactGaps;
                    if (config.CompactGaps) config.LinkedNumberOverride = null;
                    session.SaveState();
                    break;
                case 7:
                    config.Scope = config.Scope switch
                    {
                        CountScope.NameAndArtist => CountScope.NameOnly,
                        CountScope.NameOnly => CountScope.CharacterOnly,
                        _ => CountScope.NameAndArtist
                    };
                    session.SaveState();
                    break;
                case 8:
                    AnsiConsole.Clear();
                    AnsiConsole.WriteLine();
                    string raw = AnsiConsole.Prompt(
                        new TextPrompt<string>($"  [{theme.CategoryLabelHex}]New Group Number[/] [{theme.MutedHex}](blank to disable) >[/]")
                            .AllowEmpty());
                    config.LinkedNumberOverride = int.TryParse(raw, out int parsedNum) && parsedNum >= 0 ? parsedNum : null;
                    if (config.LinkedNumberOverride.HasValue) config.CompactGaps = false;
                    session.SaveState();
                    break;
                case 9:
                    config.Reset();
                    session.SaveState();
                    break;
                case 0:
                    return;
            }
        }
    }

    private static void RenderOperationStackAndLivePreview(AppSession session)
    {
        var config = session.PipelineConfig;
        var theme = session.CurrentTheme;

        var stackTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(theme.ToColor(theme.BorderHex))
            .AddColumn($"[{theme.MutedHex}]#[/]")
            .AddColumn($"[{theme.HeaderAccentHex}]Operation[/]")
            .AddColumn($"[{theme.HeaderAccentHex}]Configuration[/]")
            .AddColumn($"[{theme.HeaderAccentHex}]State[/]");

        if (config.Operations.Count == 0)
        {
            stackTable.AddRow(
                "-",
                $"[{theme.MutedHex}]No operations in stack[/]",
                $"[{theme.MutedHex}]Press [[1]] to add an operation[/]",
                "-");
        }
        else
        {
            for (int i = 0; i < config.Operations.Count; i++)
            {
                var op = config.Operations[i];
                string state = op.IsEnabled
                    ? $"[{theme.SuccessHex}]✓ Active[/]"
                    : $"[{theme.MutedHex}]Disabled[/]";

                string titleColor = op.IsEnabled ? theme.PrimaryHex : theme.MutedHex;
                string summaryColor = op.IsEnabled ? theme.TextHex : theme.MutedHex;

                stackTable.AddRow(
                    $"[{theme.MutedHex}]{i + 1}[/]",
                    $"[{titleColor}]{Markup.Escape(op.DisplayTitle)}[/]",
                    $"[{summaryColor}]{Markup.Escape(op.Summary)}[/]",
                    state);
            }
        }

        AnsiConsole.Write(new Padder(stackTable, new Padding(2, 0, 2, 0)));

        var sampleSource = session.SelectedFiles.Count > 0
            ? [.. session.SelectedFiles.Take(3)]
            : session.AllFiles.Take(3).ToList();

        if (sampleSource.Count > 0)
        {
            var samplePreviews = session.Engine.BuildPreview(session.AllFiles, sampleSource, config);
            var previewTable = new Table()
                .NoBorder()
                .AddColumn(new TableColumn($"[{theme.MutedHex}]Live Sample (Before)[/]"))
                .AddColumn(new TableColumn(string.Empty).Width(3))
                .AddColumn(new TableColumn($"[{theme.HeaderAccentHex}]Live Sample (After)[/]"));

            foreach (var p in samplePreviews.Take(3))
            {
                string afterColor = p.HasCollision ? theme.ErrorHex : p.HasChanged ? theme.SuccessHex : theme.MutedHex;
                previewTable.AddRow(
                    $"[{theme.MutedHex}]{Markup.Escape(p.Source.FileName)}[/]",
                    $"[{theme.MutedHex}]→[/]",
                    $"[{afterColor}]{Markup.Escape(p.ProposedFileName)}[/]");
            }

            var livePanel = new Panel(previewTable)
            {
                Header = new PanelHeader($" [{theme.MutedHex}]Live Sample Preview[/] ", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(foreground: theme.ToColor(theme.BorderHex)),
                Padding = new Padding(1, 0, 1, 0)
            };

            AnsiConsole.Write(new Padder(livePanel, new Padding(2, 0, 2, 1)));
        }
        else
        {
            AnsiConsole.WriteLine();
        }
    }

    private static void AddOperationFlow(RenamePipelineConfig config, AppTheme theme)
    {
        AnsiConsole.Clear();
        AnsiConsole.WriteLine();

        var descriptors = OperationEditorRegistry.All;
        var typeOptions = descriptors
            .Select((desc, idx) => new MenuOption<int>(
                idx,
                Markup.Escape(desc.MenuLabel),
                idx < 9 ? (char)('1' + idx) : null))
            .Append(new MenuOption<int>(-1, "Cancel (Esc)", '0', IsBackOption: true))
            .ToList();

        int selectedIdx = InteractiveMenu.Prompt(
            $"  [{theme.CategoryLabelHex}]Choose Operation Type to Append[/]",
            typeOptions,
            theme,
            backValue: -1,
            allowEscape: true);

        if (selectedIdx < 0) return;

        AnsiConsole.Clear();
        AnsiConsole.WriteLine();

        var created = descriptors[selectedIdx].CreateNew(theme);
        config.Operations.Add(created);
    }

    private static void ManageExistingOperations(RenamePipelineConfig config, AppTheme theme)
    {
        if (config.Operations.Count == 0) return;

        AnsiConsole.Clear();
        AnsiConsole.WriteLine();

        var opOptions = config.Operations
            .Select((op, idx) =>
            {
                string badge = op.IsEnabled ? $"[{theme.SuccessHex}]ON[/]" : $"[{theme.MutedHex}]OFF[/]";
                char? hotkey = idx < 9 ? (char)('1' + idx) : null;
                return new MenuOption<int>(
                    idx,
                    $"[{theme.MutedHex}]#{idx + 1}[/] [[{badge}]] [{theme.PrimaryHex}]{Markup.Escape(op.DisplayTitle)}[/] — {Markup.Escape(op.Summary)}",
                    hotkey);
            })
            .Append(new MenuOption<int>(-1, "Back (Esc)", '0', IsBackOption: true))
            .ToList();

        int index = InteractiveMenu.Prompt(
            $"  [{theme.CategoryLabelHex}]Select Operation to Modify[/]",
            opOptions,
            theme,
            backValue: -1,
            allowEscape: true);

        if (index == -1) return;

        var selectedOp = config.Operations[index];

        AnsiConsole.Clear();
        AnsiConsole.WriteLine();

        var actionOptions = new List<MenuOption<int>>
        {
            new(1, "✎ Edit Parameters", '1'),
            new(2, "▲ Move Up", '2'),
            new(3, "▼ Move Down", '3'),
            new(4, selectedOp.IsEnabled ? "○ Disable Step" : "● Enable Step", '4'),
            new(5, $"[{theme.ErrorHex}]✕ Remove Step[/]", '5'),
            new(0, "Back (Esc)", '0', IsBackOption: true)
        };

        int action = InteractiveMenu.Prompt(
            $"  [{theme.CategoryLabelHex}]Action for #{index + 1} ({Markup.Escape(selectedOp.DisplayTitle)})[/]",
            actionOptions,
            theme,
            backValue: 0,
            allowEscape: true);

        switch (action)
        {
            case 1:
                AnsiConsole.Clear();
                AnsiConsole.WriteLine();
                OperationEditorRegistry.FindFor(selectedOp)?.Edit(selectedOp, theme);
                break;
            case 2 when index > 0:
                (config.Operations[index - 1], config.Operations[index]) = (config.Operations[index], config.Operations[index - 1]);
                break;
            case 3 when index < config.Operations.Count - 1:
                (config.Operations[index + 1], config.Operations[index]) = (config.Operations[index], config.Operations[index + 1]);
                break;
            case 4:
                selectedOp.IsEnabled = !selectedOp.IsEnabled;
                break;
            case 5:
                config.Operations.RemoveAt(index);
                break;
        }
    }

    private static void ShowStepByStepPreview(AppSession session)
    {
        var theme = session.CurrentTheme;
        var config = session.PipelineConfig;

        var sampleFiles = session.SelectedFiles.Count > 0
            ? session.SelectedFiles
            : [.. session.AllFiles.Take(15)];

        if (sampleFiles.Count == 0)
        {
            AnsiConsole.MarkupLine($"  [{theme.WarningHex}]! No files indexed in workspace to preview.[/]");
            ThemeRenderer.Pause(theme);
            return;
        }

        var previews = session.Engine.BuildPreview(session.AllFiles, sampleFiles, config);
        var enabledOps = config.Operations.Where(o => o.IsEnabled).ToList();
        int inspectedStepIndex = enabledOps.Count > 0 ? 0 : -1;

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Align(
                new Markup($"[underline bold {theme.HeaderAccentHex}]Step-by-Step Pipeline Inspector[/]"),
                HorizontalAlignment.Center));
            AnsiConsole.WriteLine();

            string inspectedHeader = inspectedStepIndex >= 0 && inspectedStepIndex < enabledOps.Count
                ? $"After Step #{inspectedStepIndex + 1} ({Markup.Escape(enabledOps[inspectedStepIndex].DisplayTitle)})"
                : "After Steps";

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(theme.ToColor(theme.BorderHex))
                .AddColumn($"[{theme.MutedHex}]Original[/]")
                .AddColumn($"[{theme.HeaderAccentHex}]{inspectedHeader}[/]")
                .AddColumn($"[{theme.PrimaryHex}]Final Name[/]");

            int maxRows = WindowStyler.GetResponsivePageSize(reservedHeaderRows: 16, minRows: 5);
            foreach (var p in previews.Take(maxRows))
            {
                string afterStepText = inspectedStepIndex >= 0 && inspectedStepIndex < p.StepHistory.Count
                    ? p.StepHistory[inspectedStepIndex].ResultingBaseName + p.Source.Extension
                    : p.Source.FileName;

                string afterColor = !string.Equals(afterStepText, p.Source.FileName, StringComparison.Ordinal)
                    ? theme.TextHex
                    : theme.MutedHex;

                string finalColor = p.HasCollision
                    ? theme.ErrorHex
                    : p.HasChanged
                        ? theme.SuccessHex
                        : theme.MutedHex;

                table.AddRow(
                    $"[{theme.MutedHex}]{Markup.Escape(p.Source.FileName)}[/]",
                    $"[{afterColor}]{Markup.Escape(afterStepText)}[/]",
                    $"[{finalColor}]{Markup.Escape(p.ProposedFileName)}[/]");
            }

            AnsiConsole.Write(new Padder(table, new Padding(2, 0, 2, 1)));

            var tabOptions = enabledOps
                .Select((op, idx) => new MenuOption<int>(
                    idx,
                    $"Step #{idx + 1}: {Markup.Escape(op.DisplayTitle)} ({Markup.Escape(op.Summary)})",
                    idx < 9 ? (char)('1' + idx) : null))
                .Append(new MenuOption<int>(-1, "← Back to Stack Editor (Esc)", '0', IsBackOption: true))
                .ToList();

            int picked = InteractiveMenu.Prompt(
                $"  [{theme.CategoryLabelHex}]Inspect Step Tab[/]",
                tabOptions,
                theme,
                backValue: -1,
                allowEscape: true,
                pageSize: 6);

            if (picked == -1) return;
            inspectedStepIndex = picked;
        }
    }

    private static void ManagePresets(RenamePipelineConfig config, IPresetRepository presetsRepo, AppTheme theme)
    {
        AnsiConsole.Clear();
        AnsiConsole.WriteLine();

        var presetMenu = new List<MenuOption<int>>
        {
            new(1, "Load Saved Preset", '1'),
            new(2, "Save Current Operation Stack as Preset", '2'),
            new(3, "Delete a Preset", '3'),
            new(0, "Back (Esc)", '0', IsBackOption: true)
        };

        int action = InteractiveMenu.Prompt(
            $"  [{theme.CategoryLabelHex}]Pipeline Presets[/]",
            presetMenu,
            theme,
            backValue: 0,
            allowEscape: true);

        if (action == 1)
        {
            var presets = presetsRepo.LoadAll();
            if (presets.Count == 0) return;

            AnsiConsole.Clear();
            AnsiConsole.WriteLine();

            var choices = presets
                .Select((p, i) => new MenuOption<int>(i, $"{Markup.Escape(p.Name)} [{theme.MutedHex}]({p.Config.Operations.Count} steps)[/]", i < 9 ? (char)('1' + i) : null))
                .Append(new MenuOption<int>(-1, "Cancel (Esc)", '0', IsBackOption: true))
                .ToList();

            int idx = InteractiveMenu.Prompt($"  [{theme.CategoryLabelHex}]Select Preset to Load[/]", choices, theme, backValue: -1, allowEscape: true);
            if (idx >= 0)
            {
                config.CopyFrom(presets[idx].Config);
                AnsiConsole.MarkupLine($"\n  [{theme.SuccessHex}]✓ Loaded preset '{Markup.Escape(presets[idx].Name)}'.[/]");
                ThemeRenderer.Pause(theme);
            }
        }
        else if (action == 2)
        {
            AnsiConsole.Clear();
            AnsiConsole.WriteLine();
            string name = AnsiConsole.Prompt(
                new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Preset Name[/] [{theme.MutedHex}]>[/]").AllowEmpty());

            if (!string.IsNullOrWhiteSpace(name))
            {
                presetsRepo.Save(name, config);
                AnsiConsole.MarkupLine($"  [{theme.SuccessHex}]✓ Saved preset '{Markup.Escape(name)}'.[/]");
                ThemeRenderer.Pause(theme);
            }
        }
        else if (action == 3)
        {
            var presets = presetsRepo.LoadAll();
            if (presets.Count == 0) return;

            AnsiConsole.Clear();
            AnsiConsole.WriteLine();

            var choices = presets
                .Select((p, i) => new MenuOption<int>(i, Markup.Escape(p.Name), i < 9 ? (char)('1' + i) : null))
                .Append(new MenuOption<int>(-1, "Cancel (Esc)", '0', IsBackOption: true))
                .ToList();

            int idx = InteractiveMenu.Prompt($"  [{theme.WarningHex}]Select Preset to Delete[/]", choices, theme, backValue: -1, allowEscape: true);
            if (idx >= 0)
            {
                presetsRepo.Delete(presets[idx]);
                AnsiConsole.MarkupLine($"\n  [{theme.SuccessHex}]✓ Deleted preset '{Markup.Escape(presets[idx].Name)}'.[/]");
                ThemeRenderer.Pause(theme);
            }
        }
    }
}
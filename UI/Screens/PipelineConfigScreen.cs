using MediaRenamer.Core.Models;
using MediaRenamer.Core.Pipeline;
using MediaRenamer.Core.Services;
using MediaRenamer.Theme;
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

            string scopeDisplay = config.Scope == CountScope.NameAndArtist ? "Name + Artist" : "Name Only";

            var options = new List<MenuOption<int>>
            {
                new(1, $"[{theme.SuccessHex}]+ Add Operation[/] [{theme.MutedHex}](Replace, Regex, Split, Identity, Affix, Trim, Case)[/]", '1'),
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
                    ManagePresets(config, theme);
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
                    config.Scope = config.Scope == CountScope.NameAndArtist
                        ? CountScope.NameOnly
                        : CountScope.NameAndArtist;
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

        // 1. Operation Stack Table
        var stackTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(theme.ToColor(theme.BorderHex))
            .AddColumn($"[{theme.MutedHex}]#[/]")
            .AddColumn($"[{theme.HeaderAccentHex}]Operation[/]")
            .AddColumn($"[{theme.HeaderAccentHex}]Configuration[/]")
            .AddColumn($"[{theme.HeaderAccentHex}]State[/]");

        if (config.Operations.Count == 0)
        {
            stackTable.AddRow("-", $"[{theme.MutedHex}]No operations in stack[/]", $"[{theme.MutedHex}]Press [1] to add an operation[/]", "-");
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

        // 2. Live Inline Sample Preview (Top 3 selected or workspace files)
        var sampleSource = session.SelectedFiles.Count > 0
            ? session.SelectedFiles.Take(3).ToList()
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

        var typeOptions = new List<MenuOption<int>>
        {
            new(1, "Replace String (supports Regex & Case-Sensitive)", '1'),
            new(2, "Split Name / Artist (insert ' - ' after N words or before Artist)", '2'),
            new(3, "Set Base Identity (replace entire Name - Artist stem)", '3'),
            new(4, "Add Prefix / Suffix", '4'),
            new(5, "Trim Characters (remove N chars from start or end)", '5'),
            new(6, "Case Conversion (TitleCase, PascalCase, CamelCase)", '6'),
            new(7, "Strip Legacy Numbers (remove old trailing ' 00', '_01')", '7'),
            new(0, "Cancel (Esc)", '0', IsBackOption: true)
        };

        int typeChoice = InteractiveMenu.Prompt(
            $"  [{theme.CategoryLabelHex}]Choose Operation Type to Append[/]",
            typeOptions,
            theme,
            backValue: 0,
            allowEscape: true);

        AnsiConsole.Clear();
        AnsiConsole.WriteLine();

        RenameOperation? created = typeChoice switch
        {
            1 => PromptReplaceOperation(new ReplaceStringOperation(), theme),
            2 => PromptSplitOperation(new SplitNameArtistOperation(), theme),
            3 => PromptSetIdentityOperation(new SetBaseIdentityOperation(), theme),
            4 => PromptAffixOperation(new AddAffixOperation(), theme),
            5 => PromptTrimOperation(new TrimCharactersOperation(), theme),
            6 => PromptCaseOperation(new CaseTransformOperation(), theme),
            7 => new StripLegacyNumberOperation(),
            _ => null
        };

        if (created != null)
        {
            config.Operations.Add(created);
        }
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
                EditOperation(selectedOp, theme);
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
            : session.AllFiles.Take(15).ToList();

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

            int maxRows = Math.Max(5, Console.WindowHeight - 16);
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

    private static void EditOperation(RenameOperation op, AppTheme theme)
    {
        switch (op)
        {
            case ReplaceStringOperation rep: PromptReplaceOperation(rep, theme); break;
            case SplitNameArtistOperation spl: PromptSplitOperation(spl, theme); break;
            case SetBaseIdentityOperation idOp: PromptSetIdentityOperation(idOp, theme); break;
            case AddAffixOperation aff: PromptAffixOperation(aff, theme); break;
            case TrimCharactersOperation trim: PromptTrimOperation(trim, theme); break;
            case CaseTransformOperation cas: PromptCaseOperation(cas, theme); break;
        }
    }

    private static ReplaceStringOperation PromptReplaceOperation(ReplaceStringOperation op, AppTheme theme)
    {
        op.SearchFor = AnsiConsole.Prompt(
            new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Search for String / Pattern[/] [{theme.MutedHex}]>[/]")
                .DefaultValue(op.SearchFor)
                .AllowEmpty());

        op.ReplaceWith = AnsiConsole.Prompt(
            new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Replace with[/] [{theme.MutedHex}](Enter for empty/delete) >[/]")
                .DefaultValue(op.ReplaceWith)
                .AllowEmpty());

        op.UseRegex = AnsiConsole.Confirm($"  [{theme.TextHex}]Use Regular Expression?[/]", defaultValue: op.UseRegex);
        op.CaseSensitive = AnsiConsole.Confirm($"  [{theme.TextHex}]Case Sensitive?[/]", defaultValue: op.CaseSensitive);
        return op;
    }

    private static SplitNameArtistOperation PromptSplitOperation(SplitNameArtistOperation op, AppTheme theme)
    {
        int mode = AnsiConsole.Prompt(
            new SelectionPrompt<int>()
                .Title($"  [{theme.CategoryLabelHex}]Split Mode[/]")
                .UseConverter(m => m == 1
                    ? "  Split after N words (e.g., 1 word: 'Character Artist' → 'Character - Artist')"
                    : "  Split before specific Artist name")
                .AddChoices(1, 2));

        if (mode == 1)
        {
            op.ArtistHint = null;
            op.SplitAfterWords = AnsiConsole.Prompt(
                new TextPrompt<int>($"  [{theme.CategoryLabelHex}]Number of words in Name[/] [{theme.MutedHex}]>[/]")
                    .DefaultValue(op.SplitAfterWords ?? 1));
        }
        else
        {
            op.SplitAfterWords = null;
            op.ArtistHint = AnsiConsole.Prompt(
                new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Artist name to split before[/] [{theme.MutedHex}]>[/]")
                    .DefaultValue(op.ArtistHint ?? string.Empty)
                    .AllowEmpty());
        }
        return op;
    }

    private static SetBaseIdentityOperation PromptSetIdentityOperation(SetBaseIdentityOperation op, AppTheme theme)
    {
        op.NewIdentity = AnsiConsole.Prompt(
            new TextPrompt<string>($"  [{theme.CategoryLabelHex}]New Base Identity[/] [{theme.MutedHex}](e.g. 'Character - Artist') >[/]")
                .DefaultValue(op.NewIdentity)
                .AllowEmpty());
        return op;
    }

    private static AddAffixOperation PromptAffixOperation(AddAffixOperation op, AppTheme theme)
    {
        op.Prefix = AnsiConsole.Prompt(
            new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Prefix[/] [{theme.MutedHex}]>[/]")
                .DefaultValue(op.Prefix)
                .AllowEmpty());

        op.Suffix = AnsiConsole.Prompt(
            new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Suffix[/] [{theme.MutedHex}]>[/]")
                .DefaultValue(op.Suffix)
                .AllowEmpty());
        return op;
    }

    private static TrimCharactersOperation PromptTrimOperation(TrimCharactersOperation op, AppTheme theme)
    {
        op.TrimStartCount = AnsiConsole.Prompt(
            new TextPrompt<int>($"  [{theme.CategoryLabelHex}]Characters to trim from START[/] [{theme.MutedHex}]>[/]")
                .DefaultValue(op.TrimStartCount));

        op.TrimEndCount = AnsiConsole.Prompt(
            new TextPrompt<int>($"  [{theme.CategoryLabelHex}]Characters to trim from END[/] [{theme.MutedHex}]>[/]")
                .DefaultValue(op.TrimEndCount));
        return op;
    }

    private static CaseTransformOperation PromptCaseOperation(CaseTransformOperation op, AppTheme theme)
    {
        op.CaseMode = AnsiConsole.Prompt(
            new SelectionPrompt<CaseTransform>()
                .Title($"  [{theme.CategoryLabelHex}]Case Mode[/]")
                .AddChoices(CaseTransform.TitleCase, CaseTransform.PascalCase, CaseTransform.CamelCase, CaseTransform.None));
        return op;
    }

    private static void ManagePresets(RenamePipelineConfig config, AppTheme theme)
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
            var presets = PresetManager.LoadAllPresets();
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
                PresetManager.SavePreset(name, config);
                AnsiConsole.MarkupLine($"  [{theme.SuccessHex}]✓ Saved preset '{Markup.Escape(name)}'.[/]");
                ThemeRenderer.Pause(theme);
            }
        }
        else if (action == 3)
        {
            var presets = PresetManager.LoadAllPresets();
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
                PresetManager.DeletePreset(presets[idx]);
                AnsiConsole.MarkupLine($"\n  [{theme.SuccessHex}]✓ Deleted preset '{Markup.Escape(presets[idx].Name)}'.[/]");
                ThemeRenderer.Pause(theme);
            }
        }
    }
}
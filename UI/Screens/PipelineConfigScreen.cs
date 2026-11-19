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

            RenderOperationStackSummary(config, theme);

            string scopeDisplay = config.Scope == CountScope.NameAndArtist ? "Name + Artist" : "Name Only";

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<int>()
                    .Title($"  [{theme.CategoryLabelHex}]Stack & Numbering Controls[/]")
                    .PageSize(WindowStyler.GetResponsivePageSize(reservedHeaderRows: 12, minRows: 6))
                    .HighlightStyle(new Style(
                        foreground: theme.ToColor(theme.BackgroundHex),
                        background: theme.ToColor(theme.PrimaryHex),
                        decoration: Decoration.Bold))
                    .UseConverter(id => id switch
                    {
                        1 => $"  [{theme.SuccessHex}]+ Add Operation[/] [{theme.MutedHex}](Replace, Regex, Split, Identity, Affix, Trim, Case)[/]",
                        2 => $"  [{theme.PrimaryHex}]✎ Edit / Reorder / Remove Operations ({config.Operations.Count})[/]",
                        3 => $"  [{theme.HeaderAccentHex}]👁 Preview Steps (Original | After Step | Final Name)[/]",
                        4 => $"  [{theme.HeaderAccentHex}]Presets (Load / Save / Delete)[/]",
                        5 => $"  Smart Auto-Count      [{theme.MutedHex}]→[/] {(config.AutoCount ? $"[{theme.SuccessHex}]✓ ENABLED[/]" : $"[{theme.MutedHex}]DISABLED[/]")}",
                        6 => $"  Compact Sequence Gaps [{theme.MutedHex}]→[/] {(config.CompactGaps ? $"[{theme.WarningHex}]✓ ON (001, 003 → 001, 002)[/]" : $"[{theme.MutedHex}]OFF[/]")}",
                        7 => $"  Auto-Count Scope      [{theme.MutedHex}]→[/] [{theme.PrimaryHex}]{Markup.Escape(scopeDisplay)}[/]",
                        8 => $"  Linked Group Index    [{theme.MutedHex}]→[/] {(config.LinkedNumberOverride.HasValue ? $"[{theme.WarningHex}]{config.LinkedNumberOverride.Value}[/]" : $"[{theme.MutedHex}]Disabled[/]")}",
                        9 => $"  [{theme.WarningHex}]Reset Stack to Default[/]",
                        10 => $"  [{theme.SuccessHex}]✓ Done[/]",
                        _ => string.Empty
                    })
                    .AddChoices(1, 2, 3, 4, 5, 6, 7, 8, 9, 10));

            switch (choice)
            {
                case 1:
                    AddOperationFlow(config, theme);
                    break;

                case 2:
                    ManageExistingOperations(config, theme);
                    break;

                case 3:
                    ShowStepByStepPreview(session);
                    break;

                case 4:
                    ManagePresets(config, theme);
                    break;

                case 5:
                    config.AutoCount = !config.AutoCount;
                    break;

                case 6:
                    config.CompactGaps = !config.CompactGaps;
                    if (config.CompactGaps) config.LinkedNumberOverride = null;
                    break;

                case 7:
                    config.Scope = config.Scope == CountScope.NameAndArtist
                        ? CountScope.NameOnly
                        : CountScope.NameAndArtist;
                    break;

                case 8:
                    string raw = AnsiConsole.Prompt(
                        new TextPrompt<string>($"  [{theme.CategoryLabelHex}]New Group Number[/] [{theme.MutedHex}](blank to disable) >[/]")
                            .AllowEmpty());
                    config.LinkedNumberOverride = int.TryParse(raw, out int parsedNum) && parsedNum >= 0 ? parsedNum : null;
                    if (config.LinkedNumberOverride.HasValue) config.CompactGaps = false;
                    break;

                case 9:
                    config.Reset();
                    break;

                case 10:
                    return;
            }
        }
    }

    private static void RenderOperationStackSummary(RenamePipelineConfig config, AppTheme theme)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(theme.ToColor(theme.BorderHex))
            .AddColumn($"[{theme.MutedHex}]#[/]")
            .AddColumn($"[{theme.HeaderAccentHex}]Operation[/]")
            .AddColumn($"[{theme.HeaderAccentHex}]Configuration[/]")
            .AddColumn($"[{theme.HeaderAccentHex}]State[/]");

        if (config.Operations.Count == 0)
        {
            table.AddRow("-", $"[{theme.MutedHex}]No operations in stack[/]", $"[{theme.MutedHex}]Select '+ Add Operation' below[/]", "-");
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

                table.AddRow(
                    $"[{theme.MutedHex}]{i + 1}[/]",
                    $"[{titleColor}]{Markup.Escape(op.DisplayTitle)}[/]",
                    $"[{summaryColor}]{Markup.Escape(op.Summary)}[/]",
                    state);
            }
        }

        AnsiConsole.Write(new Padder(table, new Padding(2, 0, 2, 1)));
    }

    private static void AddOperationFlow(RenamePipelineConfig config, AppTheme theme)
    {
        int typeChoice = AnsiConsole.Prompt(
            new SelectionPrompt<int>()
                .Title($"  [{theme.CategoryLabelHex}]Choose Operation Type to Append[/]")
                .HighlightStyle(new Style(
                    foreground: theme.ToColor(theme.BackgroundHex),
                    background: theme.ToColor(theme.PrimaryHex)))
                .UseConverter(t => t switch
                {
                    1 => "  Replace String (supports Regex & Case-Sensitive)",
                    2 => "  Split Name / Artist (insert ' - ' after N words or before Artist)",
                    3 => "  Set Base Identity (replace entire Name - Artist stem)",
                    4 => "  Add Prefix / Suffix",
                    5 => "  Trim Characters (remove N chars from start or end)",
                    6 => "  Case Conversion (TitleCase, PascalCase, CamelCase)",
                    7 => "  Strip Legacy Numbers (remove old trailing ' 00', '_01')",
                    8 => "  Cancel",
                    _ => string.Empty
                })
                .AddChoices(1, 2, 3, 4, 5, 6, 7, 8));

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
        if (config.Operations.Count == 0)
        {
            AnsiConsole.MarkupLine($"  [{theme.WarningHex}]! Stack is empty.[/]");
            ThemeRenderer.Pause(theme);
            return;
        }

        int index = AnsiConsole.Prompt(
            new SelectionPrompt<int>()
                .Title($"  [{theme.CategoryLabelHex}]Select Operation to Modify[/]")
                .HighlightStyle(new Style(
                    foreground: theme.ToColor(theme.BackgroundHex),
                    background: theme.ToColor(theme.PrimaryHex)))
                .UseConverter(idx =>
                {
                    if (idx == -1) return "  Back";
                    var op = config.Operations[idx];
                    string badge = op.IsEnabled ? $"[{theme.SuccessHex}]ON[/]" : $"[{theme.MutedHex}]OFF[/]";
                    return $"  [{theme.MutedHex}]#{idx + 1}[/] [[{badge}]] [{theme.PrimaryHex}]{Markup.Escape(op.DisplayTitle)}[/] — {Markup.Escape(op.Summary)}";
                })
                .AddChoices(Enumerable.Range(0, config.Operations.Count).Append(-1)));

        if (index == -1) return;

        var selectedOp = config.Operations[index];

        int action = AnsiConsole.Prompt(
            new SelectionPrompt<int>()
                .Title($"  [{theme.CategoryLabelHex}]Action for #{index + 1} ({Markup.Escape(selectedOp.DisplayTitle)})[/]")
                .HighlightStyle(new Style(
                    foreground: theme.ToColor(theme.BackgroundHex),
                    background: theme.ToColor(theme.PrimaryHex)))
                .UseConverter(a => a switch
                {
                    1 => "  ✎ Edit Parameters",
                    2 => "  ▲ Move Up",
                    3 => "  ▼ Move Down",
                    4 => selectedOp.IsEnabled ? "  ○ Disable Step" : "  ● Enable Step",
                    5 => $"  [{theme.ErrorHex}]✕ Remove Step[/]",
                    6 => "  Back",
                    _ => string.Empty
                })
                .AddChoices(1, 2, 3, 4, 5, 6));

        switch (action)
        {
            case 1:
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

            int maxRows = WindowStyler.GetResponsivePageSize(reservedHeaderRows: 12, minRows: 5);
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

            if (enabledOps.Count <= 1)
            {
                ThemeRenderer.Pause(theme);
                return;
            }

            var navChoices = Enumerable.Range(0, enabledOps.Count).Append(-1).ToList();
            int picked = AnsiConsole.Prompt(
                new SelectionPrompt<int>()
                    .Title($"  [{theme.CategoryLabelHex}]Inspect Step Tab[/]")
                    .HighlightStyle(new Style(
                        foreground: theme.ToColor(theme.BackgroundHex),
                        background: theme.ToColor(theme.PrimaryHex)))
                    .UseConverter(idx => idx == -1
                        ? "  ← Back to Stack Editor"
                        : $"  Step #{idx + 1}: {Markup.Escape(enabledOps[idx].DisplayTitle)} ({Markup.Escape(enabledOps[idx].Summary)})")
                    .AddChoices(navChoices));

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
        int action = AnsiConsole.Prompt(
            new SelectionPrompt<int>()
                .Title($"  [{theme.CategoryLabelHex}]Pipeline Presets[/]")
                .HighlightStyle(new Style(
                    foreground: theme.ToColor(theme.BackgroundHex),
                    background: theme.ToColor(theme.PrimaryHex)))
                .UseConverter(a => a switch
                {
                    1 => "  Load Saved Preset",
                    2 => "  Save Current Operation Stack as Preset",
                    3 => "  Delete a Preset",
                    4 => "  Back",
                    _ => string.Empty
                })
                .AddChoices(1, 2, 3, 4));

        if (action == 1)
        {
            var presets = PresetManager.LoadAllPresets();
            if (presets.Count == 0) return;

            var chosen = AnsiConsole.Prompt(
                new SelectionPrompt<PipelinePreset>()
                    .Title($"  [{theme.CategoryLabelHex}]Select Preset to Load[/]")
                    .UseConverter(p => $"  {Markup.Escape(p.Name)} [{theme.MutedHex}]({p.Config.Operations.Count} steps)[/]")
                    .AddChoices(presets));

            config.CopyFrom(chosen.Config);
            AnsiConsole.MarkupLine($"  [{theme.SuccessHex}]✓ Loaded preset '{Markup.Escape(chosen.Name)}'.[/]");
            ThemeRenderer.Pause(theme);
        }
        else if (action == 2)
        {
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

            var toDelete = AnsiConsole.Prompt(
                new SelectionPrompt<PipelinePreset>()
                    .Title($"  [{theme.WarningHex}]Select Preset to Delete[/]")
                    .UseConverter(p => $"  {Markup.Escape(p.Name)}")
                    .AddChoices(presets));

            PresetManager.DeletePreset(toDelete);
            AnsiConsole.MarkupLine($"  [{theme.SuccessHex}]✓ Deleted preset '{Markup.Escape(toDelete.Name)}'.[/]");
            ThemeRenderer.Pause(theme);
        }
    }
}
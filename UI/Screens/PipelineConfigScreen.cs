using MediaRenamer.Core.Models;
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
                new Markup($"[underline bold {theme.HeaderAccentHex}]Rename Pipeline Rules[/]"),
                HorizontalAlignment.Center));
            AnsiConsole.WriteLine();

            string scopeDisplay = config.Scope == CountScope.NameAndArtist ? "Name + Artist" : "Name Only";

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<int>()
                    .Title($"  [{theme.CategoryLabelHex}]Rule Settings[/]")
                    .PageSize(WindowStyler.GetResponsivePageSize(reservedHeaderRows: 8, minRows: 5))
                    .HighlightStyle(new Style(
                        foreground: theme.ToColor(theme.BackgroundHex),
                        background: theme.ToColor(theme.PrimaryHex),
                        decoration: Decoration.Bold))
                    .UseConverter(id => id switch
                    {
                        1 => $"  [{theme.HeaderAccentHex}]Presets (Load / Save / Delete)[/]",
                        2 => $"  Set Base Identity    [{theme.MutedHex}]→[/] {(string.IsNullOrEmpty(config.SetBaseIdentity) ? $"[{theme.MutedHex}]Keep Existing[/]" : $"[{theme.PrimaryHex}]'{Markup.Escape(config.SetBaseIdentity)}'[/]")}",
                        3 => $"  Split Name/Artist    [{theme.MutedHex}]→[/] {FormatSplitSummary(config, theme)}",
                        4 => $"  Find & Replace       [{theme.MutedHex}]→[/] {(string.IsNullOrEmpty(config.FindText) ? $"[{theme.MutedHex}]None[/]" : $"[{theme.TextHex}]'{Markup.Escape(config.FindText)}' → '{Markup.Escape(config.ReplaceText)}'[/]")}",
                        5 => $"  Prefix               [{theme.MutedHex}]→[/] {(string.IsNullOrEmpty(config.Prefix) ? $"[{theme.MutedHex}]None[/]" : $"[{theme.TextHex}]'{Markup.Escape(config.Prefix)}'[/]")}",
                        6 => $"  Suffix               [{theme.MutedHex}]→[/] {(string.IsNullOrEmpty(config.Suffix) ? $"[{theme.MutedHex}]None[/]" : $"[{theme.TextHex}]'{Markup.Escape(config.Suffix)}'[/]")}",
                        7 => $"  Case Conversion      [{theme.MutedHex}]→[/] [{theme.SecondaryHex}]{Markup.Escape(config.CaseMode.ToString())}[/]",
                        8 => $"  Strip Legacy Numbers [{theme.MutedHex}]→[/] {(config.StripLegacyNumbers ? $"[{theme.SuccessHex}]✓ ON (removes old ' 00', '_01')[/]" : $"[{theme.MutedHex}]OFF[/]")}",
                        9 => $"  Smart Auto-Count     [{theme.MutedHex}]→[/] {(config.AutoCount ? $"[{theme.SuccessHex}]✓ ENABLED[/]" : $"[{theme.MutedHex}]DISABLED[/]")}",
                        10 => $"  Compact Sequence Gaps[{theme.MutedHex}]→[/] {(config.CompactGaps ? $"[{theme.WarningHex}]✓ ON (001, 003 → 001, 002)[/]" : $"[{theme.MutedHex}]OFF[/]")}",
                        11 => $"  Auto-Count Scope     [{theme.MutedHex}]→[/] [{theme.PrimaryHex}]{Markup.Escape(scopeDisplay)}[/]",
                        12 => $"  Linked Group Index   [{theme.MutedHex}]→[/] {(config.LinkedNumberOverride.HasValue ? $"[{theme.WarningHex}]{config.LinkedNumberOverride.Value}[/]" : $"[{theme.MutedHex}]Disabled[/]")}",
                        13 => $"  [{theme.WarningHex}]Reset All Rules[/]",
                        14 => $"  [{theme.SuccessHex}]✓ Done[/]",
                        _ => string.Empty
                    })
                    .AddChoices(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14));

            switch (choice)
            {
                case 1:
                    ManagePresets(config, theme);
                    break;

                case 2:
                    config.SetBaseIdentity = AnsiConsole.Prompt(
                        new TextPrompt<string>($"  [{theme.CategoryLabelHex}]New Base Identity[/] [{theme.MutedHex}](e.g. 'Character - Artist', blank to keep) >[/]").AllowEmpty());
                    break;

                case 3:
                    ConfigureSplitRule(config, theme);
                    break;

                case 4:
                    config.FindText = AnsiConsole.Prompt(
                        new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Find[/] [{theme.MutedHex}](blank to clear) >[/]").AllowEmpty());
                    if (!string.IsNullOrEmpty(config.FindText))
                    {
                        config.ReplaceText = AnsiConsole.Prompt(
                            new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Replace With[/] [{theme.MutedHex}]>[/]").AllowEmpty());
                    }
                    break;

                case 5:
                    config.Prefix = AnsiConsole.Prompt(
                        new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Prefix[/] [{theme.MutedHex}](blank to clear) >[/]").AllowEmpty());
                    break;

                case 6:
                    config.Suffix = AnsiConsole.Prompt(
                        new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Suffix[/] [{theme.MutedHex}](blank to clear) >[/]").AllowEmpty());
                    break;

                case 7:
                    config.CaseMode = AnsiConsole.Prompt(
                        new SelectionPrompt<CaseTransform>()
                            .Title($"  [{theme.CategoryLabelHex}]Case Mode[/]")
                            .HighlightStyle(new Style(
                                foreground: theme.ToColor(theme.BackgroundHex),
                                background: theme.ToColor(theme.PrimaryHex)))
                            .AddChoices(CaseTransform.None, CaseTransform.TitleCase, CaseTransform.PascalCase, CaseTransform.CamelCase));
                    break;

                case 8:
                    config.StripLegacyNumbers = !config.StripLegacyNumbers;
                    break;

                case 9:
                    config.AutoCount = !config.AutoCount;
                    break;

                case 10:
                    config.CompactGaps = !config.CompactGaps;
                    if (config.CompactGaps)
                    {
                        config.LinkedNumberOverride = null; // Mutually exclusive with single-number group override
                    }
                    break;

                case 11:
                    config.Scope = config.Scope == CountScope.NameAndArtist
                        ? CountScope.NameOnly
                        : CountScope.NameAndArtist;
                    break;

                case 12:
                    string raw = AnsiConsole.Prompt(
                        new TextPrompt<string>($"  [{theme.CategoryLabelHex}]New Group Number[/] [{theme.MutedHex}](blank to disable) >[/]")
                            .AllowEmpty());
                    config.LinkedNumberOverride = int.TryParse(raw, out int parsedNum) && parsedNum >= 0 ? parsedNum : null;
                    if (config.LinkedNumberOverride.HasValue)
                    {
                        config.CompactGaps = false;
                    }
                    break;

                case 13:
                    config.Reset();
                    break;

                case 14:
                    return;
            }
        }
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
                    2 => "  Save Current Rules as Preset",
                    3 => "  Delete a Preset",
                    4 => "  Back",
                    _ => string.Empty
                })
                .AddChoices(1, 2, 3, 4));

        if (action == 1)
        {
            var presets = PresetManager.LoadAllPresets();
            if (presets.Count == 0)
            {
                AnsiConsole.MarkupLine($"  [{theme.WarningHex}]! No presets found.[/]");
                ThemeRenderer.Pause(theme);
                return;
            }

            var chosen = AnsiConsole.Prompt(
                new SelectionPrompt<PipelinePreset>()
                    .Title($"  [{theme.CategoryLabelHex}]Select Preset to Load[/]")
                    .HighlightStyle(new Style(
                        foreground: theme.ToColor(theme.BackgroundHex),
                        background: theme.ToColor(theme.PrimaryHex)))
                    .UseConverter(p => $"  {Markup.Escape(p.Name)}")
                    .AddChoices(presets));

            config.CopyFrom(chosen.Config);
            AnsiConsole.MarkupLine($"  [{theme.SuccessHex}]✓ Loaded preset '{Markup.Escape(chosen.Name)}'.[/]");
            ThemeRenderer.Pause(theme);
        }
        else if (action == 2)
        {
            string name = AnsiConsole.Prompt(
                new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Preset Name[/] [{theme.MutedHex}]>[/]")
                    .AllowEmpty());

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
                    .HighlightStyle(new Style(
                        foreground: theme.ToColor(theme.BackgroundHex),
                        background: theme.ToColor(theme.ErrorHex)))
                    .UseConverter(p => $"  {Markup.Escape(p.Name)}")
                    .AddChoices(presets));

            PresetManager.DeletePreset(toDelete);
            AnsiConsole.MarkupLine($"  [{theme.SuccessHex}]✓ Deleted preset '{Markup.Escape(toDelete.Name)}'.[/]");
            ThemeRenderer.Pause(theme);
        }
    }

    private static void ConfigureSplitRule(RenamePipelineConfig config, AppTheme theme)
    {
        int splitMode = AnsiConsole.Prompt(
            new SelectionPrompt<int>()
                .Title($"  [{theme.CategoryLabelHex}]How should 'Name Artist' be split?[/]")
                .HighlightStyle(new Style(
                    foreground: theme.ToColor(theme.BackgroundHex),
                    background: theme.ToColor(theme.PrimaryHex)))
                .UseConverter(m => m switch
                {
                    1 => "  Split after N words (e.g., 1 word: 'Character John Doe' → 'Character - John Doe')",
                    2 => "  Split before Artist name (e.g., 'John Doe' → 'Character - John Doe')",
                    3 => "  Disable splitting",
                    _ => string.Empty
                })
                .AddChoices(1, 2, 3));

        if (splitMode == 1)
        {
            config.ArtistDelimiterHint = null;
            config.SplitNameAfterWords = AnsiConsole.Prompt(
                new TextPrompt<int>($"  [{theme.CategoryLabelHex}]Number of words in Name[/] [{theme.MutedHex}](e.g. 1 or 2) >[/]")
                    .DefaultValue(1));
        }
        else if (splitMode == 2)
        {
            config.SplitNameAfterWords = null;
            config.ArtistDelimiterHint = AnsiConsole.Prompt(
                new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Artist name to split before[/] [{theme.MutedHex}]>[/]")
                    .AllowEmpty());
        }
        else
        {
            config.SplitNameAfterWords = null;
            config.ArtistDelimiterHint = null;
        }
    }

    private static string FormatSplitSummary(RenamePipelineConfig cfg, AppTheme theme)
    {
        if (!string.IsNullOrWhiteSpace(cfg.ArtistDelimiterHint))
            return $"[{theme.PrimaryHex}]Before '{Markup.Escape(cfg.ArtistDelimiterHint)}'[/]";
        if (cfg.SplitNameAfterWords is > 0)
            return $"[{theme.PrimaryHex}]After {cfg.SplitNameAfterWords} word(s)[/]";
        return $"[{theme.MutedHex}]Off[/]";
    }
}
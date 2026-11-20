using MediaRenamer.Core.Models;
using MediaRenamer.Core.Pipeline;
using MediaRenamer.Theme;
using Spectre.Console;

namespace MediaRenamer.UI.Operations;

public sealed class ReplaceStringDescriptor : OperationDescriptor<ReplaceStringOperation>
{
    public override int SortOrder => 10;
    public override string MenuLabel => "Replace String (supports Regex & Case-Sensitive)";

    protected override void Configure(ReplaceStringOperation op, AppTheme theme)
    {
        string currentSearchHint = string.IsNullOrEmpty(op.SearchFor)
            ? string.Empty
            : $" [{theme.MutedHex}](current: '{Markup.Escape(op.SearchFor)}')[/]";

        string inputSearch = AnsiConsole.Prompt(
            new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Search for String / Pattern[/]{currentSearchHint} [{theme.MutedHex}]>[/]")
                .AllowEmpty());

        if (!string.IsNullOrEmpty(inputSearch))
            op.SearchFor = inputSearch;

        string currentReplaceDisplay = op.ReplaceWith == " "
            ? "' ' (single space)"
            : string.IsNullOrEmpty(op.ReplaceWith)
                ? "empty"
                : $"'{Markup.Escape(op.ReplaceWith)}'";

        op.ReplaceWith = AnsiConsole.Prompt(
            new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Replace with[/] [{theme.MutedHex}](current: {currentReplaceDisplay}; Space+Enter for whitespace, Enter for empty) >[/]")
                .AllowEmpty());

        op.UseRegex = AnsiConsole.Confirm($"  [{theme.TextHex}]Use Regular Expression?[/]", defaultValue: op.UseRegex);
        op.CaseSensitive = AnsiConsole.Confirm($"  [{theme.TextHex}]Case Sensitive?[/]", defaultValue: op.CaseSensitive);
    }
}

public sealed class SplitNameArtistDescriptor : OperationDescriptor<SplitNameArtistOperation>
{
    public override int SortOrder => 20;
    public override string MenuLabel => "Split Name / Artist (insert ' - ' after N words or before Artist)";

    protected override void Configure(SplitNameArtistOperation op, AppTheme theme)
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
    }
}

public sealed class SetBaseIdentityDescriptor : OperationDescriptor<SetBaseIdentityOperation>
{
    public override int SortOrder => 30;
    public override string MenuLabel => "Set Base Identity (replace entire Name - Artist stem)";

    protected override void Configure(SetBaseIdentityOperation op, AppTheme theme)
    {
        op.NewIdentity = AnsiConsole.Prompt(
            new TextPrompt<string>($"  [{theme.CategoryLabelHex}]New Base Identity[/] [{theme.MutedHex}](e.g. 'Character - Artist') >[/]")
                .DefaultValue(op.NewIdentity)
                .AllowEmpty());
    }
}

public sealed class AddAffixDescriptor : OperationDescriptor<AddAffixOperation>
{
    public override int SortOrder => 40;
    public override string MenuLabel => "Add Prefix / Suffix";

    protected override void Configure(AddAffixOperation op, AppTheme theme)
    {
        op.Prefix = AnsiConsole.Prompt(
            new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Prefix[/] [{theme.MutedHex}]>[/]")
                .DefaultValue(op.Prefix)
                .AllowEmpty());

        op.Suffix = AnsiConsole.Prompt(
            new TextPrompt<string>($"  [{theme.CategoryLabelHex}]Suffix[/] [{theme.MutedHex}]>[/]")
                .DefaultValue(op.Suffix)
                .AllowEmpty());
    }
}

public sealed class TrimCharactersDescriptor : OperationDescriptor<TrimCharactersOperation>
{
    public override int SortOrder => 50;
    public override string MenuLabel => "Trim Characters (remove N chars from start or end)";

    protected override void Configure(TrimCharactersOperation op, AppTheme theme)
    {
        op.TrimStartCount = AnsiConsole.Prompt(
            new TextPrompt<int>($"  [{theme.CategoryLabelHex}]Characters to trim from START[/] [{theme.MutedHex}]>[/]")
                .DefaultValue(op.TrimStartCount));

        op.TrimEndCount = AnsiConsole.Prompt(
            new TextPrompt<int>($"  [{theme.CategoryLabelHex}]Characters to trim from END[/] [{theme.MutedHex}]>[/]")
                .DefaultValue(op.TrimEndCount));
    }
}

public sealed class CaseTransformDescriptor : OperationDescriptor<CaseTransformOperation>
{
    public override int SortOrder => 60;
    public override string MenuLabel => "Case Conversion (TitleCase, PascalCase, CamelCase)";

    protected override void Configure(CaseTransformOperation op, AppTheme theme)
    {
        op.CaseMode = AnsiConsole.Prompt(
            new SelectionPrompt<CaseTransform>()
                .Title($"  [{theme.CategoryLabelHex}]Case Mode[/]")
                .AddChoices(CaseTransform.TitleCase, CaseTransform.PascalCase, CaseTransform.CamelCase, CaseTransform.None));
    }
}

public sealed class StripLegacyNumberDescriptor : OperationDescriptor<StripLegacyNumberOperation>
{
    public override int SortOrder => 70;
    public override string MenuLabel => "Strip Legacy Numbers (remove old trailing ' 00', '_01')";

    protected override void Configure(StripLegacyNumberOperation op, AppTheme theme)
    {
        // Parameterless operation — no prompt needed
    }
}
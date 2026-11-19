using MediaRenamer.Theme;
using Spectre.Console;

namespace MediaRenamer.UI;

public sealed record MenuOption<T>(T Value, string Label, char? Hotkey = null, bool IsBackOption = false);

public static class InteractiveMenu
{
    public static T Prompt<T>(
        string title,
        IReadOnlyList<MenuOption<T>> options,
        AppTheme theme,
        T? backValue = default,
        bool allowEscape = false,
        int pageSize = 14)
    {
        int selectedIndex = 0;
        int scrollOffset = 0;

        AnsiConsole.MarkupLine(title);
        int startTop = Console.CursorTop;
        int maxVisible = Math.Max(4, Math.Min(pageSize, Console.WindowHeight - startTop - 3));

        Console.CursorVisible = false;
        try
        {
            while (true)
            {
                if (selectedIndex < scrollOffset) scrollOffset = selectedIndex;
                if (selectedIndex >= scrollOffset + maxVisible) scrollOffset = selectedIndex - maxVisible + 1;

                for (int row = 0; row < maxVisible; row++)
                {
                    Console.SetCursorPosition(0, startTop + row);
                    Console.Write(new string(' ', Math.Max(1, Console.WindowWidth - 1)));
                    Console.SetCursorPosition(0, startTop + row);

                    int itemIdx = scrollOffset + row;
                    if (itemIdx >= options.Count) continue;

                    var opt = options[itemIdx];
                    bool isSelected = itemIdx == selectedIndex;
                    string keyBadge = opt.Hotkey.HasValue ? $"[{theme.MutedHex}][[{opt.Hotkey.Value}]][/] " : "    ";

                    if (isSelected)
                    {
                        string plain = Markup.Remove(opt.Label).TrimStart();
                        string keyPlain = opt.Hotkey.HasValue ? $"[{opt.Hotkey.Value}] " : "  ";
                        AnsiConsole.Markup($"  [{theme.BackgroundHex} on {theme.PrimaryHex} bold] ▸ {Markup.Escape(keyPlain)}{Markup.Escape(plain)} [/]");
                    }
                    else
                    {
                        AnsiConsole.Markup($"   {keyBadge}{opt.Label}");
                    }
                }

                // Footer keybinding legend
                int footerRow = Math.Min(Console.WindowHeight - 1, startTop + maxVisible + 1);
                Console.SetCursorPosition(0, footerRow);
                string escHint = allowEscape ? "  •  Esc Back" : string.Empty;
                AnsiConsole.Markup($"  [{theme.MutedHex}]↑/↓ Move  •  1-9 Quick Select  •  Enter Confirm{escHint}[/]");

                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.UpArrow)
                {
                    selectedIndex = (selectedIndex - 1 + options.Count) % options.Count;
                }
                else if (key.Key == ConsoleKey.DownArrow)
                {
                    selectedIndex = (selectedIndex + 1) % options.Count;
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    return options[selectedIndex].Value;
                }
                else if (allowEscape && (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Backspace))
                {
                    var backOpt = options.FirstOrDefault(o => o.IsBackOption);
                    return backOpt != null ? backOpt.Value : backValue!;
                }
                else if (char.IsLetterOrDigit(key.KeyChar))
                {
                    char pressed = char.ToUpperInvariant(key.KeyChar);
                    int matchIdx = -1;
                    for (int i = 0; i < options.Count; i++)
                    {
                        if (options[i].Hotkey.HasValue && char.ToUpperInvariant(options[i].Hotkey!.Value) == pressed)
                        {
                            matchIdx = i;
                            break;
                        }
                    }

                    if (matchIdx >= 0)
                    {
                        return options[matchIdx].Value;
                    }
                }
            }
        }
        finally
        {
            Console.CursorVisible = true;
        }
    }
}
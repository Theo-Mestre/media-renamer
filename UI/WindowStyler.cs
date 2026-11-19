using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using MediaRenamer.Theme;

namespace MediaRenamer.UI;

public static partial class WindowStyler
{
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    private static CancellationTokenSource? _resizeCts;
    private static int _lastWidth;
    private static int _lastHeight;

    [LibraryImport("kernel32.dll")]
    private static partial nint GetConsoleWindow();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(nint hConsoleHandle, uint dwMode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleScreenBufferInfoEx(nint hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFO_EX lpConsoleScreenBufferInfoEx);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleScreenBufferInfoEx(nint hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFO_EX lpConsoleScreenBufferInfoEx);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

    [LibraryImport("uxtheme.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SetWindowTheme(nint hwnd, string pszSubAppName, string? pszSubIdList);

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD { public short X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SMALL_RECT { public short Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct CONSOLE_SCREEN_BUFFER_INFO_EX
    {
        public uint cbSize;
        public COORD dwSize;
        public COORD dwCursorPosition;
        public ushort wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
        public ushort wPopupAttributes;
        public int bFullscreenSupported;
        public fixed uint ColorTable[16];
    }

    public static void ApplyWindowTheme(AppTheme theme, string windowTitle, int targetWidth = 128, int targetHeight = 42)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.Title = windowTitle;

        if (OperatingSystem.IsWindows())
        {
            EnableVirtualTerminalProcessing();
            ApplyWin32ConsolePalette(theme);
            SetInitialWindowSize(targetWidth, targetHeight);
            ApplyWindowsDwmStyling(theme);
            SyncBufferToWindowSize();
        }

        ApplyOscColors(theme);

        _lastWidth = Console.WindowWidth;
        _lastHeight = Console.WindowHeight;
    }

    public static void ApplyOscColors(AppTheme theme)
    {
        Console.Write($"\x1b]11;{theme.BackgroundHex}\x07");
        Console.Write($"\x1b]10;{theme.TextHex}\x07");
    }

    [SupportedOSPlatform("windows")]
    private static void EnableVirtualTerminalProcessing()
    {
        nint handle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (handle != nint.Zero && GetConsoleMode(handle, out uint mode))
        {
            SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        }
    }

    [SupportedOSPlatform("windows")]
    private static unsafe void ApplyWin32ConsolePalette(AppTheme theme)
    {
        nint handle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (handle == nint.Zero) return;

        var info = new CONSOLE_SCREEN_BUFFER_INFO_EX
        {
            cbSize = (uint)sizeof(CONSOLE_SCREEN_BUFFER_INFO_EX)
        };

        if (GetConsoleScreenBufferInfoEx(handle, ref info))
        {
            // Overwrite palette index 0 (default Black background) and index 7 (default White text)
            info.ColorTable[0] = (uint)HexToColorRef(theme.BackgroundHex);
            info.ColorTable[7] = (uint)HexToColorRef(theme.TextHex);

            // Prevent Get/SetConsoleScreenBufferInfoEx off-by-one window shrink quirk
            info.srWindow.Right += 1;
            info.srWindow.Bottom += 1;

            SetConsoleScreenBufferInfoEx(handle, ref info);
        }
    }

    public static void StartResizeWatcher(Action? onResized = null)
    {
        _resizeCts?.Cancel();
        _resizeCts = new CancellationTokenSource();
        var token = _resizeCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(120, token);

                int currentWidth = Console.WindowWidth;
                int currentHeight = Console.WindowHeight;

                if (currentWidth != _lastWidth || currentHeight != _lastHeight)
                {
                    await Task.Delay(150, token);
                    currentWidth = Console.WindowWidth;
                    currentHeight = Console.WindowHeight;

                    _lastWidth = currentWidth;
                    _lastHeight = currentHeight;

                    if (OperatingSystem.IsWindows())
                    {
                        SyncBufferToWindowSize();
                    }

                    onResized?.Invoke();
                }
            }
        }, token);
    }

    public static int GetResponsivePageSize(int reservedHeaderRows = 16, int minRows = 5)
    {
        int available = Console.WindowHeight - reservedHeaderRows;
        return Math.Max(minRows, available);
    }

    [SupportedOSPlatform("windows")]
    public static void SetInitialWindowSize(int width, int height)
    {
        try
        {
            int safeWidth = Math.Min(width, Console.LargestWindowWidth);
            int safeHeight = Math.Min(height, Console.LargestWindowHeight);

            if (safeWidth > 0 && safeHeight > 0)
            {
                if (Console.BufferWidth < safeWidth || Console.BufferHeight < safeHeight)
                {
                    Console.SetBufferSize(
                        Math.Max(Console.BufferWidth, safeWidth),
                        Math.Max(Console.BufferHeight, safeHeight));
                }

                Console.SetWindowSize(safeWidth, safeHeight);
                Console.SetBufferSize(safeWidth, safeHeight);
            }
        }
        catch
        {
            // Ignore when hosted inside Windows Terminal tabs
        }
    }

    [SupportedOSPlatform("windows")]
    public static void SyncBufferToWindowSize()
    {
        try
        {
            int w = Console.WindowWidth;
            int h = Console.WindowHeight;
            if (w > 0 && h > 0 && (Console.BufferWidth != w || Console.BufferHeight != h))
            {
                Console.SetBufferSize(w, h);
            }
        }
        catch
        {
            // Ignore when buffer cannot shrink below active cursor row
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyWindowsDwmStyling(AppTheme theme)
    {
        nint hwnd = GetConsoleWindow();
        if (hwnd == nint.Zero) return;

        SetWindowTheme(hwnd, "DarkMode_Explorer", null);

        int useDarkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));

        int captionColorRef = HexToColorRef(theme.TitleBarHex);
        int textColorRef = HexToColorRef(theme.TitleBarTextHex);
        int borderColorRef = HexToColorRef(theme.BorderHex);

        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColorRef, sizeof(int));
        DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColorRef, sizeof(int));
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColorRef, sizeof(int));
    }

    private static int HexToColorRef(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return 0x00222827;

        int r = Convert.ToInt32(hex[0..2], 16);
        int g = Convert.ToInt32(hex[2..4], 16);
        int b = Convert.ToInt32(hex[4..6], 16);

        return (b << 16) | (g << 8) | r;
    }
}
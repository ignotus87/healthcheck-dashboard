using System;
using System.Runtime.InteropServices;

internal static class ConsoleHelper
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    public static void EnsureConsole() => AllocConsole();
    public static void ReleaseConsole() => FreeConsole();

    public static void ShowConsole()
    {
        IntPtr hWnd = GetConsoleWindow();
        if (hWnd != IntPtr.Zero)
        {
            ShowWindow(hWnd, SW_SHOW);
        }
    }

    public static void HideConsole()
    {
        IntPtr hWnd = GetConsoleWindow();
        if (hWnd != IntPtr.Zero)
        {
            ShowWindow(hWnd, SW_HIDE);
        }
    }

    public static bool IsConsoleVisible()
    {
        IntPtr hWnd = GetConsoleWindow();
        return hWnd != IntPtr.Zero && IsWindowVisible(hWnd);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindowVisible(IntPtr hWnd);
}
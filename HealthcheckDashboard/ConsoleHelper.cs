using System.Runtime.InteropServices;

internal static class ConsoleHelper
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    public static void EnsureConsole() => AllocConsole();
    public static void ReleaseConsole() => FreeConsole();
}
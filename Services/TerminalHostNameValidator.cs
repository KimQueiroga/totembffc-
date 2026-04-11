using System.Text.RegularExpressions;

namespace TotemBff.Services;

public static class TerminalHostNameValidator
{
    private static readonly Regex TerminalHostNameRegex = new(
        "^[A-Za-z0-9._-]{1,120}$",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    public static bool IsValid(string hostName)
    {
        return TerminalHostNameRegex.IsMatch(hostName);
    }
}

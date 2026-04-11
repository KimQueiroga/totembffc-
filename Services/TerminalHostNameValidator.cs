using System.Text.RegularExpressions;

namespace TotemBff.Services;

public static partial class TerminalHostNameValidator
{
    public static bool IsValid(string hostName)
    {
        return TerminalHostNameRegex().IsMatch(hostName);
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,120}$")]
    private static partial Regex TerminalHostNameRegex();
}

namespace FolderSwitch.Core.Activation;

/// <summary>Recognises the virtual-location forms that have no disk path: <c>shell:...</c> and <c>::{CLSID}</c>.</summary>
internal static class ShellForms
{
    public static bool IsShellForm(string target) =>
        target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
        || target.StartsWith("::{", StringComparison.OrdinalIgnoreCase);
}

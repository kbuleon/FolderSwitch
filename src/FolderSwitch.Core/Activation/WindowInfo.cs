namespace FolderSwitch.Core.Activation;

/// <summary>What <see cref="WindowClassifier"/> needs to know about a window.</summary>
/// <param name="ClassName">Win32 class name of the window.</param>
/// <param name="ProcessName">Name of the owning process, with or without the <c>.exe</c> suffix.</param>
/// <param name="DescendantClassNames">Win32 class names of all the descendant windows.</param>
public sealed record WindowInfo(string ClassName, string ProcessName, IReadOnlyList<string> DescendantClassNames)
{
    /// <summary>
    /// Control IDs of the descendant windows, in the same order as <see cref="DescendantClassNames"/>
    /// (entry <c>i</c> belongs to the window of class <c>DescendantClassNames[i]</c>). Empty when unknown.
    /// </summary>
    public IReadOnlyList<int> DescendantControlIds { get; init; } = [];
}

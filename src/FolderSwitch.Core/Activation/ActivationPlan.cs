namespace FolderSwitch.Core.Activation;

/// <summary>What to do when an item is activated, as decided by <see cref="ActivationRouter"/>.</summary>
/// <param name="Kind">The action to run.</param>
/// <param name="Target">
/// What the action works on: the folder to navigate to or open, or the file to open. For a file in a
/// file dialog it is the file's parent folder.
/// </param>
/// <param name="Fallback">The action to run on the same target if <paramref name="Kind"/> fails, if any.</param>
public sealed record ActivationPlan(ActivationKind Kind, string Target, ActivationKind? Fallback);

using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Persistence;

/// <summary>The configuration returned by <see cref="ConfigStore.Load"/> and how it was obtained.</summary>
public sealed record LoadResult(AppConfig Config, LoadOutcome Outcome);

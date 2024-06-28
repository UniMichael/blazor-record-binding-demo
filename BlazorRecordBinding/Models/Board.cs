using System.Collections.Immutable;

namespace BlazorRecordBinding.Models;

public record Board(Guid Id, string Name, bool Synced, ImmutableList<WorkItem> WorkItems);

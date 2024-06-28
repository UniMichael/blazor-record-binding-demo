using System.Collections.Immutable;

namespace BlazorRecordBinding.Models;

public record Project(string Name, ImmutableList<Board> Boards);

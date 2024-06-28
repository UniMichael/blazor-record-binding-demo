namespace BlazorRecordBinding.Models;

public record WorkItem(Guid Id, string Name, string Description = "", bool Done = false);
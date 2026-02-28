namespace NoteSphere.Api.Dtos;

public sealed record DashboardStatsDto(
    int TotalNotes,
    int ActiveNotes,
    int PinnedNotes,
    int ArchivedNotes,
    int DeletedNotes,
    int TagsUsed,
    int UpdatedLast7Days
);

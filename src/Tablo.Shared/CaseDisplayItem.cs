namespace Tablo.Shared;

/// <summary>Одна строка табло — одно сегодняшнее заседание.</summary>
public record CaseDisplayItem(
    int? HallId,
    string? HallName,
    string CaseNumber,
    string? Judge,
    CaseCategory Category,
    TimeSpan? EventTime,
    IReadOnlyList<PartyLine> Parties,
    string? Result);

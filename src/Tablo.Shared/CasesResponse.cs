namespace Tablo.Shared;

/// <summary>
/// Ответ сервера на /api/cases. <see cref="IsStale"/> = true означает, что
/// последний опрос БД не удался и клиент видит предыдущий успешный снимок
/// данных, а не ошибку.
/// </summary>
public record CasesResponse(
    IReadOnlyList<CaseDisplayItem> Cases,
    DateTimeOffset GeneratedAtUtc,
    bool IsStale,
    bool HasEverSucceeded);

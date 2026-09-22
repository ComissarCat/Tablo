using Tablo.Shared;

namespace Tablo.Client;

/// <summary>Чистое форматирование данных дела для отображения на табло.</summary>
public static class CaseFormatting
{
    public static string CategoryLabel(CaseCategory category) => category switch
    {
        CaseCategory.Civil => "Гражданское",
        CaseCategory.Administrative => "Административное",
        CaseCategory.Criminal => "Уголовное",
        _ => category.ToString()
    };

    /// <summary>
    /// Для гражданских/административных дел стороны группируются по роли
    /// (истцы отдельно, ответчики отдельно), для уголовных — просто
    /// "ФИО — статья" построчно.
    /// </summary>
    public static IReadOnlyList<string> FormatParties(CaseCategory category, IReadOnlyList<PartyLine> parties)
    {
        if (parties.Count == 0) return Array.Empty<string>();

        if (category == CaseCategory.Criminal)
        {
            return parties
                .Select(p => string.IsNullOrWhiteSpace(p.Extra) ? p.Name : $"{p.Name} — {p.Extra}")
                .ToList();
        }

        return parties
            .GroupBy(p => p.GroupLabel ?? "Прочие участники")
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(p => p.Name))}")
            .ToList();
    }

    public static string FormatResult(string? result) =>
        string.IsNullOrWhiteSpace(result) ? "рассмотрение не завершено" : result;

    public static string FormatTime(TimeSpan? time) =>
        time is { } t ? t.ToString(@"hh\:mm") : "время не указано";
}

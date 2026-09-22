namespace Tablo.Shared;

/// <summary>
/// Одна строка участника процесса.
/// Для гражданских/административных дел <see cref="GroupLabel"/> — роль
/// (истец/ответчик/административный истец/административный ответчик из
/// CATALOGCONTENT), <see cref="Extra"/> не используется.
/// Для уголовных дел <see cref="GroupLabel"/> не используется,
/// <see cref="Extra"/> — статья (U33_PARTS.MAIN_LAW_ARTICLE).
/// </summary>
public record PartyLine(string? GroupLabel, string Name, string? Extra);

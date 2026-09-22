namespace Tablo.Client;

/// <summary>
/// Локальные настройки одного табло. Читаются из tablo.config.json рядом с
/// исполняемым файлом (см. tablo.config.sample.json).
/// </summary>
public sealed class ClientConfig
{
    /// <summary>Адрес Tablo.Server, например http://tablo-server:5000</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Название зала (как в CATALOGCONTENT.NAME — точный список текущих
    /// названий смотрите на сервере через GET /api/halls). Сравнение без
    /// учёта регистра и пробелов по краям. Null/пусто = все залы на день.
    /// </summary>
    public string? HallName { get; set; }

    public bool ShowCivil { get; set; } = true;
    public bool ShowAdministrative { get; set; } = true;
    public bool ShowCriminal { get; set; } = true;

    /// <summary>Масштаб интерфейса, 1.0 = 100%.</summary>
    public double UiScale { get; set; } = 1.0;

    public int PollIntervalSeconds { get; set; } = 20;
}

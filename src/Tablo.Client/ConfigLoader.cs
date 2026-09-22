using System.Text.Json;
using Serilog;

namespace Tablo.Client;

public static class ConfigLoader
{
    private const string FileName = "tablo.config.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Никогда не бросает исключение: при отсутствующем или битом конфиге
    /// возвращает настройки по умолчанию, чтобы табло всё равно запустилось.
    /// </summary>
    public static (ClientConfig Config, string? Warning) Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, FileName);
        try
        {
            if (!File.Exists(path))
            {
                var warning = $"Файл {FileName} не найден рядом с приложением, используются настройки по умолчанию";
                Log.Warning(warning);
                return (new ClientConfig(), warning);
            }

            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<ClientConfig>(json, JsonOptions);

            if (config is null)
            {
                var warning = $"Файл {FileName} пуст, используются настройки по умолчанию";
                Log.Warning(warning);
                return (new ClientConfig(), warning);
            }

            Log.Information("Конфигурация загружена из {Path}", path);
            return (config, null);
        }
        catch (Exception ex)
        {
            var warning = $"Не удалось прочитать {FileName}: {ex.Message}. Используются настройки по умолчанию";
            Log.Error(ex, warning);
            return (new ClientConfig(), warning);
        }
    }
}

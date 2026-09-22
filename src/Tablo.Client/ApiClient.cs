using System.Net.Http.Json;
using Serilog;
using Tablo.Shared;

namespace Tablo.Client;

/// <summary>
/// Обёртка над HttpClient для опроса Tablo.Server. Любая ошибка сети/сервера
/// глотается и превращается в null — вызывающий код (MainWindow) в этом
/// случае просто оставляет на экране предыдущие данные, ничего не показывая
/// пользователю модально.
/// </summary>
public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<CasesResponse?> TryGetCasesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _http.GetFromJsonAsync<CasesResponse>("/api/cases", cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось получить данные с сервера табло");
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}

using Tablo.Shared;

namespace Tablo.Server;

/// <summary>
/// Потокобезопасный держатель последнего успешно полученного снимка данных.
/// Опрос БД (<see cref="CaseDataPollerService"/>) обновляет его; если очередной
/// опрос не удался, снимок НЕ затирается — клиенты продолжают получать
/// последние известные данные с пометкой <see cref="CasesResponse.IsStale"/>.
/// </summary>
public sealed class CaseDataCache
{
    private readonly object _lock = new();
    private IReadOnlyList<CaseDisplayItem> _cases = Array.Empty<CaseDisplayItem>();
    private IReadOnlyList<HallInfo> _halls = Array.Empty<HallInfo>();
    private DateTimeOffset _lastSuccessUtc;
    private bool _hasEverSucceeded;
    private bool _lastPollFailed;

    public void ReportSuccess(IReadOnlyList<CaseDisplayItem> cases, IReadOnlyList<HallInfo> halls)
    {
        lock (_lock)
        {
            _cases = cases;
            _halls = halls;
            _lastSuccessUtc = DateTimeOffset.UtcNow;
            _hasEverSucceeded = true;
            _lastPollFailed = false;
        }
    }

    public void ReportFailure()
    {
        lock (_lock)
        {
            _lastPollFailed = true;
        }
    }

    public CasesResponse GetCasesResponse()
    {
        lock (_lock)
        {
            return new CasesResponse(_cases, _lastSuccessUtc, _lastPollFailed, _hasEverSucceeded);
        }
    }

    public IReadOnlyList<HallInfo> GetHalls()
    {
        lock (_lock)
        {
            return _halls;
        }
    }
}

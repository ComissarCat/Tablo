using Polly;
using Polly.Retry;

namespace Tablo.Server;

/// <summary>
/// Фоновый опрос Firebird по таймеру. Сбой одного цикла не роняет сервис и не
/// затирает последний успешный снимок в <see cref="CaseDataCache"/> — только
/// логируется и помечает кэш как устаревший.
/// </summary>
public sealed class CaseDataPollerService : BackgroundService
{
    private readonly FirebirdCaseRepository _repository;
    private readonly CaseDataCache _cache;
    private readonly ILogger<CaseDataPollerService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly ResiliencePipeline _retryPipeline;

    public CaseDataPollerService(
        FirebirdCaseRepository repository,
        CaseDataCache cache,
        IConfiguration configuration,
        ILogger<CaseDataPollerService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;

        var seconds = configuration.GetValue("Tablo:PollIntervalSeconds", 20);
        _pollInterval = TimeSpan.FromSeconds(Math.Max(5, seconds));

        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning(args.Outcome.Exception,
                        "Попытка {Attempt} опроса БД не удалась, повтор", args.AttemptNumber + 1);
                    return default;
                }
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);
        do
        {
            await PollOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken stoppingToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var (cases, halls) = await _retryPipeline.ExecuteAsync(
                async ct => await _repository.GetTodayCasesAsync(ct),
                stoppingToken);

            _cache.ReportSuccess(cases, halls);
            _logger.LogInformation("Опрос БД успешен за {ElapsedMs} мс, дел на сегодня: {Count}",
                stopwatch.ElapsedMilliseconds, cases.Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // штатная остановка сервиса
        }
        catch (Exception ex)
        {
            _cache.ReportFailure();
            _logger.LogError(ex, "Опрос БД не удался после всех повторов за {ElapsedMs} мс, отдаём предыдущий снимок данных",
                stopwatch.ElapsedMilliseconds);
        }
    }
}

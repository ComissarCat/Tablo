using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Serilog;
using Tablo.Shared;

namespace Tablo.Client;

public partial class MainWindow : Window
{
    private ClientConfig _config = new();
    private ApiClient? _api;
    private DispatcherTimer? _timer;
    private CasesResponse? _lastGoodResponse;

    // Медленная автопрокрутка, когда список не помещается на экран целиком.
    private DispatcherTimer? _scrollTimer;
    private static readonly TimeSpan ScrollTickInterval = TimeSpan.FromMilliseconds(50);
    private const double ScrollSpeedPxPerSecond = 25;
    private const double ScrollPauseSeconds = 3;
    private double _scrollPauseRemaining;
    private bool _scrollPendingResetToTop;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        // Полноэкранное окно без рамки нечем закрыть мышью — Esc нужен и
        // для ручного тестирования/разворачивания, и на будущее для
        // обслуживания на месте.
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
        Closed += OnClosed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        // Всё, что может пойти не так (плохой конфиг, недоступная сеть),
        // не должно уронить окно табло — оно должно хотя бы открыться и
        // показать статус.
        try
        {
            var (config, warning) = ConfigLoader.Load();
            _config = config;
            if (warning is not null)
            {
                StatusText.Text = warning;
            }

            ApplyScale(_config.UiScale);
            ApplyFullScreen();
            UpdateTitle();

            _api = new ApiClient(_config.ApiBaseUrl);

            var interval = TimeSpan.FromSeconds(Math.Max(5, _config.PollIntervalSeconds));
            _timer = new DispatcherTimer { Interval = interval };
            _timer.Tick += async (_, _) => await PollAsync();
            _timer.Start();

            _scrollTimer = new DispatcherTimer { Interval = ScrollTickInterval };
            _scrollTimer.Tick += (_, _) => TickAutoScroll();
            _scrollTimer.Start();

            _ = PollAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка инициализации табло");
            StatusText.Text = "Ошибка инициализации, см. логи";
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer?.Stop();
        _scrollTimer?.Stop();
        _api?.Dispose();
    }

    /// <summary>
    /// Плавно листает список вниз, когда он не помещается на экран; у обоих
    /// краёв — небольшая пауза, чтобы дать дочитать, затем прыжок наверх и
    /// цикл повторяется. Ничего не делает, если весь список и так виден.
    /// </summary>
    private void TickAutoScroll()
    {
        if (_scrollPauseRemaining > 0)
        {
            _scrollPauseRemaining -= ScrollTickInterval.TotalSeconds;
            if (_scrollPauseRemaining <= 0 && _scrollPendingResetToTop)
            {
                ContentScroll.Offset = new Avalonia.Vector(0, 0);
                _scrollPendingResetToTop = false;
                _scrollPauseRemaining = ScrollPauseSeconds;
            }
            return;
        }

        var maxOffset = Math.Max(0, ContentScroll.Extent.Height - ContentScroll.Viewport.Height);
        if (maxOffset <= 0) return;

        var current = ContentScroll.Offset;
        if (current.Y > maxOffset)
        {
            // Контент пересчитался (обновились данные) и стал короче —
            // подтягиваем прокрутку, а не ждём естественного цикла.
            ContentScroll.Offset = new Avalonia.Vector(current.X, maxOffset);
            return;
        }

        var next = current.Y + ScrollSpeedPxPerSecond * ScrollTickInterval.TotalSeconds;
        if (next >= maxOffset)
        {
            ContentScroll.Offset = new Avalonia.Vector(current.X, maxOffset);
            _scrollPauseRemaining = ScrollPauseSeconds;
            _scrollPendingResetToTop = true;
        }
        else
        {
            ContentScroll.Offset = new Avalonia.Vector(current.X, next);
        }
    }

    private void ApplyScale(double scale)
    {
        var safeScale = scale is > 0.1 and < 10 ? scale : 1.0;
        if (ScaleContainer.LayoutTransform is ScaleTransform transform)
        {
            transform.ScaleX = safeScale;
            transform.ScaleY = safeScale;
        }
    }

    private void ApplyFullScreen()
    {
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        WindowState = WindowState.FullScreen;
    }

    private async Task PollAsync()
    {
        if (_api is null) return;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var response = await _api.TryGetCasesAsync(cts.Token);

        try
        {
            if (response is not null)
            {
                _lastGoodResponse = response;
                RenderCases(response);
            }

            UpdateStatus(response);
        }
        catch (Exception ex)
        {
            // Отрисовка не должна ронять приложение — просто логируем и
            // оставляем на экране то, что уже было показано.
            Log.Error(ex, "Ошибка отрисовки данных табло");
        }
    }

    private void UpdateStatus(CasesResponse? freshResponse)
    {
        var effective = freshResponse ?? _lastGoodResponse;
        if (effective is null)
        {
            StatusText.Text = "Нет связи с сервером табло, данные ещё не получены";
            StatusText.Foreground = Brush.Parse("#C97B2B");
            return;
        }

        var localTime = effective.GeneratedAtUtc.ToLocalTime();
        var isStale = effective.IsStale || freshResponse is null;
        StatusText.Text = isStale
            ? $"Обновлено {localTime:HH:mm:ss} (нет свежих данных, показаны последние известные)"
            : $"Обновлено {localTime:HH:mm:ss}";
        StatusText.Foreground = isStale ? Brush.Parse("#C97B2B") : Brush.Parse("#7A8896");
    }

    private void UpdateTitle()
    {
        var title = $"Список дел, назначенных к рассмотрению на {DateTime.Today:dd.MM.yyyy}";
        if (!string.IsNullOrWhiteSpace(_config.HallName))
        {
            title += $"  ·  Зал {_config.HallName}";
        }
        TitleText.Text = title;
    }

    private void RenderCases(CasesResponse response)
    {
        // Дата в заголовке должна оставаться верной, даже если табло работает
        // без перезапуска через полночь.
        UpdateTitle();

        var filtered = response.Cases.Where(MatchesFilter).ToList();

        ContentPanel.Children.Clear();

        if (!string.IsNullOrWhiteSpace(_config.HallName))
        {
            RenderFlatList(filtered);
        }
        else
        {
            RenderGroupedByHall(filtered);
        }

        if (filtered.Count == 0)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "На сегодня дел по заданным фильтрам нет",
                Foreground = Brush.Parse("#7A8896"),
                FontSize = 24,
                Margin = new Avalonia.Thickness(0, 40, 0, 0)
            });
        }
    }

    private bool MatchesFilter(CaseDisplayItem item)
    {
        if (!string.IsNullOrWhiteSpace(_config.HallName) &&
            !string.Equals(item.HallName?.Trim(), _config.HallName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return item.Category switch
        {
            CaseCategory.Civil => _config.ShowCivil,
            CaseCategory.Administrative => _config.ShowAdministrative,
            CaseCategory.Criminal => _config.ShowCriminal,
            _ => true
        };
    }

    // Дела без времени заседания уходят в конец списка, а не вперемешку.
    private static readonly TimeSpan NoTimeSortKey = TimeSpan.MaxValue;

    private void RenderFlatList(IReadOnlyList<CaseDisplayItem> cases)
    {
        foreach (var item in cases.OrderBy(c => c.EventTime ?? NoTimeSortKey).ThenBy(c => c.CaseNumber))
        {
            ContentPanel.Children.Add(BuildCaseCard(item));
        }
    }

    private void RenderGroupedByHall(IReadOnlyList<CaseDisplayItem> cases)
    {
        var groups = cases
            .GroupBy(c => c.HallName ?? "Зал не указан")
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = group.Key,
                Foreground = Brushes.White,
                FontSize = 30,
                FontWeight = FontWeight.Bold,
                Margin = new Avalonia.Thickness(0, 24, 0, 12)
            });

            foreach (var item in group.OrderBy(c => c.EventTime ?? NoTimeSortKey).ThenBy(c => c.CaseNumber))
            {
                // Название зала повторяем на каждой карточке, а не только в
                // заголовке группы: если дел много и список длинный,
                // заголовок зала уезжает вверх при автопрокрутке, и без
                // этого непонятно, к какому залу относятся видимые карточки.
                ContentPanel.Children.Add(BuildCaseCard(item, showHallName: true));
            }
        }
    }

    private static Border BuildCaseCard(CaseDisplayItem item, bool showHallName = false)
    {
        var body = new StackPanel { Spacing = 4 };

        var headerParts = new List<string>();
        if (showHallName && !string.IsNullOrWhiteSpace(item.HallName))
        {
            headerParts.Add($"Зал {item.HallName}");
        }
        headerParts.Add(CaseFormatting.FormatTime(item.EventTime));
        headerParts.Add(item.CaseNumber);
        headerParts.Add(CaseFormatting.CategoryLabel(item.Category));

        body.Children.Add(new TextBlock
        {
            Text = string.Join("  ·  ", headerParts),
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(item.Judge))
        {
            body.Children.Add(new TextBlock
            {
                Text = $"Судья: {item.Judge}",
                Foreground = Brush.Parse("#C7D0D8"),
                FontSize = 18
            });
        }

        foreach (var line in CaseFormatting.FormatParties(item.Category, item.Parties))
        {
            body.Children.Add(new TextBlock
            {
                Text = line,
                Foreground = Brush.Parse("#C7D0D8"),
                FontSize = 18,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });
        }

        body.Children.Add(new TextBlock
        {
            Text = $"Результат: {CaseFormatting.FormatResult(item.Result)}",
            Foreground = Brush.Parse("#8FD19E"),
            FontSize = 18,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        });

        return new Border
        {
            Background = Brush.Parse("#151B22"),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(20, 16),
            Margin = new Avalonia.Thickness(0, 0, 0, 12),
            Child = body,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }
}

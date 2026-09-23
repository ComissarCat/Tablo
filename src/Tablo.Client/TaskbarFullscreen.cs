using System;
using System.Runtime.InteropServices;
using Serilog;

namespace Tablo.Client;

/// <summary>
/// Явно сообщает панели задач Windows, что окно полноэкранное
/// (<c>ITaskbarList2::MarkFullscreenWindow</c>). Сама Explorer убирает
/// панель задач под полноэкранное окно только если оно активно, а при
/// автозапуске во время входа в систему Windows не даёт нашему окну стать
/// активным — и панель задач остаётся поверх табло. С пометкой панель
/// задач уступает независимо от активности. На не-Windows ничего не делает.
/// </summary>
internal static class TaskbarFullscreen
{
    private static bool _loggedError;

    public static void Mark(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero) return;

        try
        {
            // Объект создаём каждый раз заново: если Explorer перезапустился
            // (или ещё не был готов при прошлом вызове), старый экземпляр
            // уже ни к чему не привязан.
            var taskbar = (ITaskbarList2)new TaskbarListCoClass();
            try
            {
                taskbar.HrInit();
                taskbar.MarkFullscreenWindow(hwnd, true);
            }
            finally
            {
                Marshal.ReleaseComObject(taskbar);
            }
        }
        catch (Exception ex)
        {
            // Логируем один раз — вызывается по таймеру.
            if (!_loggedError)
            {
                _loggedError = true;
                Log.Warning(ex, "Не удалось пометить окно табло как полноэкранное для панели задач");
            }
        }
    }

    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarListCoClass
    {
    }

    [ComImport]
    [Guid("602D4995-B13A-429b-A66E-1935E44F4317")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList2
    {
        // ITaskbarList
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);

        // ITaskbarList2
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
    }
}

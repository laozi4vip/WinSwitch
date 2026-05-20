namespace WinSwitch.Core.Models;

/// <summary>
/// Win32 窗口信息
/// </summary>
public class WindowInfo
{
    public IntPtr Handle { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public bool IsVisible { get; set; }
    public bool IsTopLevel { get; set; }
    public int ExStyle { get; set; }

    public override string ToString() => $"[{Handle}] {ProcessName}: {Title}";
}

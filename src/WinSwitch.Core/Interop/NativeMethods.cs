using System.Runtime.InteropServices;
using System.Text;

namespace WinSwitch.Core.Interop;

/// <summary>
/// Win32 API 声明 — 窗口操作
/// </summary>
public static class NativeMethods
{
    #region Window Management

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLong64(IntPtr hWnd, int nIndex);

    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLong64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
    }

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLong64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetWindowLong64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool AllowSetForegroundWindow(uint dwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool FlashWindow(IntPtr hWnd, bool bInvert);

    #endregion

    #region Window Position (V2)

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    #endregion

    #region Hotkey

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    #endregion

    #region Tray Notification

    [DllImport("shell32.dll", SetLastError = true)]
    public static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
    }

    #endregion

    #region Delegates

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    #endregion

    #region Constants

    // ShowWindow commands
    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;
    public const int SW_MINIMIZE = 6;
    public const int SW_RESTORE = 9;
    public const int SW_SHOWNA = 8;

    // SetWindowPos flags
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public static readonly IntPtr HWND_TOP = new IntPtr(0);

    // GetWindowLong / SetWindowLong indices
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    // Extended window styles
    public const int WS_EX_APPWINDOW = 0x00040000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    // Hotkey modifiers
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // Tray notification
    public const uint NIM_ADD = 0x00000000;
    public const uint NIM_MODIFY = 0x00000001;
    public const uint NIM_DELETE = 0x00000002;
    public const uint NIF_MESSAGE = 0x00000001;
    public const uint NIF_ICON = 0x00000002;
    public const uint NIF_TIP = 0x00000004;
    public const uint NIF_INFO = 0x00000010;
    public const uint NIIF_INFO = 0x00000001;
    public const uint NIIF_WARNING = 0x00000002;
    public const uint NIIF_ERROR = 0x00000003;

    // WM messages
    public const int WM_HOTKEY = 0x0312;

    #endregion

    #region Simulated Input (Taskbar Hotkey)

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsIconic(IntPtr hWnd);

    // ── SendInput 常量 ──
    public const int INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYDOWN = 0x0000;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    // ── 虚拟键码 ──
    public const uint VK_LWIN = 0x5B;
    public const uint VK_RWIN = 0x5C;
    public const uint VK_LSHIFT = 0xA0;
    public const uint VK_RSHIFT = 0xA1;
    public const uint VK_LCONTROL = 0xA2;
    public const uint VK_RCONTROL = 0xA3;
    public const uint VK_LMENU = 0xA4;
    public const uint VK_RMENU = 0xA5;
    public const uint VK_0 = 0x30;
    public const uint VK_1 = 0x31;
    public const uint VK_2 = 0x32;
    public const uint VK_3 = 0x33;
    public const uint VK_4 = 0x34;
    public const uint VK_5 = 0x35;
    public const uint VK_6 = 0x36;
    public const uint VK_7 = 0x37;
    public const uint VK_8 = 0x38;
    public const uint VK_9 = 0x39;

    // ── SendInput 结构体（使用 Union 布局） ──
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    /// <summary>
    /// 获取数字对应的虚拟键码 (0-9)
    /// </summary>
    public static uint GetNumberVk(int number)
    {
        return number switch
        {
            0 => VK_0,
            1 => VK_1,
            2 => VK_2,
            3 => VK_3,
            4 => VK_4,
            5 => VK_5,
            6 => VK_6,
            7 => VK_7,
            8 => VK_8,
            9 => VK_9,
            _ => VK_0
        };
    }

    /// <summary>
    /// 发送 Win + 数字键组合，模拟切换任务栏应用
    /// 关键：先释放当前物理按下的修饰键，避免组合键冲突
    /// </summary>
    public static void SendWinNumber(int number)
    {
        if (number < 0 || number > 9) return;
        uint vkNumber = GetNumberVk(number);

        // 关键：先释放用户当前按住的修饰键（Ctrl/Alt/Shift）
        // 否则 SendInput 的 Win+数字会变成 Ctrl+Alt+Win+数字
        ReleaseModifiers();
        Thread.Sleep(30);

        // 发送 Win↓ → 数字↓ → 数字↑ → Win↑（每步间加延迟）
        SendKeyDown(VK_LWIN);
        Thread.Sleep(50);
        SendKeyDown(vkNumber);
        Thread.Sleep(30);
        SendKeyUp(vkNumber);
        Thread.Sleep(30);
        SendKeyUp(VK_LWIN);
    }

    /// <summary>
    /// 释放所有当前按下的修饰键（Ctrl/Alt/Shift/Win）
    /// 通过 GetAsyncKeyState 检测物理按键状态，只释放实际按下的键
    /// </summary>
    public static void ReleaseModifiers()
    {
        TryReleaseKey(VK_LCONTROL, 0x11);
        TryReleaseKey(VK_RCONTROL, 0x11);
        TryReleaseKey(VK_LMENU, 0x12);
        TryReleaseKey(VK_RMENU, 0x12);
        TryReleaseKey(VK_LSHIFT, 0x10);
        TryReleaseKey(VK_RSHIFT, 0x10);
    }

    /// <summary>
    /// 检测指定 VK 是否物理按下，如果是则发送释放
    /// </summary>
    private static void TryReleaseKey(uint vk, int vkCheck)
    {
        short state = GetAsyncKeyState(vkCheck);
        if ((state & 0x8000) != 0)
        {
            SendKeyUp(vk);
        }
    }

    /// <summary>
    /// 发送按键按下
    /// </summary>
    public static void SendKeyDown(uint vk)
    {
        var input = MakeKeyboardInput(vk, KEYEVENTF_KEYDOWN);
        uint sent = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        if (sent == 0)
        {
            int err = Marshal.GetLastWin32Error();
            System.Diagnostics.Trace.TraceError($"SendKeyDown 失败: vk=0x{vk:X2}, Win32Error={err}");
        }
    }

    /// <summary>
    /// 发送按键抬起
    /// </summary>
    public static void SendKeyUp(uint vk)
    {
        var input = MakeKeyboardInput(vk, KEYEVENTF_KEYUP);
        uint sent = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        if (sent == 0)
        {
            int err = Marshal.GetLastWin32Error();
            System.Diagnostics.Trace.TraceError($"SendKeyUp 失败: vk=0x{vk:X2}, Win32Error={err}");
        }
    }

    private static INPUT MakeKeyboardInput(uint vk, uint flags)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)vk,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    #endregion
}
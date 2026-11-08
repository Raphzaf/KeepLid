using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using KeepLid.App.Native;
using KeepLid.Core.Abstractions;
using KeepLid.Core.Settings;

namespace KeepLid.App.Platform;

/// <summary>
/// Hidden top-level window that receives:
///  * WM_POWERBROADCAST / PBT_POWERSETTINGCHANGE for the lid switch and the active power scheme;
///  * WM_HOTKEY for the global shortcut.
/// (A top-level window is used rather than a message-only window, which does not receive broadcasts.)
/// </summary>
internal sealed class MessageWindow : IDisposable
{
    /// <summary>winnt.h GUID_LIDSWITCH_STATE_CHANGE {BA3E0F4D-B817-4094-A2D1-D56379E6A0F3}; data: 0 = closed, 1 = opened.</summary>
    private static readonly Guid LidSwitchStateChange = new("ba3e0f4d-b817-4094-a2d1-d56379e6a0f3");

    /// <summary>winnt.h GUID_ACTIVE_POWERSCHEME {31F9F286-5084-42FE-B720-2B0264993763}.</summary>
    private static readonly Guid ActivePowerScheme = new("31f9f286-5084-42fe-b720-2b0264993763");

    private const int HotkeyId = 0x4B4C; // "KL"

    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private readonly HwndSource _source;
    private readonly IntPtr _lidNotification;
    private readonly IntPtr _schemeNotification;
    private readonly ILog _log;
    private bool _hotkeyRegistered;

    public MessageWindow(ILog log)
    {
        _log = log;
        var parameters = new HwndSourceParameters("KeepLid.MessageWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,               // not WS_VISIBLE: never shown
            ExtendedWindowStyle = WS_EX_TOOLWINDOW,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        Guid lid = LidSwitchStateChange, scheme = ActivePowerScheme;
        _lidNotification = NativeMethods.RegisterPowerSettingNotification(_source.Handle, ref lid, NativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE);
        _schemeNotification = NativeMethods.RegisterPowerSettingNotification(_source.Handle, ref scheme, NativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE);
        if (_lidNotification == IntPtr.Zero) _log.Warn($"RegisterPowerSettingNotification(lid) failed: {Marshal.GetLastWin32Error()}");
    }

    /// <summary>true = lid closed. Windows sends the current state right after registration.</summary>
    public event Action<bool>? LidSwitchChanged;

    public event Action? ActiveSchemeChanged;

    public event Action? HotkeyPressed;

    public IntPtr Handle => _source.Handle;

    /// <summary>Registers (or re-registers) the global shortcut. Returns false if another app owns it.</summary>
    public bool SetHotkey(Hotkey? hotkey)
    {
        if (_hotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _hotkeyRegistered = false;
        }
        if (hotkey is not { } h) return true;

        _hotkeyRegistered = NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, h.Modifiers | NativeMethods.MOD_NOREPEAT, h.VirtualKey);
        if (!_hotkeyRegistered) _log.Warn($"RegisterHotKey failed: {Marshal.GetLastWin32Error()}");
        return _hotkeyRegistered;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeMethods.WM_POWERBROADCAST when wParam.ToInt32() == NativeMethods.PBT_POWERSETTINGCHANGE && lParam != IntPtr.Zero:
                // POWERBROADCAST_SETTING { GUID PowerSetting; DWORD DataLength; UCHAR Data[] }
                Guid setting = Marshal.PtrToStructure<Guid>(lParam);
                int dataLength = Marshal.ReadInt32(lParam, 16);
                if (setting == LidSwitchStateChange && dataLength >= 4)
                    LidSwitchChanged?.Invoke(Marshal.ReadInt32(lParam, 20) == 0);
                else if (setting == ActivePowerScheme)
                    ActiveSchemeChanged?.Invoke();
                handled = true;
                return new IntPtr(1); // TRUE
            case NativeMethods.WM_HOTKEY when wParam.ToInt32() == HotkeyId:
                HotkeyPressed?.Invoke();
                handled = true;
                break;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        SetHotkey(null);
        if (_lidNotification != IntPtr.Zero) NativeMethods.UnregisterPowerSettingNotification(_lidNotification);
        if (_schemeNotification != IntPtr.Zero) NativeMethods.UnregisterPowerSettingNotification(_schemeNotification);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}

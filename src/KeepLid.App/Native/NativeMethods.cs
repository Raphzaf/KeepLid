using System;
using System.Runtime.InteropServices;
using System.Text;

namespace KeepLid.App.Native;

/// <summary>
/// Win32 declarations. Every signature and constant was checked against the Windows SDK 10.0.26100 headers
/// (NuGet package Microsoft.Windows.SDK.CPP); the header is named next to each declaration.
/// </summary>
internal static class NativeMethods
{
    // ---------------------------------------------------------------------------------------------
    // Execution state — um/WinBase.h: EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);
    // um/winnt.h: ES_SYSTEM_REQUIRED 0x1, ES_DISPLAY_REQUIRED 0x2, ES_CONTINUOUS 0x80000000
    // ---------------------------------------------------------------------------------------------
    public const uint ES_SYSTEM_REQUIRED = 0x00000001;
    public const uint ES_DISPLAY_REQUIRED = 0x00000002;
    public const uint ES_CONTINUOUS = 0x80000000;

    [DllImport("kernel32.dll")]
    public static extern uint SetThreadExecutionState(uint esFlags);

    // ---------------------------------------------------------------------------------------------
    // Power schemes — um/powersetting.h and um/powrprof.h (powrprof.dll). All return a Win32 error code.
    // ---------------------------------------------------------------------------------------------

    /// <summary>powersetting.h: DWORD PowerGetActiveScheme(HKEY, GUID** ActivePolicyGuid) — free with LocalFree.</summary>
    [DllImport("powrprof.dll")]
    public static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    /// <summary>powersetting.h: DWORD PowerSetActiveScheme(HKEY, CONST GUID* SchemeGuid).</summary>
    [DllImport("powrprof.dll")]
    public static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    /// <summary>powrprof.h: DWORD PowerReadACValueIndex(HKEY, CONST GUID*, CONST GUID*, CONST GUID*, LPDWORD).</summary>
    [DllImport("powrprof.dll")]
    public static extern uint PowerReadACValueIndex(IntPtr rootPowerKey, ref Guid scheme, ref Guid subGroup, ref Guid setting, out uint value);

    /// <summary>powrprof.h: DWORD PowerReadDCValueIndex(HKEY, CONST GUID*, CONST GUID*, CONST GUID*, LPDWORD).</summary>
    [DllImport("powrprof.dll")]
    public static extern uint PowerReadDCValueIndex(IntPtr rootPowerKey, ref Guid scheme, ref Guid subGroup, ref Guid setting, out uint value);

    /// <summary>powersetting.h: DWORD PowerWriteACValueIndex(HKEY, CONST GUID*, CONST GUID*, CONST GUID*, DWORD).</summary>
    [DllImport("powrprof.dll")]
    public static extern uint PowerWriteACValueIndex(IntPtr rootPowerKey, ref Guid scheme, ref Guid subGroup, ref Guid setting, uint value);

    /// <summary>powersetting.h: DWORD PowerWriteDCValueIndex(HKEY, CONST GUID*, CONST GUID*, CONST GUID*, DWORD).</summary>
    [DllImport("powrprof.dll")]
    public static extern uint PowerWriteDCValueIndex(IntPtr rootPowerKey, ref Guid scheme, ref Guid subGroup, ref Guid setting, uint value);

    /// <summary>
    /// powerbase.h: BOOLEAN GetPwrCapabilities(PSYSTEM_POWER_CAPABILITIES lpspc).
    /// winnt.h SYSTEM_POWER_CAPABILITIES starts with three BOOLEANs: PowerButtonPresent, SleepButtonPresent, LidPresent.
    /// A generous buffer is passed and LidPresent is read at offset 2.
    /// </summary>
    [DllImport("powrprof.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool GetPwrCapabilities(byte[] capabilities);

    public const int SystemPowerCapabilitiesBufferSize = 256;
    public const int LidPresentOffset = 2;

    // ---------------------------------------------------------------------------------------------
    // Battery — um/WinBase.h: BOOL GetSystemPowerStatus(LPSYSTEM_POWER_STATUS);
    // ACLineStatus: 0 = offline (battery), 1 = online, 255 = unknown. BatteryLifePercent: 0–100, 255 = unknown.
    // BatteryFlag 128 = no system battery.
    // ---------------------------------------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    // ---------------------------------------------------------------------------------------------
    // Power notifications — um/WinUser.h
    // WM_POWERBROADCAST 0x0218, PBT_POWERSETTINGCHANGE 0x8013, DEVICE_NOTIFY_WINDOW_HANDLE 0,
    // POWERBROADCAST_SETTING { GUID PowerSetting; DWORD DataLength; UCHAR Data[1]; }
    // ---------------------------------------------------------------------------------------------
    public const int WM_POWERBROADCAST = 0x0218;
    public const int PBT_POWERSETTINGCHANGE = 0x8013;
    public const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid powerSettingGuid, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterPowerSettingNotification(IntPtr handle);

    // ---------------------------------------------------------------------------------------------
    // Global hotkey — um/WinUser.h: WM_HOTKEY 0x0312, MOD_NOREPEAT 0x4000
    // BOOL RegisterHotKey(HWND hWnd, int id, UINT fsModifiers, UINT vk); BOOL UnregisterHotKey(HWND, int);
    // ---------------------------------------------------------------------------------------------
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ---------------------------------------------------------------------------------------------
    // Misc
    // ---------------------------------------------------------------------------------------------

    /// <summary>WinBase.h: HLOCAL LocalFree(HLOCAL hMem).</summary>
    [DllImport("kernel32.dll")]
    public static extern IntPtr LocalFree(IntPtr hMem);

    /// <summary>appmodel.h: LONG GetCurrentPackageFullName(UINT32*, PWSTR); APPMODEL_ERROR_NO_PACKAGE = 15700.</summary>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, StringBuilder? packageFullName);

    public const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    /// <summary>
    /// fileapi.h: DWORD GetFinalPathNameByHandleW(HANDLE, LPWSTR, DWORD, DWORD). Used to show the real
    /// location of the log file when MSIX redirects %LOCALAPPDATA%.
    /// </summary>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint GetFinalPathNameByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle hFile, StringBuilder path, uint cchFilePath, uint flags);
}

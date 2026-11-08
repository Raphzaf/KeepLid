using System;
using System.Runtime.InteropServices;
using KeepLid.App.Native;
using KeepLid.Core.Abstractions;

namespace KeepLid.App.Platform;

/// <summary>Real <see cref="IPowerSettings"/> backed by powrprof.dll (no elevation requested).</summary>
internal sealed class WindowsPowerSettings : IPowerSettings
{
    /// <summary>winnt.h GUID_SYSTEM_BUTTON_SUBGROUP {4F971E89-EEBD-4455-A8DE-9E59040E7347}.</summary>
    public static readonly Guid SubgroupButtons = new("4f971e89-eebd-4455-a8de-9e59040e7347");

    /// <summary>winnt.h GUID_LIDCLOSE_ACTION {5CA83367-6E45-459F-A27B-476B1D01C936}.</summary>
    public static readonly Guid LidCloseAction = new("5ca83367-6e45-459f-a27b-476b1d01c936");

    private readonly Lazy<bool> _lidPresent = new(DetectLid);

    public bool IsLidPresent => _lidPresent.Value;

    public Guid GetActiveScheme()
    {
        Check("PowerGetActiveScheme", NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out IntPtr ptr));
        try
        {
            return Marshal.PtrToStructure<Guid>(ptr);
        }
        finally
        {
            NativeMethods.LocalFree(ptr);
        }
    }

    public LidActions ReadLidActions(Guid scheme)
    {
        Guid sub = SubgroupButtons, setting = LidCloseAction;
        Check("PowerReadACValueIndex", NativeMethods.PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint ac));
        Check("PowerReadDCValueIndex", NativeMethods.PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint dc));
        return new LidActions(ac, dc);
    }

    public void WriteLidActions(Guid scheme, uint? ac, uint? dc)
    {
        Guid sub = SubgroupButtons, setting = LidCloseAction;
        if (ac is { } a)
            Check("PowerWriteACValueIndex", NativeMethods.PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, a));
        if (dc is { } d)
            Check("PowerWriteDCValueIndex", NativeMethods.PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, d));
    }

    public void ApplyScheme(Guid scheme) =>
        Check("PowerSetActiveScheme", NativeMethods.PowerSetActiveScheme(IntPtr.Zero, ref scheme));

    private static void Check(string operation, uint error)
    {
        if (error != 0) throw new PowerSettingsException(operation, error);
    }

    private static bool DetectLid()
    {
        var buffer = new byte[NativeMethods.SystemPowerCapabilitiesBufferSize];
        return NativeMethods.GetPwrCapabilities(buffer) && buffer[NativeMethods.LidPresentOffset] != 0;
    }
}

/// <summary>Battery / mains status from GetSystemPowerStatus.</summary>
internal sealed class WindowsPowerStatus : IPowerStatusProvider
{
    public PowerStatus GetStatus()
    {
        if (!NativeMethods.GetSystemPowerStatus(out var s)) return new PowerStatus(false, null);
        bool onBattery = s.ACLineStatus == 0;
        bool noBattery = (s.BatteryFlag & 128) != 0;
        int? percent = noBattery || s.BatteryLifePercent == 255 ? null : s.BatteryLifePercent;
        return new PowerStatus(onBattery, percent);
    }
}

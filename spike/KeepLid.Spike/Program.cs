// KeepLid — Phase 0 feasibility spike.
//
// Question: can a NON-elevated process (bare exe AND packaged MSIX full-trust app) change the
// "Lid close action" of the active power scheme, re-apply the scheme, and restore the original value?
//
// Steps:
//   1. read the current lid close action (AC + DC) of the active power scheme;
//   2. write 0 ("Do nothing") to AC and DC, re-apply the active scheme, read back to verify;
//   3. restore the original values, re-apply, read back to verify.
// The original values are ALWAYS restored in a finally block.
//
// Usage: KeepLid.Spike.exe [--out <result.json>]
// Exit codes: 0 = success, 2 = a write was refused (e.g. ERROR_ACCESS_DENIED), 1 = other failure.
//
// Sources (Windows SDK 10.0.26100, NuGet package Microsoft.Windows.SDK.CPP):
//   um/powersetting.h : PowerGetActiveScheme, PowerSetActiveScheme, PowerWriteACValueIndex, PowerWriteDCValueIndex
//   um/powrprof.h     : PowerReadACValueIndex, PowerReadDCValueIndex
//   um/winnt.h        : GUID_SYSTEM_BUTTON_SUBGROUP, GUID_LIDCLOSE_ACTION, POWERBUTTON_ACTION_INDEX_*,
//                       SYSTEM_POWER_CAPABILITIES (LidPresent is the 3rd BOOLEAN, offset 2)
//   shared/winerror.h : ERROR_SUCCESS (0), ERROR_FILE_NOT_FOUND (2), ERROR_ACCESS_DENIED (5)
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace KeepLid.Spike;

internal static class Native
{
    // winnt.h: DEFINE_GUID( GUID_SYSTEM_BUTTON_SUBGROUP, 0x4F971E89, 0xEEBD, 0x4455, 0xA8, 0xDE, 0x9E, 0x59, 0x04, 0x0E, 0x73, 0x47 );
    public static readonly Guid SubgroupButtons = new("4f971e89-eebd-4455-a8de-9e59040e7347");

    // winnt.h: DEFINE_GUID( GUID_LIDCLOSE_ACTION, 0x5CA83367, 0x6E45, 0x459F, 0xA2, 0x7B, 0x47, 0x6B, 0x1D, 0x01, 0xC9, 0x36 );
    public static readonly Guid LidCloseAction = new("5ca83367-6e45-459f-a27b-476b1d01c936");

    // winnt.h: POWERBUTTON_ACTION_INDEX_NOTHING 0, _SLEEP 1, _HIBERNATE 2, _SHUTDOWN 3
    public const uint ActionDoNothing = 0;

    // powersetting.h: DWORD PowerGetActiveScheme(HKEY UserRootPowerKey, GUID** ActivePolicyGuid); free with LocalFree.
    [DllImport("powrprof.dll")]
    public static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    // powersetting.h: DWORD PowerSetActiveScheme(HKEY UserRootPowerKey, CONST GUID* SchemeGuid);
    [DllImport("powrprof.dll")]
    public static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    // powrprof.h: DWORD PowerReadACValueIndex(HKEY, CONST GUID* Scheme, CONST GUID* SubGroup, CONST GUID* Setting, LPDWORD AcValueIndex);
    [DllImport("powrprof.dll")]
    public static extern uint PowerReadACValueIndex(IntPtr rootPowerKey, ref Guid scheme, ref Guid subGroup, ref Guid setting, out uint value);

    // powrprof.h: DWORD PowerReadDCValueIndex(HKEY, CONST GUID* Scheme, CONST GUID* SubGroup, CONST GUID* Setting, LPDWORD DcValueIndex);
    [DllImport("powrprof.dll")]
    public static extern uint PowerReadDCValueIndex(IntPtr rootPowerKey, ref Guid scheme, ref Guid subGroup, ref Guid setting, out uint value);

    // powersetting.h: DWORD PowerWriteACValueIndex(HKEY, CONST GUID* Scheme, CONST GUID* SubGroup, CONST GUID* Setting, DWORD AcValueIndex);
    [DllImport("powrprof.dll")]
    public static extern uint PowerWriteACValueIndex(IntPtr rootPowerKey, ref Guid scheme, ref Guid subGroup, ref Guid setting, uint value);

    // powersetting.h: DWORD PowerWriteDCValueIndex(HKEY, CONST GUID* Scheme, CONST GUID* SubGroup, CONST GUID* Setting, DWORD DcValueIndex);
    [DllImport("powrprof.dll")]
    public static extern uint PowerWriteDCValueIndex(IntPtr rootPowerKey, ref Guid scheme, ref Guid subGroup, ref Guid setting, uint value);

    // powrprof.h: BOOLEAN GetPwrCapabilities(PSYSTEM_POWER_CAPABILITIES lpspc);
    // We pass a generously sized buffer and read LidPresent at offset 2 (see winnt.h struct layout).
    [DllImport("powrprof.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool GetPwrCapabilities(byte[] capabilities);

    // WinBase.h: HLOCAL LocalFree(HLOCAL hMem);
    [DllImport("kernel32.dll")]
    public static extern IntPtr LocalFree(IntPtr hMem);

    // appmodel.h: LONG GetCurrentPackageFullName(UINT32* packageFullNameLength, PWSTR packageFullName);
    // Returns APPMODEL_ERROR_NO_PACKAGE (15700) when the process has no package identity.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetCurrentPackageFullName(ref uint length, StringBuilder? fullName);
}

internal sealed record Step(string Name, uint ErrorCode, string? Detail = null)
{
    public bool Ok => ErrorCode == 0;
}

internal static class Program
{
    private static readonly List<Step> Steps = new();

    private static int Main(string[] args)
    {
        string? outPath = null;
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--out") outPath = args[i + 1];

        var result = new Dictionary<string, object?>
        {
            ["timestampUtc"] = DateTime.UtcNow.ToString("o"),
            ["osVersion"] = Environment.OSVersion.VersionString,
            ["architecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["user"] = Environment.UserName,
            ["isElevatedAdmin"] = IsElevated(),
            ["packageFullName"] = GetPackageFullName(),
            ["lidPresent"] = IsLidPresent(),
        };

        int exitCode = Run(result);
        result["steps"] = Steps;
        result["exitCode"] = exitCode;
        result["verdict"] = exitCode switch
        {
            0 => "SUCCESS: lid close action written and restored without elevation issues",
            2 => "WRITE_REFUSED: the power service rejected the write (see error codes)",
            _ => "FAILURE: see steps",
        };

        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
        if (outPath != null) File.WriteAllText(outPath, json);
        return exitCode;
    }

    private static int Run(Dictionary<string, object?> result)
    {
        // 1. Active scheme.
        uint err = Native.PowerGetActiveScheme(IntPtr.Zero, out IntPtr pGuid);
        if (err != 0) { Steps.Add(new("PowerGetActiveScheme", err)); return 1; }
        Guid scheme = Marshal.PtrToStructure<Guid>(pGuid);
        Native.LocalFree(pGuid);
        Steps.Add(new("PowerGetActiveScheme", 0, scheme.ToString()));
        result["activeScheme"] = scheme.ToString();

        // 1b. Read the original AC/DC values.
        if (!Read(scheme, "original", out uint origAc, out uint origDc)) return 1;
        result["originalAc"] = origAc;
        result["originalDc"] = origDc;

        bool writeRefused = false;
        bool verifyFailed = false;
        try
        {
            // 2. Write 0 ("Do nothing") on AC and DC, then re-apply the active scheme.
            uint wAc = Write(scheme, ac: true, Native.ActionDoNothing, "write AC=0");
            uint wDc = Write(scheme, ac: false, Native.ActionDoNothing, "write DC=0");
            writeRefused = wAc != 0 || wDc != 0;
            Apply(scheme, "PowerSetActiveScheme (after write)");

            if (Read(scheme, "after write", out uint ac, out uint dc))
            {
                bool ok = ac == Native.ActionDoNothing && dc == Native.ActionDoNothing;
                Steps.Add(new("verify written values", ok ? 0u : 1u, $"AC={ac} DC={dc} expected 0/0"));
                verifyFailed |= !ok && !writeRefused;
            }
        }
        finally
        {
            // 3. Always restore the original values.
            Write(scheme, ac: true, origAc, $"restore AC={origAc}");
            Write(scheme, ac: false, origDc, $"restore DC={origDc}");
            Apply(scheme, "PowerSetActiveScheme (after restore)");
            if (Read(scheme, "after restore", out uint ac, out uint dc))
            {
                bool ok = ac == origAc && dc == origDc;
                Steps.Add(new("verify restored values", ok ? 0u : 1u, $"AC={ac} DC={dc} expected {origAc}/{origDc}"));
                verifyFailed |= !ok;
            }
        }

        if (writeRefused) return 2;
        return verifyFailed ? 1 : 0;
    }

    private static bool Read(Guid scheme, string label, out uint ac, out uint dc)
    {
        Guid sub = Native.SubgroupButtons, setting = Native.LidCloseAction;
        uint e1 = Native.PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out ac);
        uint e2 = Native.PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out dc);
        Steps.Add(new($"PowerReadACValueIndex ({label})", e1, $"value={ac}"));
        Steps.Add(new($"PowerReadDCValueIndex ({label})", e2, $"value={dc}"));
        return e1 == 0 && e2 == 0;
    }

    private static uint Write(Guid scheme, bool ac, uint value, string label)
    {
        Guid sub = Native.SubgroupButtons, setting = Native.LidCloseAction;
        uint e = ac
            ? Native.PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, value)
            : Native.PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, value);
        Steps.Add(new((ac ? "PowerWriteACValueIndex " : "PowerWriteDCValueIndex ") + $"({label})", e, Describe(e)));
        return e;
    }

    private static void Apply(Guid scheme, string label)
    {
        uint e = Native.PowerSetActiveScheme(IntPtr.Zero, ref scheme);
        Steps.Add(new(label, e, Describe(e)));
    }

    private static string Describe(uint code) => code switch
    {
        0 => "ERROR_SUCCESS",
        2 => "ERROR_FILE_NOT_FOUND",
        5 => "ERROR_ACCESS_DENIED",
        87 => "ERROR_INVALID_PARAMETER",
        _ => $"Win32 error {code}",
    };

    private static bool IsElevated()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string? GetPackageFullName()
    {
        uint len = 0;
        int rc = Native.GetCurrentPackageFullName(ref len, null);
        if (rc == 15700) return null; // APPMODEL_ERROR_NO_PACKAGE
        var sb = new StringBuilder((int)len);
        return Native.GetCurrentPackageFullName(ref len, sb) == 0 ? sb.ToString() : $"error {rc}";
    }

    private static bool IsLidPresent()
    {
        var buffer = new byte[256];
        return Native.GetPwrCapabilities(buffer) && buffer[2] != 0;
    }
}

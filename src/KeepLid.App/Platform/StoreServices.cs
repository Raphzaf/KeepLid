using System;
using System.Threading.Tasks;
using KeepLid.Core.Abstractions;
using Windows.ApplicationModel;
using Windows.Services.Store;

namespace KeepLid.App.Platform;

/// <summary>
/// "Start with Windows" through the MSIX StartupTask declared in Package.appxmanifest (TaskId "KeepLidStartup").
/// Disabled by default; only available when packaged. Nothing is written to the registry.
/// </summary>
internal static class StartupTaskService
{
    public const string TaskId = "KeepLidStartup";

    public static async Task<StartupTaskState?> GetStateAsync()
    {
        if (!PackageInfo.IsPackaged) return null;
        try
        {
            return (await StartupTask.GetAsync(TaskId)).State;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Returns the resulting state (the user may have disabled it in Windows Settings).</summary>
    public static async Task<StartupTaskState?> SetEnabledAsync(bool enabled)
    {
        if (!PackageInfo.IsPackaged) return null;
        try
        {
            StartupTask task = await StartupTask.GetAsync(TaskId);
            if (enabled) return await task.RequestEnableAsync();
            task.Disable();
            return task.State;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>Store license (paid app with a free trial). The check is local, done by the Windows Store client.</summary>
internal sealed class LicenseService(ILog log)
{
    private StoreContext? _context;

    /// <summary>False only when the Store reports an expired trial. Any error counts as licensed (fail-open).</summary>
    public bool IsActive { get; private set; } = true;

    public bool IsTrial { get; private set; }

    public TimeSpan? TrialRemaining { get; private set; }

    public async Task RefreshAsync()
    {
        if (!PackageInfo.IsPackaged) return; // developer build: always licensed
        try
        {
            _context ??= StoreContext.GetDefault();
            StoreAppLicense license = await _context.GetAppLicenseAsync();
            IsActive = license.IsActive;
            IsTrial = license.IsTrial;
            TrialRemaining = license.IsTrial && license.ExpirationDate > DateTimeOffset.Now
                ? license.ExpirationDate - DateTimeOffset.Now
                : null;
            log.Info($"License: active={IsActive}, trial={IsTrial}");
        }
        catch (Exception ex)
        {
            IsActive = true;
            log.Warn("License check unavailable (treated as licensed): " + ex.Message);
        }
    }

    /// <summary>Opens the Store purchase dialog for this app, falling back to its Store page.</summary>
    public async Task BuyAsync(IntPtr ownerWindow)
    {
        try
        {
            _context ??= StoreContext.GetDefault();
            WinRT.Interop.InitializeWithWindow.Initialize(_context, ownerWindow);
            StoreProductResult product = await _context.GetStoreProductForCurrentAppAsync();
            if (product.Product is null) return;
            StorePurchaseResult result = await _context.RequestPurchaseAsync(product.Product.StoreId);
            log.Info("Purchase result: " + result.Status);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            log.Warn("Purchase dialog failed: " + ex.Message);
            Shell.Open("ms-windows-store://downloadsandupdates");
        }
    }
}

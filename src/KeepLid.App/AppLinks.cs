using System.Globalization;

namespace KeepLid.App;

/// <summary>
/// Public pages (static files from store/04_confidentialite, published with GitHub Pages by
/// .github/workflows/pages.yml). Opening them is the only time KeepLid hands a URL to the browser;
/// the app itself never connects to the network.
/// </summary>
internal static class AppLinks
{
    public const string SiteRoot = "https://raphzaf.github.io/KeepLid/";

    private static bool French => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";

    public static string Privacy => SiteRoot + (French ? "privacy-fr.html" : "privacy-en.html");

    public static string Help => SiteRoot + (French ? "aide.html" : "help.html");

    /// <summary>Control Panel › Power Options › "Choose what closing the lid does".</summary>
    public const string LidControlPanelFile = "control.exe";
    public const string LidControlPanelArgs = "/name Microsoft.PowerOptions /page pageGlobalSettings";

    /// <summary>Settings › System › Power (fallback, and on Windows 11 the "Lid & power button controls").</summary>
    public const string PowerSettingsUri = "ms-settings:powersleep";
}

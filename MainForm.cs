using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace IosWebKitTester;

/// <summary>
/// Launcher for a REAL WebKit engine (not Chromium) using Playwright's built-in
/// iPhone / iPad device profiles (viewport, DPR, touch, iOS Safari user-agent).
/// </summary>
public class MainForm : Form
{
    const string DefaultUrl = "https://d8bj7sjc-49404.usw3.devtunnels.ms/Investigation/Index?category=cases";

    readonly ComboBox cboDevice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    readonly CheckBox chkLandscape = new() { Text = "Landscape", AutoSize = true };
    readonly TextBox txtUrl = new() { Width = 640, Text = DefaultUrl };
    readonly Button btnLaunch = new() { Text = "▶ Launch device", AutoSize = true };
    readonly Button btnGo = new() { Text = "Go", AutoSize = true };
    readonly Button btnBack = new() { Text = "◀ Back", AutoSize = true };
    readonly Button btnReload = new() { Text = "⟳ Reload", AutoSize = true };
    readonly Button btnShot = new() { Text = "📷 Screenshot", AutoSize = true };
    readonly Button btnClose = new() { Text = "✖ Close device", AutoSize = true };
    readonly Button btnInstall = new() { Text = "Install / update WebKit", AutoSize = true };
    readonly Button btnReal = new() { Text = "Test on REAL iPhone (BrowserStack)", AutoSize = true };
    readonly Label lblStatus = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0), MaximumSize = new Size(1100, 0) };

    IPlaywright? pw;
    IBrowser? browser;
    IBrowserContext? ctx;
    IPage? page;

    public MainForm()
    {
        Text = "iPhone / iPad WebKit Tester (real WebKit engine)";
        Size = new Size(1150, 230);
        StartPosition = FormStartPosition.CenterScreen;

        var row1 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
        row1.Controls.AddRange(new Control[]
        {
            new Label { Text = "Device", AutoSize = true, Padding = new Padding(0, 6, 0, 0) },
            cboDevice, chkLandscape, btnLaunch, btnClose, btnInstall
        });

        var row2 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 0, 6, 0) };
        row2.Controls.AddRange(new Control[] { btnBack, btnReload, txtUrl, btnGo, btnShot });

        var row3 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
        row3.Controls.AddRange(new Control[] { btnReal, lblStatus });

        Controls.Add(row3);
        Controls.Add(row2);
        Controls.Add(row1);

        btnLaunch.Click += async (_, _) => await Safe(LaunchAsync);
        btnClose.Click += async (_, _) => await Safe(CloseAllAsync);
        btnGo.Click += async (_, _) => await Safe(GoAsync);
        txtUrl.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Safe(GoAsync); }
        };
        btnReload.Click += async (_, _) => await Safe(async () => { if (page != null) await page.ReloadAsync(); });
        btnBack.Click += async (_, _) => await Safe(async () => { if (page != null) await page.GoBackAsync(); });
        btnShot.Click += async (_, _) => await Safe(ScreenshotAsync);
        btnInstall.Click += async (_, _) => await Safe(InstallWebKitAsync);
        btnReal.Click += (_, _) => Process.Start(new ProcessStartInfo("https://live.browserstack.com/") { UseShellExecute = true });

        Shown += async (_, _) => await Safe(InitAsync);
        FormClosing += (_, _) => { try { CloseAllAsync().GetAwaiter().GetResult(); pw?.Dispose(); } catch { } };
    }

    void Status(string s) => lblStatus.Text = s;

    async Task Safe(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex)
        {
            Status("Error: " + ex.Message);
            MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    async Task InitAsync()
    {
        // Longer download timeout for slow networks (only used if an install is needed)
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_CONNECTION_TIMEOUT", "180000");
        pw = await Playwright.CreateAsync();

        var names = pw.Devices.Keys
            .Where(k => (k.StartsWith("iPhone") || k.StartsWith("iPad")) && !k.EndsWith("landscape"))
            .OrderBy(k => k.StartsWith("iPad") ? 1 : 0)
            .ThenBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        cboDevice.Items.AddRange(names);
        var def = Array.FindIndex(names, n => n.Contains("iPhone 15 Pro") && !n.Contains("Max"));
        cboDevice.SelectedIndex = def >= 0 ? def : 0;
        Status($"{names.Length} iPhone/iPad profiles loaded. Pick a device and click Launch.");
    }

    static bool WebKitExeExists()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");
        return Directory.Exists(root)
            && Directory.GetDirectories(root, "webkit-*").Any(d => File.Exists(Path.Combine(d, "Playwright.exe")))
            && Directory.GetDirectories(root, "winldd-*").Any(d => File.Exists(Path.Combine(d, "PrintDeps.exe")));
    }

    async Task<bool> InstallWebKitAsync()
    {
        // The Microsoft mirror worked on this network; the default CDN timed out
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_HOST", "https://playwright.download.prss.microsoft.com");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_CONNECTION_TIMEOUT", "300000");

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            Status($"Installing WebKit, attempt {attempt}/3 (please wait)...");
            int code = await Task.Run(() => Microsoft.Playwright.Program.Main(new[] { "install", "webkit", "winldd" }));
            // FFmpeg (video recording only) may fail; WebKit alone is enough for this tool
            if (code == 0 || WebKitExeExists()) { Status("WebKit ready."); return true; }
        }
        Status("WebKit install FAILED after 3 attempts.");
        return false;
    }

    async Task LaunchAsync()
    {
        if (pw == null || cboDevice.SelectedItem == null) return;
        await CloseAllAsync();

        var name = (string)cboDevice.SelectedItem;
        if (chkLandscape.Checked && pw.Devices.ContainsKey(name + " landscape")) name += " landscape";
        var opts = pw.Devices[name];
        opts.Locale = "en-IN";
        opts.TimezoneId = "Asia/Kolkata";

        try
        {
            browser = await pw.Webkit.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist"))
        {
            // WebKit missing or wrong version: install it now, then retry once
            if (!await InstallWebKitAsync())
            {
                MessageBox.Show(
                    "WebKit could not be installed automatically.\n\nOpen PowerShell and run:\n\n" +
                    "cd \"" + AppContext.BaseDirectory + "\"\n" +
                    "powershell -ExecutionPolicy Bypass -File .\\playwright.ps1 install webkit\n\n" +
                    "Then click Launch again.", "Install WebKit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            browser = await pw.Webkit.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
        }
        browser.Disconnected += (_, _) => BeginInvoke(() => Status("Device window closed."));
        ctx = await browser.NewContextAsync(opts);

        // Skip the Dev Tunnels "Continue" page - only for *.devtunnels.ms requests
        await ctx.RouteAsync(new Regex(@"^https://[^/]*devtunnels\.ms/.*"), async route =>
        {
            var headers = new Dictionary<string, string>(route.Request.Headers)
            {
                ["X-Tunnel-Skip-AntiPhishing-Page"] = "true"
            };
            await route.ContinueAsync(new RouteContinueOptions { Headers = headers });
        });

        page = await ctx.NewPageAsync();
        var vp = opts.ViewportSize;
        Status($"Running: {name}  viewport {vp?.Width}x{vp?.Height}  DPR {opts.DeviceScaleFactor}  (real WebKit)");
        await GoAsync();
    }

    async Task GoAsync()
    {
        if (page == null) { await LaunchAsync(); return; }
        var url = txtUrl.Text.Trim();
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    async Task ScreenshotAsync()
    {
        if (page == null) return;
        using var sfd = new SaveFileDialog
        {
            Filter = "PNG image|*.png",
            FileName = $"{cboDevice.SelectedItem}_{(chkLandscape.Checked ? "landscape" : "portrait")}.png"
        };
        if (sfd.ShowDialog() != DialogResult.OK) return;
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = sfd.FileName, FullPage = false });
        Status("Saved " + sfd.FileName);
    }

    async Task CloseAllAsync()
    {
        try { if (ctx != null) await ctx.CloseAsync(); } catch { }
        try { if (browser != null) await browser.CloseAsync(); } catch { }
        page = null; ctx = null; browser = null;
    }
}

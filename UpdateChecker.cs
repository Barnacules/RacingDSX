using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RacingDSX
{
    /// <summary>
    /// Checks GitHub Releases for a newer version, downloads it, and performs
    /// an in-place update by launching a small helper batch script that swaps
    /// the exe while the app is not running, then restarts it.
    /// </summary>
    public static class UpdateChecker
    {
        // Change these two constants if the repo is ever moved.
        private const string GitHubOwner = "Barnacules";
        private const string GitHubRepo  = "RacingDSX";

        private static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            // GitHub API requires a User-Agent header.
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("RacingDSX", Program.VERSION));
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        }

        // ── Public entry point ────────────────────────────────────────────────

        /// <summary>
        /// Checks for a newer release on GitHub.  If one is found and the user
        /// agrees to update, the download + swap happens automatically.
        /// Call this from the UI thread (it uses async/await internally and
        /// marshals the prompt back to the UI thread via the passed <paramref name="owner"/>).
        /// </summary>
        public static async Task CheckForUpdatesAsync(IWin32Window owner, bool silent = false)
        {
            try
            {
                ReleaseInfo latest = await FetchLatestReleaseAsync();
                if (latest == null)
                {
                    if (!silent)
                        MessageBox.Show(owner,
                            "Could not retrieve release information from GitHub.",
                            "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Strip leading 'v' from tag name if present ("v0.6.8" → "0.6.8")
                string remoteVer = latest.TagName?.TrimStart('v') ?? string.Empty;

                if (!IsNewerVersion(remoteVer, Program.VERSION))
                {
                    if (!silent)
                        MessageBox.Show(owner,
                            $"You are already running the latest version ({Program.VERSION}).",
                            "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Find the right asset for this machine's architecture.
                string rid        = GetRuntimeIdentifier();
                string assetName  = $"RacingDSX-{remoteVer}-{rid}.zip";
                ReleaseAsset asset = FindAsset(latest, assetName);

                if (asset == null)
                {
                    if (!silent)
                        MessageBox.Show(owner,
                            $"A new version ({remoteVer}) is available but no download was found for your platform ({rid}).\n\n" +
                            $"Please update manually: https://github.com/{GitHubOwner}/{GitHubRepo}/releases",
                            "Update Available", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Prompt the user.
                string releaseNotes = TruncateNotes(latest.Body);
                var result = MessageBox.Show(owner,
                    $"A new version of RacingDSX is available!\n\n" +
                    $"  Current version : {Program.VERSION}\n" +
                    $"  New version     : {remoteVer}\n\n" +
                    $"Release notes:\n{releaseNotes}\n\n" +
                    $"Would you like to download and install the update now?\n" +
                    $"(The application will restart automatically.)",
                    "Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    await DownloadAndApplyUpdateAsync(owner, asset, remoteVer);
                }
            }
            catch (Exception ex)
            {
                if (!silent)
                    MessageBox.Show(owner,
                        $"Update check failed: {ex.Message}",
                        "Update Check Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── GitHub API helpers ────────────────────────────────────────────────

        private static async Task<ReleaseInfo> FetchLatestReleaseAsync()
        {
            string url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
            HttpResponseMessage response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            string json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ReleaseInfo>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private static ReleaseAsset FindAsset(ReleaseInfo release, string assetName)
        {
            if (release?.Assets == null) return null;
            foreach (var a in release.Assets)
                if (string.Equals(a.Name, assetName, StringComparison.OrdinalIgnoreCase))
                    return a;
            return null;
        }

        // ── Download + apply ──────────────────────────────────────────────────

        private static async Task DownloadAndApplyUpdateAsync(
            IWin32Window owner, ReleaseAsset asset, string newVersion)
        {
            // Show a simple progress dialog.
            using var progress = new UpdateProgressForm(asset.Name);
            progress.Show(owner);
            Application.DoEvents();

            try
            {
                string tempDir  = Path.Combine(Path.GetTempPath(), $"RacingDSX_update_{newVersion}");
                string zipPath  = Path.Combine(tempDir, asset.Name);
                string exePath  = Process.GetCurrentProcess().MainModule!.FileName;
                string exeDir   = Path.GetDirectoryName(exePath)!;
                string exeName  = Path.GetFileName(exePath);

                Directory.CreateDirectory(tempDir);

                // Download the zip.
                progress.SetStatus("Downloading...");
                Application.DoEvents();
                using (var resp = await _http.GetAsync(asset.BrowserDownloadUrl,
                           HttpCompletionOption.ResponseHeadersRead))
                {
                    resp.EnsureSuccessStatusCode();
                    long total   = resp.Content.Headers.ContentLength ?? -1;
                    long received = 0;
                    byte[] buf   = new byte[81920];

                    using var fs  = File.Create(zipPath);
                    using var src = await resp.Content.ReadAsStreamAsync();
                    int read;
                    while ((read = await src.ReadAsync(buf, 0, buf.Length)) > 0)
                    {
                        await fs.WriteAsync(buf, 0, read);
                        received += read;
                        if (total > 0)
                        {
                            int pct = (int)(received * 100 / total);
                            progress.SetProgress(pct);
                            Application.DoEvents();
                        }
                    }
                }

                // Extract, find the new exe inside the zip.
                progress.SetStatus("Extracting...");
                Application.DoEvents();
                string extractDir = Path.Combine(tempDir, "extracted");
                ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

                string newExePath = FindExeInDirectory(extractDir, exeName);
                if (newExePath == null)
                {
                    progress.Close();
                    MessageBox.Show(owner,
                        "Could not find the new executable inside the downloaded archive. Update aborted.",
                        "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Write a tiny batch script that:
                //   1. Waits for this process to exit
                //   2. Copies the new exe over the old one
                //   3. Restarts the app
                //   4. Deletes itself
                progress.SetStatus("Preparing installer...");
                Application.DoEvents();

                string scriptPath = Path.Combine(tempDir, "apply_update.bat");
                int    pid        = Process.GetCurrentProcess().Id;
                string script = $@"@echo off
rem  RacingDSX auto-updater  —  do not close this window
title RacingDSX Updater

rem  Wait until the old process exits (poll every second, timeout 30s)
set /a tries=0
:wait
tasklist /FI ""PID eq {pid}"" 2>NUL | find ""{pid}"" >NUL
if %ERRORLEVEL%==0 (
    if %tries% LSS 30 (
        timeout /t 1 /nobreak >NUL
        set /a tries+=1
        goto wait
    )
)

rem  Copy new exe over old
copy /Y ""{newExePath}"" ""{exePath}"" >NUL
if %ERRORLEVEL% NEQ 0 (
    echo Failed to copy the new executable. Please try a manual update.
    pause
    exit /b 1
)

rem  Restart the app
start """" ""{exePath}""

rem  Cleanup temp directory (best-effort)
timeout /t 2 /nobreak >NUL
rd /s /q ""{tempDir}"" 2>NUL

del ""%~f0""
";
                File.WriteAllText(scriptPath, script);

                progress.SetStatus("Launching updater...");
                Application.DoEvents();

                // Start the script hidden, then close this instance.
                var psi = new ProcessStartInfo("cmd.exe", $"/c \"{scriptPath}\"")
                {
                    WindowStyle     = ProcessWindowStyle.Hidden,
                    CreateNoWindow  = true,
                    UseShellExecute = true
                };
                Process.Start(psi);

                progress.Close();
                Application.Exit();
            }
            catch (Exception ex)
            {
                progress.Close();
                MessageBox.Show(owner,
                    $"Update failed: {ex.Message}\n\nPlease update manually from:\nhttps://github.com/{GitHubOwner}/{GitHubRepo}/releases",
                    "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Utility helpers ───────────────────────────────────────────────────

        private static bool IsNewerVersion(string remote, string local)
        {
            if (Version.TryParse(remote, out var r) && Version.TryParse(local, out var l))
                return r > l;
            // Fall back to string compare if parsing fails.
            return string.Compare(remote, local, StringComparison.Ordinal) > 0;
        }

        private static string GetRuntimeIdentifier()
        {
            // RuntimeInformation.ProcessArchitecture gives us the actual running arch.
            return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
                       == System.Runtime.InteropServices.Architecture.Arm64
                ? "win-arm64"
                : "win-x64";
        }

        private static string FindExeInDirectory(string dir, string exeName)
        {
            foreach (var f in Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories))
                if (string.Equals(Path.GetFileName(f), exeName, StringComparison.OrdinalIgnoreCase))
                    return f;
            return null;
        }

        private static string TruncateNotes(string notes, int maxLen = 400)
        {
            if (string.IsNullOrEmpty(notes)) return "(no release notes)";
            notes = notes.Trim();
            return notes.Length <= maxLen ? notes : notes[..maxLen] + "…";
        }

        // ── GitHub API JSON models ────────────────────────────────────────────

        private class ReleaseInfo
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; }

            [JsonPropertyName("body")]
            public string Body { get; set; }

            [JsonPropertyName("assets")]
            public ReleaseAsset[] Assets { get; set; }
        }

        private class ReleaseAsset
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("browser_download_url")]
            public string BrowserDownloadUrl { get; set; }

            [JsonPropertyName("size")]
            public long Size { get; set; }
        }
    }

    // ── Simple progress dialog ────────────────────────────────────────────────

    internal class UpdateProgressForm : Form
    {
        private readonly Label       _statusLabel;
        private readonly ProgressBar _progressBar;

        public UpdateProgressForm(string assetName)
        {
            Text            = "Updating RacingDSX";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterScreen;
            ClientSize      = new System.Drawing.Size(400, 110);
            ControlBox      = false;
            MaximizeBox     = false;
            MinimizeBox     = false;

            var title = new Label
            {
                Text      = $"Downloading: {assetName}",
                AutoSize  = false,
                Width     = 380,
                Height    = 20,
                Location  = new System.Drawing.Point(10, 10),
                Font      = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
            };

            _statusLabel = new Label
            {
                Text     = "Starting...",
                AutoSize = false,
                Width    = 380,
                Height   = 20,
                Location = new System.Drawing.Point(10, 35)
            };

            _progressBar = new ProgressBar
            {
                Minimum  = 0,
                Maximum  = 100,
                Value    = 0,
                Width    = 380,
                Height   = 25,
                Location = new System.Drawing.Point(10, 60),
                Style    = ProgressBarStyle.Continuous
            };

            Controls.Add(title);
            Controls.Add(_statusLabel);
            Controls.Add(_progressBar);
        }

        public void SetStatus(string status)  => _statusLabel.Text    = status;
        public void SetProgress(int percent)  => _progressBar.Value   = Math.Clamp(percent, 0, 100);
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace SE2ServerManager;

public partial class MainWindow : Window
{
    private sealed class Settings
    {
        public string GamePath { get; set; } = @"C:\SE2\Game";
        public string KitPath { get; set; } = @"C:\SE2\Kit";
        public string ServerName { get; set; } = "SE2MP_Server";
        public int Port { get; set; } = 27015;
    }

    private static readonly string SettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SE2ServerManager");
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");
    private static readonly string DsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpaceEngineers2.DedicatedServer");
    private static readonly string LogsDir = Path.Combine(DsDir, "Temp", "Logs");
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Process? server;
    private DateTime startedUtc;
    private TimeSpan prevCpu;
    private DateTime prevSample;
    private string? currentLog;
    private long logPosition;
    private string carry = "";
    private int shownLines;
    private bool starting;
    private bool restarting;

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();
        timer.Tick += (_, _) => { UpdateProcess(); ReadLog(); };
        timer.Start();
        Append("[MANAGER] Control interface initialized. No server is launched automatically.");
        Append("[MANAGER] Locate SE2 installation and point the kit directory to a no-spaces path.");
        TryAttach();
    }

    private void LoadSettings()
    {
        Settings s = new();
        try { if (File.Exists(SettingsFile)) s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsFile)) ?? s; } catch { }
        if (!Directory.Exists(s.GamePath)) s.GamePath = DetectSteamPath() ?? s.GamePath;
        if (!Directory.Exists(s.KitPath))
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, "Kit");
            s.KitPath = bundled.Contains(' ') ? s.KitPath : bundled;
        }
        GamePathBox.Text = s.GamePath; KitPathBox.Text = s.KitPath;
        ServerNameBox.Text = s.ServerName; PortBox.Text = s.Port.ToString();
        EndpointText.Text = $"0.0.0.0:{s.Port}";
    }

    private static string? DetectSteamPath()
    {
        foreach (string reg in new[] { @"SOFTWARE\WOW6432Node\Valve\Steam\Apps\1133870", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1133870" })
        {
            using var key = Registry.LocalMachine.OpenSubKey(reg);
            var val = key?.GetValue("InstallLocation")?.ToString();
            if (!string.IsNullOrEmpty(val) && File.Exists(Path.Combine(val, "Game2", "SpaceEngineers2.exe"))) return val;
        }
        return null;
    }

    private bool Validate(out string exe, out string plugin, out int port)
    {
        exe = Path.Combine(GamePathBox.Text.Trim(), "Game2", "SpaceEngineers2.exe");
        plugin = Path.Combine(KitPathBox.Text.Trim(), "DsProto.dll"); port = 0;
        if (!File.Exists(exe)) { Notify("Game executable not found. Select the SE2 installation folder (containing Game2). "); return false; }
        if (KitPathBox.Text.Contains(' ')) { Notify("DsProto prototype requires a kit path with no spaces, such as C:\\SE2\\Kit."); return false; }
        if (!File.Exists(plugin)) { Notify("DsProto.dll was not found in the specified kit directory."); return false; }
        if (!int.TryParse(PortBox.Text, out port) || port is < 1 or > 65535) { Notify("UDP port must be between 1 and 65535."); return false; }
        if (string.IsNullOrWhiteSpace(ServerNameBox.Text) || !Regex.IsMatch(ServerNameBox.Text, "^[a-zA-Z0-9_-]+$")) { Notify("Server name must contain only letters, numbers, underscores or hyphens."); return false; }
        return true;
    }

    private void StartServer(bool create)
    {
        if (server is { HasExited: false } || starting) { Notify("A server process is already managed."); return; }
        if (!Validate(out string exe, out string plugin, out int port)) return;
        SaveSettings(silent: true);
        try
        {
            var psi = new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = false, CreateNoWindow = true };
            psi.Environment["SteamAppId"] = "1133870";
            psi.Environment["SteamGameId"] = "1133870";
            foreach (var arg in new[] { "-allowMultiple", "-localDedicatedServer", "-nosplash", "-hiddenWindow", $"-plugins:{plugin}", "-dsproto:headless", $"-dsproto:bind:0.0.0.0:{port}", $"-dsproto:name:{ServerNameBox.Text.Trim()}", "-dsproto:noreport", create ? "-startContent:Survival" : "-startLast" }) psi.ArgumentList.Add(arg);
            server = Process.Start(psi) ?? throw new InvalidOperationException("Game process did not start.");
            startedUtc = DateTime.UtcNow; prevCpu = server.TotalProcessorTime; prevSample = startedUtc; starting = true;
            Append($"[MANAGER] Started {(create ? "new Survival world" : "last saved world")} (PID {server.Id}); waiting for [dsproto] startup confirmation.");
            FooterText.Text = "PROCESS STARTED // CHECK LOG FOR BIND / WORLD LOAD";
            UpdateProcess();
        }
        catch (Exception ex) { starting = false; Notify("Launch failed: " + ex.Message); }
    }

    private void TryAttach()
    {
        try
        {
            var candidates = Process.GetProcessesByName("SpaceEngineers2").Where(p => !p.HasExited).ToList();
            if (candidates.Count == 1)
            {
                // Cannot establish that a generic game process is a DsProto headless server.
                Append("[MANAGER] An SE2 process exists. Automatic attachment disabled because it cannot be identified safely.");
            }
        } catch { }
    }

    private void UpdateProcess()
    {
        if (server == null || server.HasExited)
        {
            if (server != null)
            {
                Append($"[MANAGER] Server process exited (code {server.ExitCode}).");
                server.Dispose(); server = null; starting = false;
            }
            StatusText.Text = "OFFLINE"; StatusText.Foreground = new SolidColorBrush(Color.FromRgb(230, 167, 88));
            StatusDot.Fill = StatusText.Foreground; UptimeText.Text = "00:00:00"; CpuText.Text = "—"; RamText.Text = "—"; StartButton.IsEnabled = true;
            return;
        }
        StatusText.Text = starting ? "STARTING / CHECK LOG" : "PROCESS RUNNING";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(67, 202, 240)); StatusDot.Fill = StatusText.Foreground;
        UptimeText.Text = (DateTime.UtcNow - startedUtc).ToString(@"dd\.hh\:mm\:ss");
        try
        {
            server.Refresh();
            var cpu = server.TotalProcessorTime; var now = DateTime.UtcNow;
            var sec = (now - prevSample).TotalSeconds;
            if (sec > 0) CpuText.Text = $"{Math.Max(0, (cpu - prevCpu).TotalSeconds / sec / Environment.ProcessorCount * 100):F1}%";
            RamText.Text = $"{server.WorkingSet64 / 1073741824.0:F2} GB";
            prevCpu = cpu; prevSample = now;
        } catch { }
        StartButton.IsEnabled = false;
    }

    private void ReadLog()
    {
        try
        {
            if (!Directory.Exists(LogsDir)) return;
            var newest = Directory.EnumerateFiles(LogsDir, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                .Select(f => new FileInfo(f)).OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
            if (newest == null) return;
            if (newest.FullName != currentLog) { currentLog = newest.FullName; logPosition = 0; carry = ""; Append("[MANAGER] Following " + newest.Name); LogPathText.Text = currentLog; }
            using var fs = new FileStream(currentLog!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (fs.Length < logPosition) { logPosition = 0; carry = ""; Append("[MANAGER] Log file truncated; restarting tail."); }
            if (fs.Length == logPosition) return;
            fs.Seek(logPosition, SeekOrigin.Begin);
            // Decode complete UTF-8 sequences only; leave up to four bytes at end for the next read.
            using var mem = new MemoryStream(); fs.CopyTo(mem); var data = mem.ToArray();
            int complete = data.Length;
            while (complete > 0 && (data[complete - 1] & 0xC0) == 0x80) complete--;
            if (complete > 0 && complete < data.Length)
            {
                int expected = data[complete - 1] switch { >= 0xF0 => 4, >= 0xE0 => 3, >= 0xC0 => 2, _ => 1 };
                if (data.Length - complete + 1 < expected) complete--;
            }
            if (complete <= 0) return;
            var txt = carry + System.Text.Encoding.UTF8.GetString(data, 0, complete);
            logPosition += complete;
            var parts = txt.Split('\n'); carry = parts[^1];
            foreach (var line in parts.Take(parts.Length - 1))
            {
                Append(line.TrimEnd('\r'));
                if (starting && line.Contains("[dsproto]", StringComparison.OrdinalIgnoreCase)) starting = false;
            }
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
        catch (Exception ex) { FooterText.Text = "LOG WARNING // " + ex.Message; }
    }

    private void Append(string value)
    {
        var paragraph = new Paragraph(new Run(value)) { Margin = new Thickness(0), LineHeight = 17 };
        paragraph.Foreground = value.Contains("error", StringComparison.OrdinalIgnoreCase) || value.Contains("exception", StringComparison.OrdinalIgnoreCase) ? Brushes.OrangeRed
            : value.Contains("warn", StringComparison.OrdinalIgnoreCase) ? Brushes.Orange
            : value.Contains("[MANAGER]", StringComparison.OrdinalIgnoreCase) ? new SolidColorBrush(Color.FromRgb(73, 199, 235))
            : value.Contains("[dsproto]", StringComparison.OrdinalIgnoreCase) ? new SolidColorBrush(Color.FromRgb(135, 226, 183))
            : new SolidColorBrush(Color.FromRgb(182, 206, 215));
        LogBox.Document.Blocks.Add(paragraph);
        if (++shownLines > 4000) { for (int n = 0; n < 800 && LogBox.Document.Blocks.FirstBlock != null; n++) LogBox.Document.Blocks.Remove(LogBox.Document.Blocks.FirstBlock); shownLines -= 800; }
        if (AutoScrollCheck.IsChecked == true) LogBox.ScrollToEnd();
    }

    private void Command(string command)
    {
        if (server == null || server.HasExited) { Notify("This manager has not started a server process. No command sent."); return; }
        try
        {
            if (!Directory.Exists(DsDir)) { Notify("Dedicated server command directory does not exist yet. Wait for server initialization."); return; }
            File.AppendAllText(Path.Combine(DsDir, "dsproto-cmd.txt"), command + Environment.NewLine);
            Append($"[MANAGER] Queued admin command: {command}. Awaiting server log confirmation.");
        }
        catch (Exception ex) { Notify("Unable to send command: " + ex.Message); }
    }

    private async Task StopAndRestart()
    {
        if (restarting) return;
        if (server == null || server.HasExited) { StartServer(false); return; }
        restarting = true;
        try
        {
            var pid = server.Id;
            Command("stop");
            FooterText.Text = "RESTART PENDING // WAITING FOR GRACEFUL STOP";
            for (int i = 0; i < 120; i++)
            {
                await Task.Delay(1000);
                if (server == null || server.HasExited) break;
            }
            if (server is { HasExited: false }) { Notify("Server did not exit within 120 seconds. Restart canceled; server left running."); return; }
            Append($"[MANAGER] Previous server PID {pid} stopped; starting last save.");
            StartServer(false);
        }
        finally { restarting = false; }
    }

    private void SaveSettings(bool silent = false)
    {
        if (!int.TryParse(PortBox.Text, out int port) || port < 1 || port > 65535) { if (!silent) Notify("Invalid UDP port."); return; }
        var s = new Settings { GamePath = GamePathBox.Text.Trim(), KitPath = KitPathBox.Text.Trim(), ServerName = ServerNameBox.Text.Trim(), Port = port };
        try { Directory.CreateDirectory(SettingsDir); File.WriteAllText(SettingsFile, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true })); EndpointText.Text = $"0.0.0.0:{port}"; if (!silent) Append("[MANAGER] Settings saved; launch arguments will take effect on next start."); }
        catch (Exception ex) { Notify(ex.Message); }
    }

    private static void Notify(string msg) => MessageBox.Show(msg, "SE2 Server Manager", MessageBoxButton.OK, MessageBoxImage.Information);
    private static string? BrowseFolder() { using var dlg = new Forms.FolderBrowserDialog { ShowNewFolderButton = false }; return dlg.ShowDialog() == Forms.DialogResult.OK ? dlg.SelectedPath : null; }
    private void BrowseGame_Click(object s, RoutedEventArgs e) { var path = BrowseFolder(); if (path != null) GamePathBox.Text = path; }
    private void BrowseKit_Click(object s, RoutedEventArgs e) { var path = BrowseFolder(); if (path != null) KitPathBox.Text = path; }
    private void SaveSettings_Click(object s, RoutedEventArgs e) => SaveSettings();
    private void StartLast_Click(object s, RoutedEventArgs e) => StartServer(false);
    private void CreateWorld_Click(object s, RoutedEventArgs e) { if (MessageBox.Show("Create a new Survival world? This starts the prototype with -startContent:Survival. Ensure your existing save is backed up.", "New world", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) StartServer(true); }
    private void Save_Click(object s, RoutedEventArgs e) => Command("save");
    private void Stop_Click(object s, RoutedEventArgs e) => Command("stop");
    private void Status_Click(object s, RoutedEventArgs e) => Command("status");
    private async void Restart_Click(object s, RoutedEventArgs e) => await StopAndRestart();
    private void Quit_Click(object s, RoutedEventArgs e) { if (MessageBox.Show("Quit WITHOUT SAVING? Unsaved progress will be lost.", "Force quit", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) Command("quit"); }
    private void ClearLog_Click(object s, RoutedEventArgs e) { LogBox.Document.Blocks.Clear(); shownLines = 0; Append("[MANAGER] Console view cleared; log file left unchanged."); }
    private void OpenLogFolder_Click(object s, RoutedEventArgs e) { Directory.CreateDirectory(LogsDir); Process.Start(new ProcessStartInfo("explorer.exe", $"\"{LogsDir}\"") { UseShellExecute = true }); }
    private void Firewall_Click(object s, RoutedEventArgs e)
    {
        if (!int.TryParse(PortBox.Text, out int port)) { Notify("Invalid port."); return; }
        try
        {
            var psi = new ProcessStartInfo("netsh", $"advfirewall firewall show rule name=all dir=in") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using var p = Process.Start(psi);
            var output = p?.StandardOutput.ReadToEnd() ?? ""; p?.WaitForExit(10000);
            Append($"[MANAGER] Firewall inspection completed. Verify an inbound UDP rule for port {port}, and configure router forwarding separately.");
            Notify($"Firewall rules can be inspected in Windows Defender Firewall with Advanced Security. Ensure an inbound UDP rule for {port}. Router port forwarding requires separate configuration.\n\nThis action does not change firewall settings or claim external port reachability.");
        } catch (Exception ex) { Notify("Firewall inspection failed: " + ex.Message); }
    }
}

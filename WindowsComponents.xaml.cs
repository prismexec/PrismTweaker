using PrismTweaker.Properties;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Text.Json;
using System.Globalization;
using System.Linq;

namespace PrismTweaker
{
    public partial class WindowsComponents : Page
    {
        private const string GameBackupFile = "game-optimization-backup.json";
        private static readonly (string Path, string Name, int Value)[] GameSettings =
        {
            ("Software\\Microsoft\\GameBar", "AllowAutoGameMode", 1),
            ("Software\\Microsoft\\GameBar", "AutoGameModeEnabled", 1),
            ("System\\GameConfigStore", "GameDVR_Enabled", 0),
            ("Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR", "AppCaptureEnabled", 0)
        };

        private sealed class RegistrySnapshot
        {
            public RegistrySnapshot() { }
            public string Path { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool Existed { get; set; }
            public RegistryValueKind Kind { get; set; }
            public string? Data { get; set; }
        }

        bool isLoaded = false;
        MainWindow mw = (MainWindow)Application.Current.MainWindow;
        public WindowsComponents()
        {
            InitializeComponent();

            if (checkWinEd() == "Core" || checkWinEd() == "CoreSingleLanguage" || checkWinEd() == "CoreCountrySpecific" || checkWinEd() == "CoreN")
            {
                gpedit.Visibility = Visibility.Visible;
                lgpGrid.Visibility = Visibility.Visible;
            }

            checkReg();
            LoadLang(Properties.Settings.Default.lang);
            RefreshGameButtons();
            isLoaded = true;
        }

        private void checkReg()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR"))
            {
                dvr.IsEnabled = (key?.GetValue("AllowGameDVR")?.Equals(0) != true);
            }

            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell"))
            {
                pwsh.IsEnabled = (key?.GetValue("ExecutionPolicy")?.ToString() ?? "") != "RemoteSigned";
            }

            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Photo Viewer\Capabilities\FileAssociations"))
            {
                pv.IsEnabled = (key?.GetValue(".jpg")?.ToString() != "PhotoViewer.FileAssoc.Tiff");
            }

            try
            {
                string bcdOutput = GetCmdOutput("bcdedit", "");
                hypervdis.IsEnabled = !bcdOutput.Contains("hypervisorlaunchtype    Off");
            }
            catch
            {
                hypervdis.IsEnabled = true;
            }
        }

        private string GetCmdOutput(string command, string arguments)
        {
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = command;
                    p.StartInfo.Arguments = arguments;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.CreateNoWindow = true;
                    p.Start();
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    return output;
                }
            }
            catch { return ""; }
        }

        private string checkWinEd()
        {
            string keyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string valueName = "EditionID";

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
            {
                object value = key.GetValue(valueName);
                return value.ToString();
            }
        }

        private void SetBtn(Button btn, string normalText, string appliedText)
        {
            btn.Content = btn.IsEnabled ? normalText : appliedText;
        }

        private void LoadLang(string lang)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var basel = MainWindow.Localization.LoadLocalization(languageCode, "base");
            var compon = MainWindow.Localization.LoadLocalization(languageCode, "compon");
            var tooltips = MainWindow.Localization.LoadLocalization(languageCode, "tooltips");
            string applied = basel["def"]["applied"];

            label.Text = compon["main"]["label"];
            directplay.Text = compon["main"]["directplay"];
            framework.Text = compon["main"]["framework"];
            photoviewer.Text = compon["main"]["photoviewer"];
            powershellscr.Text = compon["main"]["powershellscr"];
            xboxdvr.Text = compon["main"]["xboxdvr"];
            forcedis.Text = compon["main"]["forcedis"];
            winsxs.Text = compon["main"]["winsxs"];
            gpedit.Text = compon["main"]["gpedit"];
            gameHeading.Text = compon["main"]["gameHeading"];
            gameDescription.Text = compon["main"]["gameDescription"];
            gameApply.Content = compon["main"]["gameApply"];
            gameRestore.Content = compon["main"]["gameRestore"];

            SetBtn(dp, compon["main"]["install"], applied);
            SetBtn(dnet, compon["main"]["install"], applied);
            SetBtn(sxs, compon["main"]["reset"], applied);
            SetBtn(lgp, compon["main"]["enable"], applied);
            SetBtn(pv, compon["main"]["enable"], applied);
            SetBtn(pwsh, compon["main"]["enable"], applied);
            SetBtn(dvr, basel["def"]["apply"], applied);
            SetBtn(hypervdis, basel["def"]["apply"], applied);

            sys_tooltip_photo.Content = tooltips["main"]["photow"];
            sys_tooltip_powershell.Content = tooltips["main"]["powershell"];
            sys_tooltip_xbox.Content = tooltips["main"]["xbox"];
            sys_tooltip_hyperv.Content = tooltips["main"]["hyperv"];
            sys_tooltip_directplay.Content = tooltips["main"]["directplay"];
            RefreshGameButtons();
        }

        private static string GetGameBackupPath()
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrismTweaker");
            return Path.Combine(directory, GameBackupFile);
        }

        private void RefreshGameButtons()
        {
            bool hasBackup = File.Exists(GetGameBackupPath());
            gameApply.IsEnabled = !hasBackup;
            gameRestore.IsEnabled = hasBackup;
        }

        private static string EncodeRegistryValue(object value, RegistryValueKind kind) => kind switch
        {
            RegistryValueKind.Binary or RegistryValueKind.None => Convert.ToBase64String((byte[])value),
            RegistryValueKind.MultiString => JsonSerializer.Serialize((string[])value),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        private static object DecodeRegistryValue(string value, RegistryValueKind kind) => kind switch
        {
            RegistryValueKind.Binary or RegistryValueKind.None => Convert.FromBase64String(value),
            RegistryValueKind.MultiString => JsonSerializer.Deserialize<string[]>(value) ?? Array.Empty<string>(),
            RegistryValueKind.DWord => int.Parse(value, CultureInfo.InvariantCulture),
            RegistryValueKind.QWord => long.Parse(value, CultureInfo.InvariantCulture),
            _ => value
        };

        private static List<RegistrySnapshot> CaptureGameSettings()
        {
            var snapshots = new List<RegistrySnapshot>();
            foreach (var setting in GameSettings)
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(setting.Path, writable: false);
                bool exists = key != null && key.GetValueNames().Contains(setting.Name, StringComparer.OrdinalIgnoreCase);
                if (!exists)
                {
                    snapshots.Add(new RegistrySnapshot { Path = setting.Path, Name = setting.Name, Existed = false });
                    continue;
                }

                RegistryValueKind kind = key!.GetValueKind(setting.Name);
                object value = key.GetValue(setting.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)!;
                snapshots.Add(new RegistrySnapshot
                {
                    Path = setting.Path,
                    Name = setting.Name,
                    Existed = true,
                    Kind = kind,
                    Data = EncodeRegistryValue(value, kind)
                });
            }
            return snapshots;
        }

        private void gameApply_Click(object sender, RoutedEventArgs e)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var compon = MainWindow.Localization.LoadLocalization(languageCode, "compon");
            try
            {
                string backupPath = GetGameBackupPath();
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                File.WriteAllText(backupPath, JsonSerializer.Serialize(CaptureGameSettings(), new JsonSerializerOptions { WriteIndented = true }));

                foreach (var setting in GameSettings)
                {
                    using RegistryKey key = Registry.CurrentUser.CreateSubKey(setting.Path, writable: true)
                        ?? throw new InvalidOperationException($"Unable to open registry key {setting.Path}.");
                    key.SetValue(setting.Name, setting.Value, RegistryValueKind.DWord);
                }

                gameStatus.Text = compon["main"]["gameApplied"];
            }
            catch (Exception ex)
            {
                gameStatus.Text = $"{compon["main"]["gameError"]} {ex.Message}";
            }
            RefreshGameButtons();
        }

        private void gameRestore_Click(object sender, RoutedEventArgs e)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var compon = MainWindow.Localization.LoadLocalization(languageCode, "compon");
            string backupPath = GetGameBackupPath();
            try
            {
                var snapshots = JsonSerializer.Deserialize<List<RegistrySnapshot>>(File.ReadAllText(backupPath))
                    ?? throw new InvalidDataException("The saved gaming profile is empty.");

                foreach (var snapshot in snapshots)
                {
                    using RegistryKey? key = Registry.CurrentUser.OpenSubKey(snapshot.Path, writable: snapshot.Existed);
                    if (!snapshot.Existed)
                    {
                        key?.DeleteValue(snapshot.Name, throwOnMissingValue: false);
                        continue;
                    }

                    using RegistryKey writableKey = key ?? Registry.CurrentUser.CreateSubKey(snapshot.Path, writable: true)
                        ?? throw new InvalidOperationException($"Unable to open registry key {snapshot.Path}.");
                    writableKey.SetValue(snapshot.Name, DecodeRegistryValue(snapshot.Data ?? string.Empty, snapshot.Kind), snapshot.Kind);
                }

                File.Delete(backupPath);
                gameStatus.Text = compon["main"]["gameRestored"];
            }
            catch (Exception ex)
            {
                gameStatus.Text = $"{compon["main"]["gameError"]} {ex.Message}";
            }
            RefreshGameButtons();
        }
        private void pwsh_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("powershell", "-Command Set-ExecutionPolicy RemoteSigned -Force") { CreateNoWindow = true, UseShellExecute = false });
            mw.RebootNotify(1);
            MarkApplied(pwsh);
        }
        private void dp_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("cmd.exe", "/C dism /online /Enable-Feature /FeatureName:DirectPlay /All");
            MarkApplied(dp);
        }

        private void dnet_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("powershell.exe", "/C Add-WindowsCapability -Online -Name NetFx3~~~~"));
            MarkApplied(dnet);
        }
        private void sxs_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("cmd.exe", "/C dism /Online /Cleanup-Image /StartComponentCleanup /ResetBase");
            mw.RebootNotify(3);
            MarkApplied(sxs);
        }
        private void lgp_Click(object sender, RoutedEventArgs e)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var sr = MainWindow.Localization.LoadLocalization(languageCode, "sr");
            string batContent = @"
            pushd ""%~dp0""

            dir /b %SystemRoot%\servicing\Packages\Microsoft-Windows-GroupPolicy-ClientExtensions-Package~3*.mum >List.txt 
            dir /b %SystemRoot%\servicing\Packages\Microsoft-Windows-GroupPolicy-ClientTools-Package~3*.mum >>List.txt 

            for /f %%i in ('findstr /i . List.txt 2^>nul') do dism /online /norestart /add-package:""%SystemRoot%\servicing\Packages\%%i""";
            string tempBatFilePath = Path.Combine(Path.GetTempPath(), "script.bat");
            File.WriteAllText(tempBatFilePath, batContent);
            Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = "/c \"" + tempBatFilePath + "\"";
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = false;
            try
            {
                process.Start();

            }
            catch
            {

            }
            MarkApplied(lgp);
        }
        private void pv_Click(object sender, RoutedEventArgs e)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            try
            {
                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(@"Applications\photoviewer.dll\shell\open"))
                {
                    key.SetValue("MuiVerb", "@photoviewer.dll,-3043");
                }

                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(@"Applications\photoviewer.dll\shell\open\command"))
                {
                    key.SetValue("", @"%SystemRoot%\System32\rundll32.exe ""%ProgramFiles%\Windows Photo Viewer\PhotoViewer.dll"", ImageViewer_Fullscreen %1", RegistryValueKind.String);
                }

                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows Photo Viewer\Capabilities\FileAssociations"))
                {
                    key.SetValue(".bmp", "PhotoViewer.FileAssoc.Tiff");
                    key.SetValue(".gif", "PhotoViewer.FileAssoc.Tiff");
                    key.SetValue(".jpeg", "PhotoViewer.FileAssoc.Tiff");
                    key.SetValue(".jpg", "PhotoViewer.FileAssoc.Tiff");
                    key.SetValue(".png", "PhotoViewer.FileAssoc.Tiff");
                }
                MarkApplied(pv);
            }
            catch (Exception ex)
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show($"{ex.Message}");
            }
        }

        private void dvr_Click(object sender, RoutedEventArgs e)
        {
            Registry.CurrentUser.CreateSubKey(@"System\GameConfigStore").SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
            Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR").SetValue("AllowGameDVR", 0, RegistryValueKind.DWord);
            MarkApplied(dvr);
        }

        private void hypervdis_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("cmd.exe", "/c \"bcdedit /set hypervisorlaunchtype off\"");
            MarkApplied(hypervdis);
        }

        private void MarkApplied(Button btn)
        {
            btn.IsEnabled = false;
            var basel = MainWindow.Localization.LoadLocalization(Properties.Settings.Default.lang, "base");
            btn.Content = basel["def"]["applied"];
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using IniParser;
using IniParser.Model;
using MaterialDesignThemes.Wpf;

namespace AVUpdate
{
    public partial class MainWindow : Window
    {
        private readonly IniData _config;
        private const string ConfigFilePath = "config.ini";
        private const string LogFilePath = "update.log";
        private bool isDarkTheme;
        private bool isUpdating;
        private CancellationTokenSource updateCancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            var parser = new FileIniDataParser();
            if (!File.Exists(ConfigFilePath))
            {
                _config = CreateDefaultConfig();
                parser.WriteFile(ConfigFilePath, _config);
            }
            else
            {
                _config = parser.ReadFile(ConfigFilePath);
            }

            string theme = _config["Settings"]["Theme"];
            UpdateAppTheme(theme);
            UpdateThemeIcon(theme);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdatePathStatusUI));
        }

        private IniData CreateDefaultConfig()
        {
            var data = new IniData();
            data.Sections.AddSection("Settings");
            var s = data["Settings"];
            s["NetworkPath"] = @"\\network\updates";
            s["ArchiveName"] = "update*.zip";
            s["UseSecondaryPath"] = "false";
            s["SecondaryNetworkPath"] = @"\\backup\updates";
            s["SecondaryUsername"] = string.Empty;
            s["SecondaryPassword"] = string.Empty;
            s["UseCustomSource"] = "false";
            s["CustomSourcePath"] = string.Empty;
            s["Theme"] = "Dark";
            return data;
        }

        #region Theme Management
        private void UpdateAppTheme(string theme)
        {
            var dicts = Application.Current.Resources.MergedDictionaries;
            dicts.Clear();
            dicts.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml") });
            var themeDict = new ResourceDictionary
            {
                Source = new Uri(theme.Equals("Light", StringComparison.OrdinalIgnoreCase)
                    ? "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml"
                    : "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml")
            };
            dicts.Add(themeDict);
            dicts.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Primary/MaterialDesignColor.DeepPurple.xaml") });
            dicts.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Accent/MaterialDesignColor.Lime.xaml") });
        }

        private void UpdateThemeIcon(string theme)
        {
            if (theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                ThemeIcon.Kind = PackIconKind.WeatherSunny;
                isDarkTheme = true;
            }
            else
            {
                ThemeIcon.Kind = PackIconKind.WeatherNight;
                isDarkTheme = false;
            }
        }

        private async void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            string newTheme = isDarkTheme ? "Light" : "Dark";
            await UpdateThemeWithAnimation(newTheme);
        }

        private async Task UpdateThemeWithAnimation(string newTheme)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            BeginAnimation(OpacityProperty, fadeOut);
            await Task.Delay(300);
            UpdateAppTheme(newTheme);
            UpdateThemeIcon(newTheme);
            _config["Settings"]["Theme"] = newTheme;
            new FileIniDataParser().WriteFile(ConfigFilePath, _config);
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            BeginAnimation(OpacityProperty, fadeIn);
        }
        #endregion

        #region Settings
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new SettingsWindow(
                _config["Settings"]["NetworkPath"],
                _config["Settings"]["ArchiveName"],
                _config["Settings"]["UseSecondaryPath"] == "true",
                _config["Settings"]["SecondaryNetworkPath"],
                _config["Settings"]["SecondaryUsername"],
                _config["Settings"]["SecondaryPassword"],
                _config["Settings"]["UseCustomSource"] == "true",
                _config["Settings"]["CustomSourcePath"],
                _config["Settings"]["Theme"]);
            window.Owner = this;
            bool? result = window.ShowDialog();
            if (result == true)
            {
                _config["Settings"]["NetworkPath"] = window.NetworkPath;
                _config["Settings"]["ArchiveName"] = window.ArchiveName;
                _config["Settings"]["UseSecondaryPath"] = window.UseSecondaryPath.ToString().ToLower();
                _config["Settings"]["SecondaryNetworkPath"] = window.SecondaryNetworkPath;
                _config["Settings"]["SecondaryUsername"] = window.SecondaryUsername;
                _config["Settings"]["SecondaryPassword"] = window.SecondaryPassword;
                _config["Settings"]["UseCustomSource"] = window.UseCustomSource.ToString().ToLower();
                _config["Settings"]["CustomSourcePath"] = window.CustomSourcePath;
                _config["Settings"]["Theme"] = window.SelectedTheme;
                new FileIniDataParser().WriteFile(ConfigFilePath, _config);
                UpdateAppTheme(window.SelectedTheme);
                UpdateThemeIcon(window.SelectedTheme);
                UpdatePathStatusUI();
                ShowMessage("Настройки сохранены.");
            }
        }
        #endregion

        #region Update Logic
        private void ResetProgress()
        {
            ProgressBar.Value = 0;
            ProgressPercent.Text = "0%";
            StatusText.Text = "Ожидание запуска...";
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            ResetProgress();
            isUpdating = true;
            SetControlsEnabled(false);
            CancelButton.IsEnabled = true;
            updateCancellationTokenSource = new CancellationTokenSource();
            string mask = _config["Settings"]["ArchiveName"];
            string archive = _config["Settings"]["UseCustomSource"] == "true"
                ? FindCustomSourcePath(_config["Settings"]["CustomSourcePath"], mask)
                : FindCDROMPath(mask);
            if (archive == null)
            {
                ShowMessage("Архив не найден.");
                StatusText.Text = "Архив не найден.";
                FinishUpdate();
                return;
            }
            var targets = new List<(string Path, string Label)> { (_config["Settings"]["NetworkPath"], "Основной") };
            if (_config["Settings"]["UseSecondaryPath"] == "true")
                targets.Add((_config["Settings"]["SecondaryNetworkPath"], "Второй"));

            for (int i = 0; i < targets.Count; i++)
            {
                var (dest, label) = targets[i];
                ArchivePathText.Text = $"Архив: {Path.GetFileName(archive)}";
                TargetPathText.Text = $"{label} путь: {dest}";
                if (!CheckDirectory(dest, label)) continue;
                try
                {
                    StatusText.Text = $"{label}: очистка...";
                    await Task.Run(() => CleanDirectory(dest, updateCancellationTokenSource.Token), updateCancellationTokenSource.Token);
                    UpdateProgress((i + 1) * 100 / (targets.Count * 3));
                    StatusText.Text = $"{label}: копирование...";
                    string temp = Path.Combine(dest, Path.GetFileName(archive));
                    await Task.Run(() => File.Copy(archive, temp, true), updateCancellationTokenSource.Token);
                    UpdateProgress((i + 1) * 100 * 2 / (targets.Count * 3));
                    StatusText.Text = $"{label}: распаковка...";
                    await Task.Run(() => ZipFile.ExtractToDirectory(temp, dest), updateCancellationTokenSource.Token);
                    File.Delete(temp);
                    UpdateProgress((i + 1) * 100 * 3 / (targets.Count * 3));
                    ShowMessage($"[{label}] обновление успешно.");
                }
                catch (OperationCanceledException)
                {
                    ShowMessage($"[{label}] отменено.");
                    break;
                }
                catch (Exception ex)
                {
                    ShowMessage($"[{label}] ошибка: {ex.Message}");
                    LogError(ex);
                }
            }
            FinishUpdate();
        }
        #endregion

        #region Helpers
        private bool CheckDirectory(string path, string label)
        {
            bool exists;
            try
            {
                if (label == "Второй" && !string.IsNullOrWhiteSpace(_config["Settings"]["SecondaryUsername"]))
                {
                    using (var conn = new NetworkConnection(path, new NetworkCredential(_config["Settings"]["SecondaryUsername"], _config["Settings"]["SecondaryPassword"])) )
                    {
                        exists = Directory.Exists(path);
                    }
                }
                else
                {
                    exists = Directory.Exists(path);
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                exists = false;
            }
            if (!exists) ShowMessage($"[{label}] путь недоступен.");
            return exists;
        }

        private void FinishUpdate()
        {
            isUpdating = false;
            SetControlsEnabled(true);
            CancelButton.IsEnabled = false;
            StatusText.Text = "Готово";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => updateCancellationTokenSource?.Cancel();

        private void SetControlsEnabled(bool enabled)
        {
            UpdateButton.IsEnabled = enabled;
            SettingsButton.IsEnabled = enabled;
            ThemeButton.IsEnabled = enabled;
        }

        private void CleanDirectory(string path, CancellationToken token)
        {
            if (!Directory.Exists(path)) return;
            foreach (var f in Directory.GetFiles(path)) { token.ThrowIfCancellationRequested(); File.Delete(f); }
            foreach (var d in Directory.GetDirectories(path)) { token.ThrowIfCancellationRequested(); Directory.Delete(d, true); }
        }

        private string FindCDROMPath(string mask)
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.CDRom && drive.IsReady)
                {
                    var files = Directory.GetFiles(drive.RootDirectory.FullName, mask);
                    if (files.Length > 0) return files[0];
                }
            }
            return null;
        }

        private string FindCustomSourcePath(string folder, string mask)
        {
            if (!Directory.Exists(folder)) return null;
            var files = Directory.GetFiles(folder, mask);
            return files.Length > 0 ? files[0] : null;
        }

        private void UpdateProgress(double percent)
        {
            ProgressBar.Value = percent;
            ProgressPercent.Text = $"{(int)percent}%";
            if (percent < 40) ProgressBar.Foreground = new SolidColorBrush(Colors.OrangeRed);
            else if (percent < 80) ProgressBar.Foreground = new SolidColorBrush(Colors.Goldenrod);
            else ProgressBar.Foreground = new SolidColorBrush(Colors.LightGreen);
        }

        private void UpdatePathStatusUI()
        {
            bool primaryExists = Directory.Exists(_config["Settings"]["NetworkPath"]);
            PrimaryStatus.Fill = primaryExists ? Brushes.LightGreen : Brushes.IndianRed;
            bool showSecond = _config["Settings"]["UseSecondaryPath"] == "true";
            bool secondaryExists = CheckDirectory(_config["Settings"]["SecondaryNetworkPath"], "Второй");
            SecondaryStatus.Fill = secondaryExists ? Brushes.LightGreen : Brushes.IndianRed;
            SecondaryStatusPanel.Visibility = showSecond ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LogError(Exception ex)
        {
            try { File.AppendAllText(LogFilePath, $"{DateTime.Now} - {ex}\r\n"); } catch { }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (isUpdating)
            {
                e.Cancel = true;
                ShowMessage("Дождитесь завершения обновления или отмените его.");
            }
            base.OnClosing(e);
        }

        private void ShowMessage(string message) => MainSnackbar.MessageQueue?.Enqueue(message);
        #endregion
    }
}

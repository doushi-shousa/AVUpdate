using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
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
        private bool isDarkTheme = false;
        private bool isUpdating = false;
        private CancellationTokenSource updateCancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            var parser = new FileIniDataParser();

            if (!File.Exists(ConfigFilePath))
            {
                _config = new IniData();
                _config.Sections.AddSection("Settings");
                _config["Settings"]["NetworkPath"] = @"\\network\updates";
                _config["Settings"]["ArchiveName"] = "update*.zip";
                _config["Settings"]["UseSecondaryPath"] = "false";
                _config["Settings"]["SecondaryNetworkPath"] = @"\\x.x.x.x\c$\source";
                _config["Settings"]["SecondaryUsername"] = "";
                _config["Settings"]["SecondaryPassword"] = "";
                _config["Settings"]["UseCustomSource"] = "false";
                _config["Settings"]["CustomSourcePath"] = "";
                _config["Settings"]["Theme"] = "Dark";
                parser.WriteFile(ConfigFilePath, _config);
            }
            else
            {
                _config = parser.ReadFile(ConfigFilePath);
            }

            // Инициализация темы и статуса путей
            string theme = _config["Settings"]["Theme"];
            UpdateAppTheme(theme);
            UpdateThemeIcon(theme);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdatePathStatusUI));
        }

        private async Task UpdateThemeWithAnimation(string newTheme)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            this.BeginAnimation(OpacityProperty, fadeOut);
            await Task.Delay(300);

            UpdateAppTheme(newTheme);
            UpdateThemeIcon(newTheme);
            _config["Settings"]["Theme"] = newTheme;
            new FileIniDataParser().WriteFile(ConfigFilePath, _config);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            this.BeginAnimation(OpacityProperty, fadeIn);
        }

        private void UpdateAppTheme(string theme)
        {
            var dicts = Application.Current.Resources.MergedDictionaries;
            dicts.Clear();
            dicts.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml")
            });
            dicts.Add(new ResourceDictionary
            {
                Source = theme.Equals("Light", StringComparison.OrdinalIgnoreCase)
                    ? new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml")
                    : new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml")
            });
            dicts.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Primary/MaterialDesignColor.DeepPurple.xaml")
            });
            dicts.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Accent/MaterialDesignColor.Lime.xaml")
            });
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
                _config["Settings"].ContainsKey("Theme") ? _config["Settings"]["Theme"] : "Dark")
            { Owner = this };

            if (window.ShowDialog() == true)
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

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
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
                SetControlsEnabled(true);
                CancelButton.IsEnabled = false;
                isUpdating = false;
                return;
            }

            string dest = _config["Settings"]["NetworkPath"];
            ArchivePathText.Text = $"Архив: {Path.GetFileName(archive)}";
            TargetPathText.Text = $"Путь: {dest}";

            try
            {
                StatusText.Text = "Очистка каталога...";
                await Task.Run(() => CleanDirectory(dest, updateCancellationTokenSource.Token),
                               updateCancellationTokenSource.Token);
                UpdateProgress(15);

                StatusText.Text = "Копирование...";
                string tempZip = Path.Combine(dest, Path.GetFileName(archive));
                await Task.Run(() => File.Copy(archive, tempZip, true),
                               updateCancellationTokenSource.Token);
                UpdateProgress(50);

                StatusText.Text = "Распаковка...";
                await Task.Run(() => ZipFile.ExtractToDirectory(tempZip, dest),
                               updateCancellationTokenSource.Token);
                UpdateProgress(90);

                File.Delete(tempZip);
                UpdateProgress(100);
                StatusText.Text = "Готово";
                ShowMessage("Обновление завершено.");
            }
            catch (OperationCanceledException)
            {
                ShowMessage("Обновление отменено.");
                StatusText.Text = "Обновление отменено.";
            }
            catch (Exception ex)
            {
                ShowMessage("Ошибка обновления: " + ex.Message);
                StatusText.Text = "Ошибка обновления.";
                LogError(ex);
            }
            finally
            {
                isUpdating = false;
                SetControlsEnabled(true);
                CancelButton.IsEnabled = false;
                updateCancellationTokenSource.Dispose();
                updateCancellationTokenSource = null;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
            => updateCancellationTokenSource?.Cancel();

        private void SetControlsEnabled(bool enabled)
        {
            UpdateButton.IsEnabled = enabled;
            SettingsButton.IsEnabled = enabled;
            ThemeButton.IsEnabled = enabled;
        }

        private void CleanDirectory(string path, CancellationToken token)
        {
            if (!Directory.Exists(path)) return;
            foreach (var f in Directory.GetFiles(path))
            {
                token.ThrowIfCancellationRequested();
                File.Delete(f);
            }
            foreach (var d in Directory.GetDirectories(path))
            {
                token.ThrowIfCancellationRequested();
                Directory.Delete(d, true);
            }
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
            // Проверка основного пути
            bool primary = Directory.Exists(_config["Settings"]["NetworkPath"]);
            PrimaryStatus.Fill = primary ? Brushes.LightGreen : Brushes.IndianRed;

            // Проверка второго пути
            bool showSecondary = _config["Settings"]["UseSecondaryPath"] == "true";
            bool secondary = false;
            if (showSecondary)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(_config["Settings"]["SecondaryUsername"]) &&
                        !string.IsNullOrWhiteSpace(_config["Settings"]["SecondaryPassword"]))
                    {
                        using (var conn = new NetworkConnection(
                            _config["Settings"]["SecondaryNetworkPath"],
                            new NetworkCredential(
                                _config["Settings"]["SecondaryUsername"],
                                _config["Settings"]["SecondaryPassword"])))
                        {
                            secondary = Directory.Exists(_config["Settings"]["SecondaryNetworkPath"]);
                        }
                    }
                    else
                    {
                        secondary = Directory.Exists(_config["Settings"]["SecondaryNetworkPath"]);
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex);
                }
            }
            SecondaryStatus.Fill = secondary ? Brushes.LightGreen : Brushes.IndianRed;
            SecondaryStatusPanel.Visibility = showSecondary ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LogError(Exception ex)
        {
            try
            {
                File.AppendAllText(LogFilePath, $"{DateTime.Now} - {ex}\r\n");
            }
            catch { }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (isUpdating)
            {
                e.Cancel = true;
                ShowMessage("Дождитесь завершения обновления или отмените его перед выходом.");
            }
            base.OnClosing(e);
        }

        private void ShowMessage(string text)
            => MainSnackbar.MessageQueue?.Enqueue(text);
    }
}

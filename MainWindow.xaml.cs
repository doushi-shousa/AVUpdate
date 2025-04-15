using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
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
                _config["Settings"]["SecondaryNetworkPath"] = @"\\backup\updates";
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

            string theme = _config["Settings"]["Theme"];
            ApplyTheme(theme);
            UpdateThemeIcon(theme);

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdatePathStatusUI));
        }

        private void UpdateThemeIcon(string theme)
        {
            ThemeIcon.Text = theme == "Dark" ? "☀️" : "🌙";
            isDarkTheme = theme == "Dark";
        }

        private void ApplyTheme(string theme)
        {
            var palette = new PaletteHelper();
            ITheme current = palette.GetTheme();

            switch (theme.ToLower())
            {
                case "light": current.SetBaseTheme(Theme.Light); break;
                case "dark": current.SetBaseTheme(Theme.Dark); break;
                default: current.SetBaseTheme(Theme.Dark); break;
            }

            palette.SetTheme(current);
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            string newTheme = isDarkTheme ? "Light" : "Dark";
            ApplyTheme(newTheme);
            UpdateThemeIcon(newTheme);
            _config["Settings"]["Theme"] = newTheme;
            new FileIniDataParser().WriteFile(ConfigFilePath, _config);
        }

        private void ShowMessage(string message)
        {
            MainSnackbar.MessageQueue?.Enqueue(message);
        }

        private void UpdateProgress(double percent)
        {
            ProgressBar.Value = percent;
            ProgressPercent.Text = $"{(int)percent}%";

            if (percent < 40)
                ProgressBar.Foreground = new SolidColorBrush(Colors.OrangeRed);
            else if (percent < 80)
                ProgressBar.Foreground = new SolidColorBrush(Colors.Goldenrod);
            else
                ProgressBar.Foreground = new SolidColorBrush(Colors.LightGreen);
        }

        private void UpdatePathStatusUI()
        {
            // Главный путь
            bool primaryExists = Directory.Exists(_config["Settings"]["NetworkPath"]);
            PrimaryStatus.Fill = primaryExists ? Brushes.LightGreen : Brushes.IndianRed;

            // Второй путь
            bool showSecondary = _config["Settings"]["UseSecondaryPath"] == "true";
            bool secondaryExists = Directory.Exists(_config["Settings"]["SecondaryNetworkPath"]);

            SecondaryStatus.Fill = secondaryExists ? Brushes.LightGreen : Brushes.IndianRed;
            SecondaryStatusPanel.Visibility = showSecondary ? Visibility.Visible : Visibility.Collapsed;
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
            {
                Owner = this
            };

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

                ApplyTheme(window.SelectedTheme);
                UpdateThemeIcon(window.SelectedTheme);
                UpdatePathStatusUI();

                ShowMessage("Настройки сохранены.");
            }
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            isUpdating = true;
            SetControlsEnabled(false);

            string archiveMask = _config["Settings"]["ArchiveName"];
            string archivePath = _config["Settings"]["UseCustomSource"] == "true"
                ? FindCustomSourcePath(_config["Settings"]["CustomSourcePath"], archiveMask)
                : FindCDROMPath(archiveMask);

            if (archivePath == null)
            {
                ShowMessage("Архив не найден.");
                StatusText.Text = "Архив не найден.";
                SetControlsEnabled(true);
                isUpdating = false;
                return;
            }

            string destPath = _config["Settings"]["NetworkPath"];
            ArchivePathText.Text = $"Архив: {Path.GetFileName(archivePath)}";
            TargetPathText.Text = $"Путь: {destPath}";

            StatusText.Text = "Очистка каталога...";
            await Task.Run(() => CleanDirectory(destPath));
            UpdateProgress(15);

            StatusText.Text = "Копирование...";
            string targetZip = Path.Combine(destPath, Path.GetFileName(archivePath));
            await Task.Run(() => File.Copy(archivePath, targetZip, true));
            UpdateProgress(50);

            StatusText.Text = "Распаковка...";
            await Task.Run(() => ZipFile.ExtractToDirectory(targetZip, destPath));
            UpdateProgress(90);

            File.Delete(targetZip);
            UpdateProgress(100);
            StatusText.Text = "Готово";
            ShowMessage("Обновление завершено.");
            SetControlsEnabled(true);
            isUpdating = false;
        }

        private void CleanDirectory(string path)
        {
            if (!Directory.Exists(path)) return;

            foreach (var f in Directory.GetFiles(path))
                File.Delete(f);
            foreach (var d in Directory.GetDirectories(path))
                Directory.Delete(d, true);
        }

        private string FindCDROMPath(string archiveMask)
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.CDRom && drive.IsReady)
                {
                    var files = Directory.GetFiles(drive.RootDirectory.FullName, archiveMask);
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

        private void SetControlsEnabled(bool enabled)
        {
            UpdateButton.IsEnabled = enabled;
            SettingsButton.IsEnabled = enabled;
            ThemeButton.IsEnabled = enabled;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (isUpdating)
            {
                e.Cancel = true;
                ShowMessage("Дождитесь завершения обновления перед выходом.");
            }
            base.OnClosing(e);
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using IniParser;
using IniParser.Model;

namespace AVUpdate
{
    public partial class MainWindow : Window
    {
        private readonly IniData _config;
        private const string ConfigFilePath = "config.ini";
        private const string LogFilePath = "update.log";
        private bool isDarkTheme = true;

        public MainWindow()
        {
            InitializeComponent();

            var parser = new FileIniDataParser();
            if (!File.Exists(ConfigFilePath))
            {
                _config = CreateDefaultConfig();
                parser.WriteFile(ConfigFilePath, _config);
                Log("Создан config.ini по умолчанию.");
            }
            else
            {
                _config = parser.ReadFile(ConfigFilePath);
                Log("Загружен config.ini.");
            }

            ApplyThemeFromConfig();
            LoadThemeIcon();
            DeleteOldLogs();
        }

        private IniData CreateDefaultConfig()
        {
            var config = new IniData();
            config.Sections.AddSection("Settings");
            config["Settings"]["NetworkPath"] = @"\\network\path\updates";
            config["Settings"]["ArchiveName"] = "kave*.zip";
            config["Settings"]["UseSecondaryPath"] = "false";
            config["Settings"]["SecondaryNetworkPath"] = @"\\secondary\path\updates";
            config["Settings"]["SecondaryUsername"] = "";
            config["Settings"]["SecondaryPassword"] = "";
            config["Settings"]["UseCustomSource"] = "false";
            config["Settings"]["CustomSourcePath"] = "";
            config["Settings"]["Theme"] = "Dark";
            return config;
        }

        private void ApplyThemeFromConfig()
        {
            string theme = _config["Settings"].ContainsKey("Theme") ? _config["Settings"]["Theme"] : "Dark";
            switch (theme)
            {
                case "Light":
                    ApplyLightTheme();
                    isDarkTheme = false;
                    break;
                case "Dark":
                    ApplyDarkTheme();
                    isDarkTheme = true;
                    break;
                case "System":
                    ApplyDarkTheme(); // можно расширить позже
                    isDarkTheme = true;
                    break;
            }
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (isDarkTheme)
            {
                ApplyLightTheme();
                isDarkTheme = false;
                _config["Settings"]["Theme"] = "Light";
            }
            else
            {
                ApplyDarkTheme();
                isDarkTheme = true;
                _config["Settings"]["Theme"] = "Dark";
            }
            LoadThemeIcon();
            new FileIniDataParser().WriteFile(ConfigFilePath, _config);
        }

        private void LoadThemeIcon()
        {
            ThemeIcon.Text = isDarkTheme ? "☀️" : "🌙";
        }

        private void ApplyLightTheme()
        {
            Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            StatusText.Foreground = new SolidColorBrush(Colors.Black);
            ProgressBar.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215));
        }

        private void ApplyDarkTheme()
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            StatusText.Foreground = new SolidColorBrush(Colors.White);
            ProgressBar.Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 255));
        }

        private void SetControlsEnabled(bool state)
        {
            UpdateButton.IsEnabled = state;
            SettingsButton.IsEnabled = state;
            ThemeButton.IsEnabled = state;
        }

        private void UpdateProgress(double value)
        {
            ProgressBar.Value = value;
            ProgressPercent.Text = $"{(int)value}%";
        }

        private void ShowMessage(string message)
        {
            MainSnackbar.MessageQueue?.Enqueue(message);
        }

        private void DeleteOldLogs()
        {
            try
            {
                if (File.Exists(LogFilePath))
                {
                    var lines = File.ReadAllLines(LogFilePath);
                    var fresh = Array.FindAll(lines, line =>
                    {
                        if (DateTime.TryParse(line.Substring(0, 19), out DateTime dt))
                            return dt >= DateTime.Now.AddDays(-30);
                        return true;
                    });
                    File.WriteAllLines(LogFilePath, fresh);
                }
            }
            catch
            {
                // необязательно логировать
            }
        }
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetControlsEnabled(false);
                StatusText.Text = "Запуск обновления...";
                UpdateProgress(0);
                Log("Запуск обновления...");

                if (!CheckNetworkPaths())
                {
                    ShowMessage("Сетевые пути недоступны.");
                    StatusText.Text = "Ошибка подключения.";
                    return;
                }

                string archiveName = _config["Settings"]["ArchiveName"];
                string dvdPath = _config["Settings"]["UseCustomSource"] == "true"
                                 ? FindCustomSourcePath(_config["Settings"]["CustomSourcePath"], archiveName)
                                 : FindCDROMPath(archiveName);

                if (dvdPath == null)
                {
                    ShowMessage("Архив не найден.");
                    StatusText.Text = "Источник не найден!";
                    return;
                }

                var result = MessageBox.Show("Очистить сетевую папку?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    ShowMessage("Очистка отменена.");
                    return;
                }

                await ProcessPath(_config["Settings"]["NetworkPath"], dvdPath, archiveName, 0);

                if (_config["Settings"]["UseSecondaryPath"] == "true")
                {
                    await ProcessPath(_config["Settings"]["SecondaryNetworkPath"], dvdPath, archiveName, 50);
                }

                StatusText.Text = "Обновление завершено!";
                ShowMessage("Успешно завершено.");
                Log("Обновление завершено.");
            }
            catch (Exception ex)
            {
                ShowMessage("Ошибка: " + ex.Message);
                StatusText.Text = "Ошибка при обновлении.";
                Log("Ошибка: " + ex.Message);
            }
            finally
            {
                SetControlsEnabled(true);
            }
        }

        private async Task ProcessPath(string path, string sourcePath, string archiveName, int progressOffset)
        {
            if (!Directory.Exists(path))
            {
                ShowMessage($"Путь {path} не найден.");
                Log($"Ошибка: Путь {path} не найден.");
                return;
            }

            StatusText.Text = $"Очистка {path}...";
            await Task.Run(() => CleanDirectory(path));
            UpdateProgress(progressOffset + 10);
            Log($"Очищена папка {path}");

            string archiveDest = Path.Combine(path, Path.GetFileName(sourcePath));
            StatusText.Text = $"Копирование архива...";
            await Task.Run(() => File.Copy(sourcePath, archiveDest, true));
            UpdateProgress(progressOffset + 40);
            Log($"Архив скопирован в {path}");

            StatusText.Text = $"Проверка архива...";
            if (!await Task.Run(() => CheckArchiveIntegrity(archiveDest)))
            {
                ShowMessage("Архив повреждён!");
                Log("Архив повреждён");
                return;
            }
            UpdateProgress(progressOffset + 60);
            Log("Архив проверен");

            StatusText.Text = $"Распаковка...";
            await Task.Run(() => ZipFile.ExtractToDirectory(archiveDest, path));
            UpdateProgress(progressOffset + 90);
            Log("Распаковано");

            File.Delete(archiveDest);
            UpdateProgress(progressOffset + 100);
            Log("Архив удалён");
        }

        private bool CheckNetworkPaths()
        {
            try
            {
                string p1 = _config["Settings"]["NetworkPath"];
                if (!Directory.Exists(p1)) return false;

                if (_config["Settings"]["UseSecondaryPath"] == "true")
                {
                    string p2 = _config["Settings"]["SecondaryNetworkPath"];
                    if (!Directory.Exists(p2)) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void CleanDirectory(string path)
        {
            foreach (var file in Directory.GetFiles(path))
                File.Delete(file);
            foreach (var dir in Directory.GetDirectories(path))
                Directory.Delete(dir, true);
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

        private string FindCustomSourcePath(string path, string mask)
        {
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, mask);
                if (files.Length > 0) return files[0];
            }
            return null;
        }

        private bool CheckArchiveIntegrity(string archivePath)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(archivePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        using (var stream = entry.Open()) { }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
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
                ShowMessage("Настройки сохранены.");
                Log("Настройки обновлены.");
            }
        }

        private void Log(string message)
        {
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
        }
    }
}

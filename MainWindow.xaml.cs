using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
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

            // Устанавливаем тему согласно настройкам из INI-файла
            string theme = _config["Settings"]["Theme"];
            UpdateAppTheme(theme);
            UpdateThemeIcon(theme);

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdatePathStatusUI));
        }

        /// <summary>
        /// Меняет глобальные ресурсы приложения для обновления темы.
        /// </summary>
        /// <param name="theme">Название темы: Light, Dark или System.</param>
        private void UpdateAppTheme(string theme)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            dictionaries.Clear();

            // Добавляем базовый словарь стилей Material Design
            dictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml")
            });

            // Выбираем тему по значению параметра
            if (theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
            {
                dictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml")
                });
            }
            else if (theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                dictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml")
                });
            }
            else if (theme.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                // Пример простой реализации: по умолчанию выбираем Dark
                dictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml")
                });
            }
            else
            {
                dictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml")
                });
            }

            // Добавляем словари цветов
            dictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Primary/MaterialDesignColor.DeepPurple.xaml")
            });
            dictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Accent/MaterialDesignColor.Lime.xaml")
            });
        }

        /// <summary>
        /// Обновляет значок кнопки переключения темы.
        /// </summary>
        /// <param name="theme">Название темы.</param>
        private void UpdateThemeIcon(string theme)
        {
            // Если тема Dark, значит значок показывает "☀️" для переключения на Light, иначе – "🌙"
            ThemeIcon.Text = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? "☀️" : "🌙";
            isDarkTheme = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            // Переключаем тему: если сейчас темная – выбираем светлую, иначе – темную.
            string newTheme = isDarkTheme ? "Light" : "Dark";
            UpdateAppTheme(newTheme);
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
            bool primaryExists = Directory.Exists(_config["Settings"]["NetworkPath"]);
            PrimaryStatus.Fill = primaryExists ? Brushes.LightGreen : Brushes.IndianRed;

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

            string archiveMask = _config["Settings"]["ArchiveName"];
            string archivePath = _config["Settings"]["UseCustomSource"] == "true"
                ? FindCustomSourcePath(_config["Settings"]["CustomSourcePath"], archiveMask)
                : FindCDROMPath(archiveMask);

            if (archivePath == null)
            {
                ShowMessage("Архив не найден.");
                StatusText.Text = "Архив не найден.";
                SetControlsEnabled(true);
                CancelButton.IsEnabled = false;
                isUpdating = false;
                return;
            }

            string destPath = _config["Settings"]["NetworkPath"];
            ArchivePathText.Text = $"Архив: {Path.GetFileName(archivePath)}";
            TargetPathText.Text = $"Путь: {destPath}";

            try
            {
                StatusText.Text = "Очистка каталога...";
                await Task.Run(() => CleanDirectory(destPath, updateCancellationTokenSource.Token), updateCancellationTokenSource.Token);
                UpdateProgress(15);

                StatusText.Text = "Копирование...";
                string targetZip = Path.Combine(destPath, Path.GetFileName(archivePath));
                await Task.Run(() => File.Copy(archivePath, targetZip, true), updateCancellationTokenSource.Token);
                UpdateProgress(50);

                StatusText.Text = "Распаковка...";
                await Task.Run(() => ZipFile.ExtractToDirectory(targetZip, destPath), updateCancellationTokenSource.Token);
                UpdateProgress(90);

                File.Delete(targetZip);
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
        {
            updateCancellationTokenSource?.Cancel();
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

        private void LogError(Exception ex)
        {
            try
            {
                File.AppendAllText(LogFilePath, DateTime.Now + " - " + ex.ToString() + Environment.NewLine);
            }
            catch { }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (isUpdating)
            {
                e.Cancel = true;
                ShowMessage("Дождитесь завершения обновления или отмените его перед выходом.");
            }
            base.OnClosing(e);
        }
    }
}

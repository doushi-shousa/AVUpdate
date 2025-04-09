using System;
using System.IO;
using System.IO.Compression; // Для ZipFile и ZipArchive
using System.Threading.Tasks;
using System.Windows;
using IniParser;
using IniParser.Model;

namespace AVUpdate
{
    public partial class MainWindow : Window
    {
        private readonly IniData _config;
        private const string ConfigFilePath = "config.ini";
        private const string LogFilePath = "update.log";

        private string NetworkPath => _config["Settings"]["NetworkPath"];
        private string ArchiveName => _config["Settings"]["ArchiveName"];

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                var parser = new FileIniDataParser();

                if (!File.Exists(ConfigFilePath))
                {
                    _config = new IniData();
                    _config.Sections.AddSection("Settings");
                    _config["Settings"]["NetworkPath"] = @"\\network\path\updates";
                    _config["Settings"]["ArchiveName"] = "updates.zip";
                    parser.WriteFile(ConfigFilePath, _config);
                    Log("Создан новый config.ini с настройками по умолчанию.");
                    StatusText.Text = "Создан новый config.ini с настройками по умолчанию.";
                }
                else
                {
                    _config = parser.ReadFile(ConfigFilePath);
                    Log("Конфигурация загружена из config.ini.");
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка при работе с config.ini: {ex.Message}");
                MessageBox.Show($"Ошибка при работе с config.ini: {ex.Message}");
                Close();
            }
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateButton.IsEnabled = false;
                SettingsButton.IsEnabled = false;
                StatusText.Text = "Запуск обновления...";
                ProgressBar.Value = 0;
                Log("Запуск обновления...");

                string networkPath = NetworkPath;
                string archiveName = ArchiveName;
                string dvdPath = FindCDROMPath(archiveName);

                if (dvdPath == null)
                {
                    StatusText.Text = "CD-ROM или архив не найдены!";
                    Log("Ошибка: CD-ROM или архив не найдены!");
                    return;
                }

                if (!Directory.Exists(networkPath))
                {
                    StatusText.Text = "Сетевая папка не найдена!";
                    Log("Ошибка: Сетевая папка не найдена!");
                    return;
                }

                var result = MessageBox.Show("Вы уверены, что хотите очистить сетевую папку?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    StatusText.Text = "Очистка отменена.";
                    Log("Очистка сетевой папки отменена пользователем.");
                    return;
                }

                StatusText.Text = "Очистка сетевой папки...";
                await Task.Run(() => CleanDirectory(networkPath));
                ProgressBar.Value = 25;
                Log("Сетевая папка очищена.");

                string destinationArchive = Path.Combine(networkPath, archiveName);
                StatusText.Text = "Копирование архива...";
                await Task.Run(() => File.Copy(dvdPath, destinationArchive, true));
                ProgressBar.Value = 50;
                Log("Архив скопирован.");

                StatusText.Text = "Проверка архива...";
                if (!await Task.Run(() => CheckArchiveIntegrity(destinationArchive)))
                {
                    StatusText.Text = "Архив повреждён!";
                    Log("Ошибка: Архив повреждён!");
                    return;
                }
                Log("Проверка архива прошла успешно.");

                StatusText.Text = "Распаковка архива...";
                await Task.Run(() => ZipFile.ExtractToDirectory(destinationArchive, networkPath));
                ProgressBar.Value = 75;
                Log("Архив распакован.");

                StatusText.Text = "Удаление архива...";
                await Task.Run(() => File.Delete(destinationArchive));
                ProgressBar.Value = 100;
                Log("Архив удалён.");

                StatusText.Text = "Обновление успешно завершено!";
                Log("Обновление успешно завершено!");
            }
            catch (UnauthorizedAccessException)
            {
                StatusText.Text = "Нет доступа к сетевой папке или CD-ROM.";
                Log("Ошибка: Нет доступа к сетевой папке или CD-ROM.");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка: {ex.Message}";
                Log($"Ошибка: {ex.Message}");
            }
            finally
            {
                UpdateButton.IsEnabled = true;
                SettingsButton.IsEnabled = true;
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(NetworkPath, ArchiveName);
            if (settingsWindow.ShowDialog() == true)
            {
                var parser = new FileIniDataParser();
                _config["Settings"]["NetworkPath"] = settingsWindow.NetworkPath;
                _config["Settings"]["ArchiveName"] = settingsWindow.ArchiveName;
                parser.WriteFile(ConfigFilePath, _config);
                Log("Настройки обновлены.");
                StatusText.Text = "Настройки сохранены.";
            }
        }

        private void CleanDirectory(string path)
        {
            foreach (string file in Directory.GetFiles(path))
            {
                File.Delete(file);
            }
            foreach (string dir in Directory.GetDirectories(path))
            {
                Directory.Delete(dir, true);
            }
        }

        private string FindCDROMPath(string archiveName)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.CDRom && drive.IsReady)
                {
                    string archivePath = Path.Combine(drive.RootDirectory.FullName, archiveName);
                    if (File.Exists(archivePath))
                    {
                        return archivePath;
                    }
                }
            }
            return null;
        }

        private bool CheckArchiveIntegrity(string archivePath)
        {
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(archivePath))
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

        private void Log(string message)
        {
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
        }
    }
}
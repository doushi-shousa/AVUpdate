using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace AVUpdate
{
    public partial class SettingsWindow : Window
    {
        public string NetworkPath { get; private set; }
        public string ArchiveName { get; private set; }
        public bool UseSecondaryPath { get; private set; }
        public string SecondaryNetworkPath { get; private set; }
        public string SecondaryUsername { get; private set; }
        public string SecondaryPassword { get; private set; }
        public bool UseCustomSource { get; private set; }
        public string CustomSourcePath { get; private set; }

        public string SelectedTheme
        {
            get
            {
                if (ThemeComboBox.SelectedItem is ComboBoxItem item)
                    return item.Content.ToString();
                return "Dark";
            }
        }

        public SettingsWindow(
            string networkPath,
            string archiveName,
            bool useSecondaryPath,
            string secondaryNetworkPath,
            string secondaryUsername,
            string secondaryPassword,
            bool useCustomSource,
            string customSourcePath,
            string currentTheme)
        {
            InitializeComponent();

            NetworkPathTextBox.Text = networkPath;
            ArchiveNameTextBox.Text = archiveName;
            UseSecondaryPathCheckBox.IsChecked = useSecondaryPath;
            SecondaryNetworkPathTextBox.Text = secondaryNetworkPath;
            SecondaryUsernameTextBox.Text = secondaryUsername;
            SecondaryPasswordBox.Password = secondaryPassword;
            UseCustomSourceCheckBox.IsChecked = useCustomSource;
            CustomSourcePathTextBox.Text = customSourcePath;

            foreach (ComboBoxItem item in ThemeComboBox.Items)
            {
                if (string.Equals(item.Content.ToString(), currentTheme, StringComparison.OrdinalIgnoreCase))
                {
                    ThemeComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NetworkPathTextBox.Text))
            {
                // Явно вызываем MessageBox из WPF, чтобы избежать System.Windows.Forms.MessageBox
                System.Windows.MessageBox.Show(
                    "Путь к сетевому ресурсу не может быть пустым.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            NetworkPath = NetworkPathTextBox.Text;
            ArchiveName = ArchiveNameTextBox.Text;
            UseSecondaryPath = UseSecondaryPathCheckBox.IsChecked ?? false;
            SecondaryNetworkPath = SecondaryNetworkPathTextBox.Text;
            SecondaryUsername = SecondaryUsernameTextBox.Text;
            SecondaryPassword = SecondaryPasswordBox.Password;
            UseCustomSource = UseCustomSourceCheckBox.IsChecked ?? false;
            CustomSourcePath = CustomSourcePathTextBox.Text;

            DialogResult = true;
            Close();
        }

        private void SelectNetworkPathButton_Click(object sender, RoutedEventArgs e)
            => TrySelectFolder(path => NetworkPathTextBox.Text = path);

        private void SelectSecondaryNetworkPathButton_Click(object sender, RoutedEventArgs e)
            => TrySelectFolder(path => SecondaryNetworkPathTextBox.Text = path);

        private void SelectCustomSourcePathButton_Click(object sender, RoutedEventArgs e)
            => TrySelectFolder(path => CustomSourcePathTextBox.Text = path);

        private void TrySelectFolder(Action<string> setPath)
        {
            try
            {
                var dialog = new FolderBrowserDialog();
                dialog.Description = "Выберите папку";
                dialog.ShowNewFolderButton = true;
                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    setPath(dialog.SelectedPath);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Ошибка при выборе папки: " + ex.Message,
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}

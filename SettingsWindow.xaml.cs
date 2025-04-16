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
                if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem)
                    return selectedItem.Content.ToString();
                return "Dark";
            }
        }

        public SettingsWindow(string networkPath, string archiveName, bool useSecondaryPath,
            string secondaryNetworkPath, string secondaryUsername, string secondaryPassword,
            bool useCustomSource, string customSourcePath, string currentTheme)
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
                if (item.Content.ToString().Equals(currentTheme, StringComparison.OrdinalIgnoreCase))
                {
                    ThemeComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Валидация: проверяем, что обязательное поле (NetworkPath) не пустое
            if (string.IsNullOrWhiteSpace(NetworkPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("Путь к сетевому ресурсу не может быть пустым.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
        {
            try
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        NetworkPathTextBox.Text = dialog.SelectedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Ошибка при выборе каталога: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectSecondaryNetworkPathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        SecondaryNetworkPathTextBox.Text = dialog.SelectedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Ошибка при выборе дополнительного каталога: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectCustomSourcePathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        CustomSourcePathTextBox.Text = dialog.SelectedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Ошибка при выборе пути обновления: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

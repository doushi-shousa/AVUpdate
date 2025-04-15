using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using MaterialDesignThemes.Wpf;

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
        public string SelectedTheme { get; private set; }

        public SettingsWindow(string networkPath, string archiveName, bool useSecondaryPath,
            string secondaryNetworkPath, string secondaryUsername, string secondaryPassword,
            bool useCustomSource, string customSourcePath, string selectedTheme)
        {
            InitializeComponent();

            // Применяем тему окна
            var paletteHelper = new PaletteHelper();
            ITheme theme = paletteHelper.GetTheme();

            if (selectedTheme == "Dark")
                theme.SetBaseTheme(Theme.Dark);
            else if (selectedTheme == "Light")
                theme.SetBaseTheme(Theme.Light);

            paletteHelper.SetTheme(theme);

            // Инициализация данных
            NetworkPath = networkPath;
            ArchiveName = archiveName;
            UseSecondaryPath = useSecondaryPath;
            SecondaryNetworkPath = secondaryNetworkPath;
            SecondaryUsername = secondaryUsername;
            SecondaryPassword = secondaryPassword;
            UseCustomSource = useCustomSource;
            CustomSourcePath = customSourcePath;
            SelectedTheme = selectedTheme;

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
                if (item.Content.ToString().Equals(selectedTheme, System.StringComparison.OrdinalIgnoreCase))
                {
                    ThemeComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            NetworkPath = NetworkPathTextBox.Text;
            ArchiveName = ArchiveNameTextBox.Text;
            UseSecondaryPath = UseSecondaryPathCheckBox.IsChecked ?? false;
            SecondaryNetworkPath = SecondaryNetworkPathTextBox.Text;
            SecondaryUsername = SecondaryUsernameTextBox.Text;
            SecondaryPassword = SecondaryPasswordBox.Password;
            UseCustomSource = UseCustomSourceCheckBox.IsChecked ?? false;
            CustomSourcePath = CustomSourcePathTextBox.Text;
            SelectedTheme = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            DialogResult = true;
            Close();
        }

        private void SelectNetworkPathButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    NetworkPathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void SelectSecondaryNetworkPathButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    SecondaryNetworkPathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void SelectCustomSourcePathButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    CustomSourcePathTextBox.Text = dialog.SelectedPath;
            }
        }
    }
}

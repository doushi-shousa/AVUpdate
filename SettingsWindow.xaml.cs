using System.Windows;

namespace AVUpdate
{
    public partial class SettingsWindow : Window
    {
        public string NetworkPath { get; private set; }
        public string ArchiveName { get; private set; }

        public SettingsWindow(string networkPath, string archiveName)
        {
            InitializeComponent();
            NetworkPath = networkPath;
            ArchiveName = archiveName;
            NetworkPathTextBox.Text = networkPath;
            ArchiveNameTextBox.Text = archiveName;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            NetworkPath = NetworkPathTextBox.Text;
            ArchiveName = ArchiveNameTextBox.Text;
            DialogResult = true;
            Close();
        }
    }
}
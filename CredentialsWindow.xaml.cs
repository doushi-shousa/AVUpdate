using System.Windows;

namespace AVUpdate
{
    public partial class CredentialsWindow : Window
    {
        public string Username { get; private set; }
        public string Password { get; private set; }

        public CredentialsWindow(string username, string password)
        {
            InitializeComponent();

            Username = username;
            Password = password;

            UsernameTextBox.Text = username;
            PasswordBox.Password = password;
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            Username = UsernameTextBox.Text;
            Password = PasswordBox.Password;
            DialogResult = true;
            Close();
        }
    }
}

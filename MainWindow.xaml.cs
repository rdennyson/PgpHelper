using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace PgpHelper
{
    public partial class MainWindow : Window
    {
        private string _outputFilePath = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            // Open file dialog to select a file
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (EncryptRadioButton.IsChecked == true)
                openFileDialog.Filter = "All Files (*.*)|*.*";
            else
                openFileDialog.Filter = "PGP Encrypted File (*.pgp)|*.pgp";

            if (openFileDialog.ShowDialog() == true)
            {
                FilePathTextBox.Text = openFileDialog.FileName;
            }
        }

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            var filePath = FilePathTextBox.Text.Replace("\\", "/");
            if (string.IsNullOrEmpty(filePath))
            {
                MessageBox.Show("Please select a file.");
                return;
            }

            string outputFilePath = string.Empty;

            try
            {
                if (EncryptRadioButton.IsChecked == true)
                {
                    outputFilePath = filePath + ".pgp";
                    var publicKey = PublicKeyTextBox.Text;
                    PgpUtil.EncryptFile(filePath, outputFilePath, publicKey);
                    MessageBox.Show($"RSA encryption completed successfully. File saved at: {_outputFilePath}");

                }
                else if (DecryptRadioButton.IsChecked == true)
                {
                    outputFilePath = filePath.Replace(".pgp", "");
                    var privateKey = PrivateKeyTextBox.Text;
                    var passphrase = PassphraseTextBox.Text;
                    PgpUtil.DecryptFile(filePath, outputFilePath, privateKey, passphrase);
                    MessageBox.Show($"RSA decryption completed successfully. File saved at: {_outputFilePath}");

                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        // Event handler to open the file location in File Explorer
        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_outputFilePath))
            {
                string folderPath = Path.GetDirectoryName(_outputFilePath);
                if (Directory.Exists(folderPath))
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = folderPath,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
            }
        }

        // Enable or disable Encrypt button based on public key input
        private void PublicKeyTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            EncryptRadioButton.IsEnabled = !string.IsNullOrWhiteSpace(PublicKeyTextBox.Text);
        }

        // Enable or disable Decrypt button based on private key and passphrase input
        private void PrivateKeyTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            CheckDecryptButtonEnable();
        }

        private void PassphraseTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            CheckDecryptButtonEnable();
        }

        // Method to check if the Decrypt button should be enabled
        private void CheckDecryptButtonEnable()
        {
            DecryptRadioButton.IsEnabled = !string.IsNullOrWhiteSpace(PrivateKeyTextBox.Text) &&
                                           !string.IsNullOrWhiteSpace(PassphraseTextBox.Text);
        }
    }
}


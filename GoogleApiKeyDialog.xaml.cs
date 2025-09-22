using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace TTS1.WPF.Dialogs
{
    public partial class GoogleApiKeyDialog : Window
    {
        public string ApiKey { get; private set; }
        
        public GoogleApiKeyDialog(string currentApiKey = null)
        {
            InitializeComponent();
            
            // Set current API key if provided
            if (!string.IsNullOrEmpty(currentApiKey))
            {
                txtApiKey.Password = currentApiKey;
                txtApiKeyVisible.Text = currentApiKey;
            }
        }
        
        private void chkShowKey_Checked(object sender, RoutedEventArgs e)
        {
            // Show the visible textbox and hide the password box
            txtApiKeyVisible.Text = txtApiKey.Password;
            txtApiKeyVisible.Visibility = Visibility.Visible;
            txtApiKey.Visibility = Visibility.Collapsed;
        }
        
        private void chkShowKey_Unchecked(object sender, RoutedEventArgs e)
        {
            // Hide the visible textbox and show the password box
            txtApiKey.Password = txtApiKeyVisible.Text;
            txtApiKey.Visibility = Visibility.Visible;
            txtApiKeyVisible.Visibility = Visibility.Collapsed;
        }
        
        private async void btnTest_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = chkShowKey.IsChecked == true ? txtApiKeyVisible.Text : txtApiKey.Password;
            
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show("Please enter an API key first.", "No API Key", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            btnTest.IsEnabled = false;
            btnTest.Content = "Testing...";
            
            try
            {
                bool isValid = await TestApiKey(apiKey);
                
                if (isValid)
                {
                    MessageBox.Show("API key is valid! Google TTS is ready to use.", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "API key test failed. Please check:\n\n" +
                        "1. The API key is correct\n" +
                        "2. Text-to-Speech API is enabled in Google Cloud Console\n" +
                        "3. Billing is enabled for your project\n" +
                        "4. You have internet connection",
                        "Test Failed", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error testing API key:\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnTest.IsEnabled = true;
                btnTest.Content = "Test Connection";
            }
        }
        
        private async Task<bool> TestApiKey(string apiKey)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    
                    string url = $"https://texttospeech.googleapis.com/v1/voices?key={apiKey}";
                    var response = await httpClient.GetAsync(url);
                    
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }
        
        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            ApiKey = chkShowKey.IsChecked == true ? txtApiKeyVisible.Text : txtApiKey.Password;
            
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                var result = MessageBox.Show(
                    "No API key entered. Do you want to continue without Google TTS?", 
                    "No API Key", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }
            
            DialogResult = true;
            Close();
        }
        
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}
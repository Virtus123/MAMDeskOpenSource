using System.Windows;
using MAMDesk.Shared.Models;
using MAMDesk.Shared.Services;

namespace MAMDesk.Operator.Views;

public partial class LoginWindow : Window
{
    private readonly ApiClient _api;

    public LoginWindow()
    {
        InitializeComponent();
        _api = new ApiClient(new AppSettings
        {
            ServerUrl = App.Settings.ServerUrl,
            WsUrl = App.Settings.WsUrl,
        });
    }

    private async void LoginBtn_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        LoginBtn.IsEnabled = false;

        try
        {
            var response = await _api.LoginAsync(EmailBox.Text.Trim(), PasswordBox.Password);
            App.Settings.Token = response.AccessToken;
            App.Settings.UserName = response.Usuario.Nome;

            var profile = UserProfileStore.Load();
            if (string.IsNullOrWhiteSpace(profile.DisplayName))
            {
                profile.DisplayName = response.Usuario.Nome;
                UserProfileStore.Save(profile);
            }

            var main = new MainWindow(_api);
            main.Show();
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message.Contains("401") ? "E-mail ou senha incorretos." : ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoginBtn.IsEnabled = true;
        }
    }
}

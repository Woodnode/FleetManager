using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

public partial class LoginPage : PageBase
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnEmailCompleted(object? sender, EventArgs e) => PasswordEntry.Focus();

    // Champ actif : filet brand-400, comme le :focus de .fm-input-dark.
    private void OnFieldFocused(object? sender, FocusEventArgs e) => FieldOf(sender)?.SetValue(Border.StrokeProperty, Color.FromArgb("#748FFC"));

    private void OnFieldUnfocused(object? sender, FocusEventArgs e) => FieldOf(sender)?.SetValue(Border.StrokeProperty, Color.FromArgb("#1C2233"));

    private Border? FieldOf(object? sender) => sender == EmailEntry ? EmailField : sender == PasswordEntry ? PasswordField : null;

}

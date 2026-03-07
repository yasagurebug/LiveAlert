using System.Windows;

namespace LiveAlert.Windows.Views;

public partial class TextViewerWindow : Window
{
    public TextViewerWindow(string title, string content)
    {
        InitializeComponent();
        Title = title;
        ContentTextBox.Text = content;
        ContentTextBox.CaretIndex = 0;
        ContentTextBox.ScrollToHome();
    }

    private void HandleCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

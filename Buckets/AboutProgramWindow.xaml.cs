using System.Windows;
namespace Buckets
{
    public partial class AboutProgramWindow : Window
    {
        public AboutProgramWindow() { InitializeComponent(); }
        private void Close_Click(object sender, RoutedEventArgs e) { Close(); }
    }
}

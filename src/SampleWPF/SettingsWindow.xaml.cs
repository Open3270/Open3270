using System.Windows;

namespace TerminalDemo
{
	/// <summary>
	/// Interaction logic for SettingsWindow.xaml
	/// </summary>
	public partial class SettingsWindow : Window
	{
		public SettingsWindow()
		{
			InitializeComponent();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			(this.mainGrid.DataContext as TerminalSettings).SaveToSettings(Properties.Settings.Default);
			this.Close();
		}
	}
}
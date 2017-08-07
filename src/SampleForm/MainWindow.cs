using System;
using System.Windows.Forms;

namespace SampleForm
{
	public partial class MainWindow : Form
	{
		private SettingsWindow sw = new SettingsWindow();

		public MainWindow()
		{
			InitializeComponent();
		}

		private void btnConnect_Click(object sender, EventArgs e)
		{
			OpenEmulator.Connect(sw.Host, sw.Port, sw.TerminalType, sw.UseSsl);
		}

		private void btnRefresh_Click(object sender, EventArgs e)
		{
			OpenEmulator.Redraw();
		}

		private void btnSettings_Click(object sender, EventArgs e)
		{
			sw.Show();
		}
	}
}
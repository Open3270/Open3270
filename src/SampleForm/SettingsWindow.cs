using System;
using System.Windows.Forms;

namespace SampleForm
{
	public partial class SettingsWindow : Form
	{
		public string Host { get { return this.txtHost.Text; } }
		public int Port { get { return int.Parse(this.txtHostPort.Text); } }
		public string TerminalType { get { return this.txtTerminalType.Text; } }
		public bool UseSsl { get { return this.cbUseSSL.Checked; } }

		public SettingsWindow()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			this.Hide();
		}
	}
}
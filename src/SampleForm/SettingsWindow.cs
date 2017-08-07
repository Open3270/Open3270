using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

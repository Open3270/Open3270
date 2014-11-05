using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Open3270;

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

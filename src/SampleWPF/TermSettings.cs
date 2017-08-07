using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using TerminalDemo.Properties;

namespace TerminalDemo
{
	public class TerminalSettings : INotifyPropertyChanged
	{



		public TerminalSettings()
		{
			this.LoadFromSettings(Settings.Default);
		}


		internal void LoadFromSettings(Settings settings)
		{
			this.Host = Properties.Settings.Default.Hostname;
			this.HostPort = Properties.Settings.Default.HostPort;
			this.TerminalType = Properties.Settings.Default.TerminalType;
			this.UseSSL = Properties.Settings.Default.UseSSL;
		}

		internal void SaveToSettings(Settings settings)
		{
			Settings.Default.Hostname = this.Host;
			Settings.Default.HostPort = this.HostPort;
			Settings.Default.TerminalType = this.TerminalType;
			Settings.Default.UseSSL = this.UseSSL;

			Settings.Default.Save();
		}




		string host;

		public string Host
		{
			get
			{
				return this.host;
			}
			set
			{
				this.host = value;
				this.OnPropertyChanged("Host");
			}
		}




		int hostPort;

		public int HostPort
		{
			get
			{
				return this.hostPort;
			}
			set
			{
				this.hostPort = value;
				this.OnPropertyChanged("HostPort");
			}
		}



		bool useSSL;

		public bool UseSSL
		{
			get
			{
				return this.useSSL;
			}
			set
			{
				this.useSSL = value;
				this.OnPropertyChanged("UseSSL");
			}
		}




		string terminalType;

		public string TerminalType
		{
			get
			{
				return this.terminalType;
			}
			set
			{
				this.terminalType = value;
				this.OnPropertyChanged("TerminalType");
			}
		}

		#region INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged(string propertyName)
		{
			if (this.PropertyChanged != null)
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		#endregion INotifyPropertyChanged


	}
}

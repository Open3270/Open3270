using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TerminalDemo
{

	public partial class MainWindow : Window
	{

		public Terminal Terminal
		{
			get { return (this.Resources["term"] as Terminal); }
		}

		public MainWindow()
		{
			InitializeComponent();
		}


		//This command isn't used in the demo, but can be used when you want to send some predefined text.
		#region SendText Command

		public static RoutedUICommand SendText = new RoutedUICommand();

		void CanExecuteSendText(object sender, CanExecuteRoutedEventArgs args)
		{
			if (true)
			{
				args.CanExecute = true;
			}
			else
			{
				//args.CanExecute = false;
			}
		}


		void ExecuteSendText(object sender, ExecutedRoutedEventArgs args)
		{
			
		}

		#endregion SendText Command


		#region SendCommand Command

		public static RoutedUICommand SendCommand = new RoutedUICommand();

		void CanExecuteSendCommand(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = this.Terminal.IsConnected;
		}


		void ExecuteSendCommand(object sender, ExecutedRoutedEventArgs args)
		{
			this.Terminal.SendKey((string)args.Parameter);
		}

		#endregion SendCommand Command


		#region Connect Command

		public static RoutedUICommand Connect = new RoutedUICommand();

		void CanExecuteConnect(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = !this.Terminal.IsConnected && !this.Terminal.IsConnecting;
		}


		void ExecuteConnect(object sender, ExecutedRoutedEventArgs args)
		{
			this.Terminal.Connect();
		}

		#endregion Connect Command


		#region Refresh Command

		public static RoutedUICommand Refresh = new RoutedUICommand();

		void CanExecuteRefresh(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = this.Terminal.IsConnected;
		}


		void ExecuteRefresh(object sender, ExecutedRoutedEventArgs args)
		{
			this.Terminal.Refresh();
		}

		#endregion Refresh Command


		#region DumpFields Command

		public static RoutedUICommand DumpFields = new RoutedUICommand();

		void CanExecuteDumpFields(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = this.Terminal.IsConnected;
		}


		void ExecuteDumpFields(object sender, ExecutedRoutedEventArgs args)
		{
			this.Terminal.DumpFillableFields();
		}

		#endregion DumpFields Command



		private void Window_TextInput(object sender, TextCompositionEventArgs e)
		{
			if (this.Terminal.IsConnected)
			{
				this.Terminal.SendText(e.Text);
			}

		}
	
		
	}
}

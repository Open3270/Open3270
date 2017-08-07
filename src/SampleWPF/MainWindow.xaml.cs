using Open3270;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

			//This odd event handler is needed because the TextBox control eats that spacebar, so we have to intercept an already-handled event.
			this.Console.AddHandler(TextBox.KeyDownEvent, new KeyEventHandler(Console_KeyDown), true);
		}

		//This command isn't used in the demo, but can be used when you want to send some predefined text.

		#region SendText Command

		public static RoutedUICommand SendText = new RoutedUICommand();

		private void CanExecuteSendText(object sender, CanExecuteRoutedEventArgs args)
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

		private void ExecuteSendText(object sender, ExecutedRoutedEventArgs args)
		{
		}

		#endregion SendText Command

		#region SendCommand Command

		public static RoutedUICommand SendCommand = new RoutedUICommand();

		private void CanExecuteSendCommand(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = this.Terminal.IsConnected;
		}

		private void ExecuteSendCommand(object sender, ExecutedRoutedEventArgs args)
		{
			this.Terminal.SendKey((TnKey)args.Parameter);
		}

		#endregion SendCommand Command

		#region Connect Command

		public static RoutedUICommand Connect = new RoutedUICommand();

		private void CanExecuteConnect(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = !this.Terminal.IsConnected && !this.Terminal.IsConnecting;
		}

		private void ExecuteConnect(object sender, ExecutedRoutedEventArgs args)
		{
			this.Terminal.Connect();

			//The caret won't show up in the textbox until it receives focus.
			this.Console.Focus();
		}

		#endregion Connect Command

		#region Refresh Command

		public static RoutedUICommand Refresh = new RoutedUICommand();

		private void CanExecuteRefresh(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = this.Terminal.IsConnected;
		}

		private void ExecuteRefresh(object sender, ExecutedRoutedEventArgs args)
		{
			this.Terminal.Refresh();
		}

		#endregion Refresh Command

		#region DumpFields Command

		public static RoutedUICommand DumpFields = new RoutedUICommand();

		private void CanExecuteDumpFields(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = this.Terminal.IsConnected;
		}

		private void ExecuteDumpFields(object sender, ExecutedRoutedEventArgs args)
		{
			this.Terminal.DumpFillableFields();
		}

		#endregion DumpFields Command

		#region OpenSettings Command

		public static RoutedUICommand OpenSettings = new RoutedUICommand();

		private void CanExecuteOpenSettings(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = true;
		}

		private void ExecuteOpenSettings(object sender, ExecutedRoutedEventArgs args)
		{
			SettingsWindow settingsWindow = new SettingsWindow();
			settingsWindow.ShowDialog();
		}

		#endregion OpenSettings Command

		private void Window_TextInput(object sender, TextCompositionEventArgs e)
		{
			if (this.Terminal.IsConnected)
			{
				this.Terminal.SendText(e.Text);
			}
		}

		private void Console_KeyDown(object sender, KeyEventArgs e)
		{
			//The textbox eats several keystrokes, so we can't handle them from keybindings/commands.
			if (this.Terminal.IsConnected)
			{
				switch (e.Key)
				{
					case Key.Space:
						{
							this.Terminal.SendText(" ");
							break;
						}
					case Key.Left:
						{
							this.Terminal.SendKey(TnKey.Left);
							break;
						}
					case Key.Right:
						{
							this.Terminal.SendKey(TnKey.Right);
							break;
						}
					case Key.Up:
						{
							this.Terminal.SendKey(TnKey.Up);
							break;
						}
					case Key.Down:
						{
							this.Terminal.SendKey(TnKey.Down);
							break;
						}
					case Key.Back:
						{
							this.Terminal.SendKey(TnKey.Backspace);
							break;
						}
					case Key.Delete:
						{
							this.Terminal.SendKey(TnKey.Delete);
							break;
						}
					default:
						break;
				}
			}
		}
	}
}
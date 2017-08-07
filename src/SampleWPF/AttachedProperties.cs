using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TerminalDemo
{
	class AttachedProperties
	{

		public static int GetCaretLocation(DependencyObject obj)
		{
			return (int)obj.GetValue(CaretLocationProperty);
		}

		public static void SetCaretLocation(DependencyObject obj, int value)
		{
			obj.SetValue(CaretLocationProperty, value);
		}

		public static readonly DependencyProperty CaretLocationProperty =
			DependencyProperty.RegisterAttached("CaretLocation", typeof(int), typeof(AttachedProperties), new PropertyMetadata(new PropertyChangedCallback(CaretChanged)));

		private static void CaretChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			//We dispatch here in order to let the textbox finish its processing before we try to change the cursor position
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
			{
				TextBox tb = d as TextBox;
				if (tb != null)
				{
					tb.CaretIndex = (int)e.NewValue;
				}
			}));

		}
	}
}

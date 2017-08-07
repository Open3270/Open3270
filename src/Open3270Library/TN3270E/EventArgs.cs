﻿using System;

namespace Open3270.TN3270
{
	public class Connected3270EventArgs : EventArgs
	{
		private bool is3270;

		public bool Is3270
		{
			get
			{
				return this.is3270;
			}
		}

		public Connected3270EventArgs(bool is3270)
		{
			this.is3270 = is3270;
		}
	}

	public class PrimaryConnectionChangedArgs : EventArgs
	{
		private bool success;

		public bool Success
		{
			get
			{
				return this.success;
			}
		}

		public PrimaryConnectionChangedArgs(bool success)
		{
			this.success = success;
		}
	}
}

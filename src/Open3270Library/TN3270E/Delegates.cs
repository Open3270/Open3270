using System;
using System.Collections.Generic;
using System.Text;

namespace Open3270.TN3270
{
	internal delegate void TelnetDataDelegate(object parentData, TNEvent eventType, string text);

	internal delegate void SChangeDelegate(bool option);

	public delegate void RunScriptDelegate(string where);
}

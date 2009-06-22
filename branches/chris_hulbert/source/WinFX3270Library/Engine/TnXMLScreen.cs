#region License
/* 
 *
 * Open3270 - A C# implementation of the TN3270/TN3270E protocol
 *
 *   Copyright © 2004-2006 Michael Warriner. All rights reserved
 * 
 * This is free software; you can redistribute it and/or modify it
 * under the terms of the GNU Lesser General Public License as
 * published by the Free Software Foundation; either version 2.1 of
 * the License, or (at your option) any later version.
 *
 * This software is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this software; if not, write to the Free
 * Software Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA
 * 02110-1301 USA, or see the FSF site: http://www.fsf.org.
 */
#endregion
using System;
using System.Data;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Collections;
using System.Reflection;
using System.Security;
using System.Xml;
using System.Xml.Serialization;
using System.Security.Cryptography;
using Open3270;
using Open3270.Internal;

namespace Open3270.TN3270
{
	/// <summary>
	/// Do not use this class, use IXMLScreen instead...!
	/// </summary>
	[Serializable]
	public class XMLScreen : IXMLScreen
	{
		[XmlIgnore()] private Guid _ScreenGuid;
		//
		[XmlIgnore()] public Guid ScreenGuid { get { return _ScreenGuid; }}
		//
		[System.Xml.Serialization.XmlElementAttribute("Field")]
		public XMLScreenField[] Field;

		[System.Xml.Serialization.XmlElementAttribute("Unformatted")]
		public XMLUnformattedScreen Unformatted;

		public bool Formatted;

		private char[]   mScreenBuffer = null;
		private string[] mScreenRows = null;
		private int _CX;
		private int _CY;

		private string _stringValueCache = null;


		public int CX { get { return _CX; }}
		public int CY { get { return _CY; }}
		public string UserIdentified;
		[XmlIgnore] public string MatchListIdentified;
		public string Name { get { return MatchListIdentified; }}
		[XmlIgnore] public string FileName;
		public Guid UniqueID;

		[XmlIgnore] public string Hash;

		static public XMLScreen load(Stream sr)
		{

			XmlSerializer serializer = new XmlSerializer(typeof(XMLScreen));
			//
			XMLScreen rules = null;
			
			object temp = serializer.Deserialize(sr);
			rules = (XMLScreen)temp;
			
			if (rules != null)
			{
				rules.FileName = null;

				rules.Render();
			}
			return rules;
		}

		static public XMLScreen load(string filename)
		{
			XmlSerializer serializer = new XmlSerializer(typeof(XMLScreen));
			//
			FileStream fs = null;
			XMLScreen rules = null;
			
			try
			{
				fs = new FileStream(filename, FileMode.Open);
				//XmlTextReader reader = new XmlTextReader(fs);

				rules = (XMLScreen) serializer.Deserialize(fs);
				rules.FileName = filename;
			}
			finally
			{
				if (fs != null)
					fs.Close();
			}
			rules.Render();
			return rules;
		}

		public void Render()
		{
			//
			// Reset cache
			//
			_stringValueCache = null;
			//
			if (_CX==0 || _CY==0)
			{
                // TODO: Need to fix this
				_CX = 80; 
				_CY = 25; 
			}
			UserIdentified = null;
			MatchListIdentified = null;
			//
			// Render text image of screen
			//
			//
			mScreenBuffer = new char[_CX*_CY];
			mScreenRows   = new string[_CY];
			int i;
			for (i=0; i<_CX*_CY; i++)
			{
				mScreenBuffer[i] = ' ';
			}
			//
			
			int chindex;
			
			if (Field==null || Field.Length==0 && 
				(Unformatted==null || Unformatted.Text==null))
			{
//				Console.WriteLine("**BUGBUG** Unformatted screen is blank");
				for (i=0; i<mScreenRows.Length; i++)
				{
					mScreenRows[i] = "                                                                                              ".Substring(0,_CX);
				}
			}
			else
			{
				for (i=0; i<Unformatted.Text.Length; i++)
				{
					string text = Unformatted.Text[i];
					while (text.Length < _CX)
						text+=" ";
					//
					int p;
					for (p=0; p<_CX; p++)
					{
						if (text[p]<32 || text[p]>126)
							text = text.Replace(text[p], ' ');
					}
					//
					for (chindex=0; chindex<Unformatted.Text[i].Length; chindex++)
					{
						if ((chindex+i*_CX)<mScreenBuffer.Length)
						{
							mScreenBuffer[chindex+i*_CX] = text[chindex];
						}
					}
					mScreenRows[i] = text;
				}
				//
				// Now superimpose the formatted fields on the unformatted base
				//
				for (i=0; i<Field.Length; i++)
				{
					XMLScreenField field = Field[i];
					if (field.Text != null)
					{
						for (chindex=0; chindex<field.Text.Length; chindex++)
						{
							char ch = field.Text[chindex];
							if (ch<32 || ch>126)
								ch = ' ';
							mScreenBuffer[chindex+field.Location.left+field.Location.top*_CX] = ch;
						}
					}
				}
				for (i=0; i<_CY; i++)
				{
					string temp = "";
					for (int j = 0; j<_CX; j++)
					{
						temp+=mScreenBuffer[i*_CX+j];
					}
					mScreenRows[i] = temp;
				}
			}
			// now calculate our screen's hash
			//
			HashAlgorithm hash = (HashAlgorithm)CryptoConfig.CreateFromName("MD5");
			StringBuilder builder = new StringBuilder();
			for (i=0; i<mScreenRows.Length; i++)
			{
				builder.Append(mScreenRows[i]);
			}
			byte[] myHash = hash.ComputeHash(new UnicodeEncoding().GetBytes(builder.ToString()));
			this.Hash = BitConverter.ToString(myHash);
			this._ScreenGuid = Guid.NewGuid();
		}

		static public XMLScreen LoadFromString(string text)
		{
			XmlSerializer serializer = new XmlSerializer(typeof(XMLScreen));
			//
			StringReader fs = null;
			XMLScreen rules = null;
			
			try
			{
				fs = new StringReader(text);
				//XmlTextReader reader = new XmlTextReader(fs);

				rules = (XMLScreen) serializer.Deserialize(fs);//reader);
				rules.FileName = null;
			}
			catch (Exception e)
			{
				Console.WriteLine("Exception "+e.Message+" reading document. saved as c:\\dump.xml");
				StreamWriter sw = File.CreateText("c:\\dump.xml");
				sw.WriteLine(text);
				sw.Close();
				throw;
			}
			finally
			{
				if (fs != null)
					fs.Close();
			}
			rules.Render();
			rules._stringValueCache = text;
			return rules;
		}
		public void save(string filename)
		{
			XmlSerializer serializer = new XmlSerializer(typeof(XMLScreen));
			//
			// now expand back to xml
			StreamWriter fsw = new StreamWriter(filename, false, System.Text.Encoding.Unicode);
			serializer.Serialize(fsw, this);
			fsw.Close();

		}
		// helper functions
		public string GetText(int x, int y, int length)
		{
			return GetText(x+y*_CX, length);
		}

		
		public string GetText(int offset, int length)
		{
			int i;
			string result = "";
			int maxlen = this.mScreenBuffer.Length;
			for (i=0; i<length; i++)
			{
				if (i+offset < maxlen)
					result+= this.mScreenBuffer[i+offset];
			}
			return result;
		}
		public char GetCharAt(int offset)
		{
			return this.mScreenBuffer[offset];
		}
		public string GetRow(int row)
		{
			return mScreenRows[row];
		}
		public string Dump()
		{
			StringAudit audit = new StringAudit();
			Dump(audit);
			return audit.ToString();
		}
		public void Dump(IAudit stream)
		{
			int i;
			stream.WriteLine("-----");
			stream.WriteLine("   0         1         2         3         4         5         6         7         ");
			stream.WriteLine("   01234567890123456789012345678901234567890123456789012345678901234567890123456789");
			for (i=0; i<_CY; i++)
			{
				string line = GetText(0,i, _CX);
				string lr = ""+i+"       ";
				stream.WriteLine(lr.Substring(0,2)+" "+line);
			}
			stream.WriteLine("-----");
		}


		public string GetXMLText()
		{
			return GetXMLText(true);
		}
		
		public string GetXMLText(bool useCache)
		{
			if (useCache==false || _stringValueCache == null)
			{
				//
				XmlSerializer serializer = new XmlSerializer(typeof(XMLScreen));
				//
				StringWriter fs = null;
			
				try
				{
					StringBuilder builder = new StringBuilder();
					fs = new StringWriter(builder);
					serializer.Serialize(fs, this);

					fs.Close();

					_stringValueCache = builder.ToString();
				}
				finally
				{
					if (fs != null)
						fs.Close();
				}
			}
			return _stringValueCache;
		}


	}

	[Serializable]
	public class XMLUnformattedScreen
	{
		[System.Xml.Serialization.XmlElementAttribute("Text")] public string[] Text;
	}
		
	[Serializable]
	public  class XMLScreenField 
	{
		[System.Xml.Serialization.XmlElementAttribute("Location")]
		public XMLScreenLocation Location;

		[System.Xml.Serialization.XmlElementAttribute("Attributes")]
		public XMLScreenAttributes Attributes;
		
		[System.Xml.Serialization.XmlText] public string Text;
	}
	[Serializable]
	public  class XMLScreenLocation
	{
		[System.Xml.Serialization.XmlAttributeAttribute(Form=System.Xml.Schema.XmlSchemaForm.Unqualified)] public int position;
		[System.Xml.Serialization.XmlAttributeAttribute(Form=System.Xml.Schema.XmlSchemaForm.Unqualified)] public int left;
		[System.Xml.Serialization.XmlAttributeAttribute(Form=System.Xml.Schema.XmlSchemaForm.Unqualified)] public int top;
		[System.Xml.Serialization.XmlAttributeAttribute(Form=System.Xml.Schema.XmlSchemaForm.Unqualified)] public int length;
	}
	[Serializable]
	public  class XMLScreenAttributes
	{
		[System.Xml.Serialization.XmlAttributeAttribute(Form=System.Xml.Schema.XmlSchemaForm.Unqualified)] public int Base;
		[System.Xml.Serialization.XmlAttributeAttribute(Form=System.Xml.Schema.XmlSchemaForm.Unqualified)] public bool Protected;
		[System.Xml.Serialization.XmlAttributeAttribute(Form=System.Xml.Schema.XmlSchemaForm.Unqualified)] public string FieldType;
		[System.Xml.Serialization.XmlAttributeAttribute(Form=System.Xml.Schema.XmlSchemaForm.Unqualified)] public string Foreground;
		[System.Xml.Serialization.XmlAttributeAttribute(Form=System.Xml.Schema.XmlSchemaForm.Unqualified)] public string Background;
	}

	//
	// 
}

#region License
/* 
 *
 * Open3270 - A C# implementation of the TN3270/TN3270E protocol
 *
 * Copyright (c) 2004-2020 Michael Warriner
 * Modifications (c) as per Git change history
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 * and associated documentation files (the "Software"), to deal in the Software without restriction,
 * including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
 * subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or substantial 
 * portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
 * LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
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
	public class XMLScreen : IXMLScreen, IDisposable
	{
		[XmlIgnore()] private Guid _ScreenGuid;
		//
		[XmlIgnore()] public Guid ScreenGuid { get { return _ScreenGuid; }}
		//
		[System.Xml.Serialization.XmlElementAttribute("Field")]
		public XMLScreenField[] Field;

		public XMLScreenField[] Fields
		{
			get { return Field; }
		}

		[System.Xml.Serialization.XmlElementAttribute("Unformatted")]
		public XMLUnformattedScreen Unformatted;

		public bool Formatted;

		private char[]   mScreenBuffer = null;
		private string[] mScreenRows = null;

        // CFC,Jr. 2008/07/11 initialize _CX, _CY to default values
		private int _CX = 80;
		private int _CY = 25;

		private string _stringValueCache = null;


		public int CX { get { return _CX; }}
		public int CY { get { return _CY; }}
		public string UserIdentified;
		[XmlIgnore] public string MatchListIdentified;
		public string Name { get { return MatchListIdentified; }}
		[XmlIgnore] public string FileName;
		public Guid UniqueID;

		[XmlIgnore] public string Hash;

        bool isDisposed = false;

        ~XMLScreen()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
                return;
            isDisposed = true;

            if (disposing)
            {
                Field = null;
                Unformatted = null;
                MatchListIdentified = null;
                mScreenBuffer = null;
                mScreenRows = null;
                Hash = null;
            }
        }

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
            //   TO DO: REWRITE THIS CLASS! and maybe the whole process of
            //          getting data from the lower classes, why convert buffers
            //          to XML just to convert them again in this Render method?
            //  ALSO: this conversion process does not take into account that
            //        the XML data that this class is converted from might
            //        contain more than _CX characters in a line, since the
            //        previous conversion to XML converts '<' to "&lt;" and
            //        the like, which will also cause shifts in character positions.
			//
			// Reset cache
			//
			_stringValueCache = null;
			//
			if (_CX==0 || _CY==0)
			{
                // TODO: Need to fix this
				_CX = 132; 
				_CY = 43; 
			}

            // CFCJr 2008/07/11
            if (_CX < 80)
				_CX = 80; 
            if (_CY < 25)
				_CY = 25; 


            // CFCJr 2008/07/11
            if (_CX < 80)
                _CX = 80;
            if (_CY < 25)
                _CY = 25;

			UserIdentified = null;
			MatchListIdentified = null;
			//
			// Render text image of screen
			//
			//
			mScreenBuffer = new char[_CX*_CY];
			mScreenRows   = new string[_CY];
            
            // CFCJr 2008/07/11
            // The following might be much faster:
            //
            //   string str = "".PadRight(_CX*_CY, ' ');
            //   mScreenBuffer = str.ToCharArray();
            //     ........do operations on mScreenBuffer to fill it......
            //   str = string.FromCharArray(mScreenBuffer);
            //   for (int r = 0; r < _CY; r++)
            //        mScreenRows[i] = str.SubString(r*_CY,_CX);
            //
            //  ie, fill mScreenBuffer with the data from Unformatted and Field, then
            //   create str (for the hash) and mScreenRows[]
            //   with the result.
			int i;
			for (i = 0; i < mScreenBuffer.Length; i++)  // CFCJr. 2008.07/11 replase _CX*CY with mScreenBuffer.Length
			{
				mScreenBuffer[i] = ' ';
			}
			//
			
			int chindex;
			
			if (Field==null || Field.Length==0 && 
				(Unformatted==null || Unformatted.Text==null))
			{
                if ( (Unformatted==null || Unformatted.Text==null) )
				    Console.WriteLine("XMLScreen:Render: **BUGBUG** XMLScreen.Unformatted screen is blank");
                else
                    Console.WriteLine("XMLScreen:Render: **BUGBUG** XMLScreen.Field is blank");

                Console.Out.Flush();

                // CFCJr. Move logic for what is in mScreenRows to seperate if logic
                //        this will give unformatted results even if Field==null or 0 length
                //        and vise-a-versa.
                /*
				for (i=0; i<mScreenRows.Length; i++)
				{
					mScreenRows[i] = new String(' ',_CX); 
				}
                */
			}

            string blankRow = string.Empty;
            blankRow = blankRow.PadRight(_CX, ' ');

            if ((Unformatted == null || Unformatted.Text == null))
            {
                // CFCJr. 2008/07/11 initilize a blank row of _CX (80?) spaces

                for (i = 0; i < mScreenRows.Length; i++)
                {
                    //mScreenRows[i] = "                                                                                              ".Substring(0, _CX);
                    // CFCJr. 2008/07/11 replace above method of 80 spaces with following
                    mScreenRows[i] = blankRow;
                }
			}
			else
			{
				for (i=0; i<Unformatted.Text.Length; i++)
				{
					string text = Unformatted.Text[i];

                    // CFCJr, make sure text is not null

                    if (string.IsNullOrEmpty(text))
                        text = string.Empty;

                    // CFCJr, replace "&lt;" with '<'
                    text = text.Replace("&lt;", "<");
                    
                    // CFCJr, Remove this loop to pad text
                    // and use text.PadRight later.
                    // This will help in not processing more
                    // characters than necessary into mScreenBuffer
                    // below

					//while (text.Length < _CX)
					//	text+=" ";

					//
					int p;
					//for (p=0; p<_CX; p++)
                    for( p = 0; p < text.Length; p++ ) // CFC,Jr.
					{
						if (text[p]<32 || text[p]>126)
							text = text.Replace(text[p], ' ');
					}
					//
					//for (chindex=0; chindex<Unformatted.Text[i].Length; chindex++)
                    // CFCJr, 2008/07/11 use text.length instead of Unformatted.Text[i].Length
                    // since we only pad text with 80 chars but if Unformatted.Text[i]
                    // contains XML codes (ie, "&lt;") then it could be longer than
                    // 80 chars (hence, longer than text). 
                    // Also, I replace "&lt;" above with "<".

                    for ( chindex = 0; chindex < text.Length; chindex++ )
					{
                        // CFCJr, calculate mScreenBuffer index only once
                        int bufNdx = chindex + (i * _CX);

						if (bufNdx < mScreenBuffer.Length)
						{
							mScreenBuffer[bufNdx] = text[chindex];
						}
					}
                    // CFCJr, make sure we don't overflow the index of mScreenRows
                    //        since i is based on the dimensions of Unformatted.Text
                    //        instead of mScreenRows.Length
                    if (i < mScreenRows.Length)
                    {
                        text = text.PadRight(_CX, ' ');  // CFCJr. 2008/07/11 use PadRight instead of loop above
					  mScreenRows[i] = text;
				}
				}
            }

            // CFCJr, lets make sure we have _CY rows in mScreenRows here
            // since we use Unformated.Text.Length for loop above which
            // could possibly be less than _CY.

            for ( i = 0; i < mScreenRows.Length; i++ )
                if ( string.IsNullOrEmpty(mScreenRows[i]) )
                    mScreenRows[i] = blankRow;

            //==============
            // Now process the Field (s)

            if ( Field!=null && Field.Length > 0 )
            {
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
                            // CFCJr, 2008/07/11 make sure we don't get out of bounds 
                            //        of the array m_ScreenBuffer.
                            int bufNdx = chindex + field.Location.left + field.Location.top * _CX;
                            if ( bufNdx >= 0 &&  bufNdx < mScreenBuffer.Length )
							    mScreenBuffer[bufNdx] = ch;
						}
					}
				}

                // CFCJr, 2008/07/11
                // SOMETHING needs to be done in this method to speed things up.
                // Above, in the processing of the Unformatted.Text, Render()
                // goes to the trouble of loading up mScreenBuffer and mScreenRows.
                // now here, we replace mScreenRows with the contents of mScreenBuffer.
                // Maybe, we should only load mScreenBuffer and then at the end
                // of Render(), load mScreenRows from it (or vise-a-vera).
                // WE COULD ALSO use
                //   mScreenRows[i] = string.FromCharArraySubset(mScreenBuffer, i*_CX, _CX);
                //  inside this loop.

				for (i=0; i<_CY; i++)
				{
					string temp = string.Empty; // CFCJr, 2008/07/11 replace ""

					for (int j = 0; j<_CX; j++)
					{
						temp+=mScreenBuffer[i*_CX+j];
					}
					mScreenRows[i] = temp;
				}
			}

			// now calculate our screen's hash
			//
			// CFCJr, dang, now we're going to copy the data again,
            //   this time into a long string.....(see comments at top of Render())
            //   I bet there's a easy way to redo this class so that we use just
            //   one buffer (string or char[]) instead of all these buffers.
            // WE COULD also use
            //   string hashStr = string.FromCharArray(mScreenBuffer);
            // instead of converting mScreenRows to StringBuilder 
            // and then converting it to a string.

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
			var screenBuffer = this.mScreenBuffer;
			if (screenBuffer == null) return null;
			int i;
			string result = "";
			int maxlen = screenBuffer.Length;
			for (i = 0; i < length; i++)
			{
				if (i + offset < maxlen)
				{
					if (screenBuffer.Length > i + offset)
						result += screenBuffer[i + offset];
				}
			}
			return result;
		}
		/*
		*/
		public int LookForTextStrings(string[] text)
		{
			string buffer = new String(this.mScreenBuffer);

			for (int i = 0; i < text.Length; i++)
			{
				if (buffer.Contains(text[i])) return i;

			}
			return -1;
		}
		public StringPosition LookForTextStrings2(string[] text)
		{

			string buffer = new String(this.mScreenBuffer);

			for (int i = 0; i < text.Length; i++)
			{
				if (buffer.Contains(text[i]))
				{
					int index = buffer.IndexOf(text[i]);
					StringPosition s = new StringPosition();
					s.indexInStringArray = i;
					s.str = text[i];
					s.x = index % _CX;
					s.y = index / _CX;
					return s;
				}
			}
			return null;

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
		bool debugWithCoordinates = true;
		public void Dump(IAudit stream)
		{
			int i;
			if (debugWithCoordinates)
			{

				stream.WriteLine("-----");
				string tens = "  ", singles = "  "; // the quoted strings must be 3 spaces each, it gets lost in translation by codeplex...
				for (i = 0; i < _CX; i += 10)
				{
					tens += String.Format("{0,-10}", i / 10);
					singles += "0123456789";
				}
				stream.WriteLine(tens.Substring(0, 2 + _CX));
				stream.WriteLine(singles.Substring(0, 2 + _CX));
			}
			for (i = 0; i < _CY; i++)
			{
				string line = GetText(0, i, _CX);
				if (debugWithCoordinates)
				{
					line = String.Format(" {0,02}{1}", i, line);
				}
					stream.WriteLine(line);
				}
			if (debugWithCoordinates)  stream.WriteLine("-----");
		}

        public string[] GetUnformatedStrings()
        {
            if (Unformatted != null && Unformatted.Text != null)
                return Unformatted.Text;
            return null;
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

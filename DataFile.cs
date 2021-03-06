using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

using System.Windows.Forms;

using System.Web.Script.Serialization;
using System.Web;
using System.Net;

namespace DataFileReader 
//namespace DDDReader
{
    /*
     -question - victor gerver is in wialon with driver card number ending in 1 but in test ddd it appears with card num ending in 2?
     -we cannot have in driver card data for same day from different sources
     * 
     prepare table vehicle record:
     -id
     -vehicle plate number
     -odometer begin
     -odometer end
     -vehicle first use time
     -vehicle last use time
     -driver card id
     */



    //driver config line 41, 47, 48, 49,56 - modified from Name to SimpleString
	/// <summary>
	/// The core class that can read a configuration describing the tachograph data structure (one
    /// for driver cards, one for vehicle cards), then read the file itself into memory, then finally
    /// write the data as XML.
	/// </summary>
	public class DataFile : Region
	{

        

		public ArrayList regions=new ArrayList();

		/// <summary>
		/// This method loads the XML config file into a new instance
		/// of VuDataFile, prior to processing.
		/// </summary>
		/// <param name="configFile">Config to load</param>
		/// <returns>A new instance ready for processing</returns>
		// 
		public static DataFile Create(string configFile)
		{
			XmlReader xtr=XmlReader.Create(File.OpenRead(configFile));
			return Create(xtr);
		}

		protected static DataFile Create(XmlReader xtr)
		{
			XmlSerializer xs=new XmlSerializer(typeof(DataFile));
			return (DataFile) xs.Deserialize(xtr);
		}

		/// Convenience method to open a file and process it
		public void Process(string dataFile, XmlWriter writer)
		{
            WriteLine(LogLevel.INFO, "Processing {0}", dataFile);
            if (!dataFile.StartsWith("M_"))
            {
                dataFile = @Application.StartupPath + "\\" + "dddfiles" + "\\" + dataFile;
                Stream s = new FileStream(dataFile, FileMode.Open, FileAccess.Read);
                Process(s, writer);
            }
		}

        public void Process(Stream s, XmlWriter writer)
        {
            CustomBinaryReader r = new CustomBinaryReader(s);
            Process(r, writer);
        }

		/// This is the core method overridden by all subclasses of Region
		// TODO: M: very inefficient if no matches found - will iterate over WORDs to end of file
		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			WriteLine(LogLevel.DEBUG, "Processing...");

			var unmatchedRegions=0;
			// in this case we read a magic and try to process it
			while ( true )
			{
				byte[] magic=new byte[2];
				int bytesRead=reader.BaseStream.Read(magic, 0, 2);
				long magicPos = reader.BaseStream.Position - 2;

				if ( bytesRead == 0 )
					// end of file - nothing more to read
					break;

				if ( bytesRead == 1 )
					// this can happen if zipping over unmatched bytes at end of file - should handle better
					//					throw new InvalidOperationException("Could only read one byte of identifier at end of stream");
					break;

				// test whether the magic matches one of our child objects
				string magicString=string.Format("0x{0:X2}{1:X2}", magic[0], magic[1]);
				bool matched = false;
				foreach ( IdentifiedObjectRegion r in regions )
				{
					if ( r.Matches(magicString) )
					{                        
						WriteLine(LogLevel.DEBUG, "Identified region: {0} with magic {1} at 0x{2:X4}", r.Name, magicString, magicPos);
						r.Process(reader, writer);
						matched = true;
						break;
					}
				}

				if ( !matched ) {
					unmatchedRegions++;
					if ( unmatchedRegions == 1 ) {
						WriteLine(LogLevel.WARN, "First unrecognised region with magic {1} at 0x{1:X4}", magicString, magicPos);
					}
				}
                // commenting @davispuh change because some files have unknown sections, so we take a brute
                // for approach and just skip over any unrecognised data
				// if (!matched)
				// {
				// 	WriteLine(LogLevel.WARN, "Unrecognized magic=0x{0:X2}{1:X2} at offset 0x{2:X4}  ", magic[0], magic[1], magicPos);
				// 	throw new NotImplementedException("Unrecognized magic " + magicString);
				// }
			}
			if ( unmatchedRegions > 0 ) {
				WriteLine(LogLevel.WARN, "There were {0} unmatched regions (magics) in the file.", unmatchedRegions);
			}
			WriteLine(LogLevel.DEBUG, "Processing done.");

		}

		/// This defines what children we can have from the XML config
		[XmlElement("IdentifiedObject", typeof(IdentifiedObjectRegion)),
		XmlElement("ElementaryFile", typeof(ElementaryFileRegion))]
		public ArrayList Regions
		{
			get { return regions; }
			set { regions = value; }
		}
	}
	/// Simple subclass to hold the identified or 'magic' for the region (used by VuDataFile above)
	public class IdentifiedObjectRegion : ContainerRegion
	{
		[XmlAttribute]
		public string Identifier;

		public bool Matches(string s)
		{
			// match a magic if we have null identifier or it actually matches
			// (allows provision of a catch all region which is only really useful during development)
			return Identifier == null || Identifier.Length == 0 || s.Equals(Identifier);
		}
	}

	public class ElementaryFileRegion : IdentifiedObjectRegion
	{
		protected override bool SuppressElement(CustomBinaryReader reader)
		{
			int type=reader.PeekChar();
			return type == 0x01;
		}

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			// read the type
			byte type=reader.ReadByte();

			regionLength=reader.ReadSInt16();
			long fileLength=regionLength;

			if ( type == 0x01 )
			{
				// this is just the signature
			}
			else
			{
				long start=reader.BaseStream.Position;

				base.ProcessInternal(reader, writer);

				long amountProcessed=reader.BaseStream.Position-start;
				fileLength -= amountProcessed;
			}

			if ( fileLength > 0 )
			{
				// deal with a remaining fileLength that is greater than int
				while ( fileLength > int.MaxValue )
				{
					reader.ReadBytes(int.MaxValue);
					fileLength-=int.MaxValue;
				}
				reader.ReadBytes((int) fileLength);
			}
		}
	}
    

	public class CyclicalActivityRegion : ContainerRegion
	{
        

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			if (regionLength <= 4)
			{
				WriteLine(LogLevel.WARN, "CyclicalActivityRegion is empty!");
				return;
			}

			uint oldest=reader.ReadSInt16();
			uint newest=reader.ReadSInt16();

			// length is length of region minus the bytes we've just read
			long effectiveLength=regionLength - 4;
			long position=reader.BaseStream.Position;

			if (position + effectiveLength > reader.BaseStream.Length)
			{
				WriteLine(LogLevel.WARN, "CyclicalActivityRegion position=0x{0:X4} + effectiveLength=0x{1:X4} > length=0x{2:X4} !", position, effectiveLength, reader.BaseStream.Length);
				return;
			}

			WriteLine(LogLevel.DEBUG, "Oldest 0x{0:X4} (offset 0x{2:X4}), newest 0x{1:X4} (offset 0x{3:X4})",
				position+oldest, position+newest,
				oldest, newest);

			if ( oldest == newest && oldest == 0 )
				// no data in file
				return;

			if ( newest >= effectiveLength || oldest >= effectiveLength)
			{
				throw new IndexOutOfRangeException("Invalid pointer to CyclicalActivity Record");
			}

			CyclicStream cyclicStream=new CyclicStream(reader.BaseStream, reader.BaseStream.Position, effectiveLength);
			CustomBinaryReader cyclicReader=new CustomBinaryReader(cyclicStream);

			reader.BaseStream.Seek(oldest, SeekOrigin.Current);

			bool last=false;
            
			while ( !last )
			{
				long pos=cyclicStream.Position;
				if ( pos == newest )
					last=true;

				base.ProcessInternal(cyclicReader, writer);
				// commenting @davispuh mod because it can cause premature termination
				// see https://github.com/jugglingcats/tachograph-reader/issues/28
				// if (cyclicStream.Wrapped)
				// {
				// 	last = true;
				// }
			}
            
			reader.BaseStream.Position = position + effectiveLength;

            if (DDDReader.Program.mainForm.createXML) writer.WriteElementString("DataBufferIsWrapped", cyclicStream.Wrapped.ToString());
            //DDDReader.Form1.updateTxt("DataBufferIsWrapped:"+ cyclicStream.Wrapped.ToString());
            //DDDReader.Program.mainForm.updateTxt("DataBufferIsWrapped:" + cyclicStream.Wrapped.ToString()+"\r\n");
		}
	}

    public class driverActivity {
        //public string time;
        public uint time;//190604 
        public string activity;
    }
    public class driverActivityMain{
        public string date;
        public string dailypresence;
        //public string distance;
    }

	public class DriverCardDailyActivityRegion : Region
	{
		private uint previousRecordLength;
		private uint currentRecordLength;
		private DateTime recordDate;
		private uint dailyPresenceCounter;
		private uint distance;

        private void getMinMaxDate() {
          
            //http://localhost/tacho/getDriverMinMaxDate.php?driverid=DF00000160178002&cmd=min
            string uri = "http://localhost/tacho/getDriverMinMaxDate.php?cmd=min&driverid=" + DDDReader.Program.mainForm.driverID;
            HttpWebRequest httpRequest = (HttpWebRequest)HttpWebRequest.Create(uri);
            HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();
            Encoding enc = System.Text.Encoding.GetEncoding(1252);
            StreamReader loResponseStream = new StreamReader(httpResponse.GetResponseStream(), enc);
            string Response = loResponseStream.ReadToEnd();
            DateTime tmpdt;
            DateTime.TryParse(Response, out tmpdt);//Int32.Parse(Response);\
            DDDReader.Program.mainForm.recordMinDate = tmpdt;
            loResponseStream.Close();
            httpResponse.Close();

            uri = "http://localhost/tacho/getDriverMinMaxDate.php?cmd=max&driverid=" + DDDReader.Program.mainForm.driverID;
            httpRequest = (HttpWebRequest)HttpWebRequest.Create(uri);
            httpResponse = (HttpWebResponse)httpRequest.GetResponse();
            enc = System.Text.Encoding.GetEncoding(1252);
            loResponseStream = new StreamReader(httpResponse.GetResponseStream(), enc);
            Response = loResponseStream.ReadToEnd();
            DateTime.TryParse(Response, out tmpdt);
            DDDReader.Program.mainForm.recordMaxDate = tmpdt;
            loResponseStream.Close();
            httpResponse.Close();

            DDDReader.Program.mainForm.recordMaxTimeHM = 0;//clear for next day input 190613
            DDDReader.Program.mainForm.recordMinTimeHM = 0;
        }

        private void checkNeedToSaveDriverToDB() {
            /*
             * flag 8 - read custom fields
             http://hst-api.wialon.com/wialon//ajax.html?svc=core/update_data_flags&sid=05f3ef926f4b2b612f54d7b8709b0158&params={%22spec%22:[{%22type%22:%22type%22,%22data%22:%22avl_unit%22,%22flags%22:8,%22mode%22:0}]}
            
             [{"i":11877080,"d":null,"f":8},{"i":12201684,"d":null,"f":8},..., 
             * {"i":19337921,"d":{"flds":{"1":{"id":1,"n":"Fahrzeug","v":"MAN"},"2":{"id":2,"n":"Gruppe","v":"UTLPB"},"3":{"id":3,"n":"Kennzeichen","v":"PB-UT-2030"},"4":{"id":4,"n":"Kunde","v":"Hilltronic \/ Universaltransporte Tacho"},"5":{"id":5,"n":"vormals","v":""}},"fldsmax":0},"f":8},..
             */
            
            string urlGetAUs =
               DDDReader.Program.mainForm.wialonurlfix + "/ajax.html?svc=core/update_data_flags&sid=" + DDDReader.Program.mainForm.wialonLogin.eID +
               "&params={" +
                   "\"spec\":[" +
                   "{" +
                       "\"type\":\"type\"," +
                       "\"data\":\"avl_unit\"," +//use avl_unit
                       "\"flags\":8," +
                        /* 1 - get common information(NAMEs) ; 
                         * 256-phone+unique ID ; 
                         * 8192 - current value for km/hours/KB;
                         * 131072 - trip detection details
                         * 8- custom fields
                         */
                       "\"mode\":0" +                /* set flags */
                   "}" +
                   "]" +
               "}";
            //units custom fields
            WebClient wc = new WebClient();
            wc.Encoding = Encoding.UTF8;
            string result = (wc.DownloadString(urlGetAUs));
            //"[{\"i\":11877080,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12201684,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12201688,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12201692,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12319401,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"MCC2264\"}},\"fldsmax\":0},\"f\":8},{\"i\":12400845,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"MCC2402\"},\"2\":{\"id\":2,\"n\":\"Mercedes\",\"v\":\"Actros\"},\"3\":{\"id\":3,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12401054,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12401055,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12401060,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12410535,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12453675,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12453680,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12488262,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12488263,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12488266,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12503290,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2510\"}},\"fldsmax\":0},\"f\":8},{\"i\":12503292,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12503295,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12503299,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12503303,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2131\"},\"2\":{\"id\":2,\"n\":\"ex\",\"v\":\"MCC8630\"}},\"fldsmax\":0},\"f\":8},{\"i\":12503313,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12522927,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12522942,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12522945,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"MCC2020\"}},\"fldsmax\":0},\"f\":8},{\"i\":12522953,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"vormals MCC8146\",\"v\":\"jetzt MCC2511\"}},\"fldsmax\":0},\"f\":8},{\"i\":12522955,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12522972,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12522976,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12522991,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12522993,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12523004,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12523032,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12523041,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12533926,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12533931,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12533939,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12533940,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex\",\"v\":\"MCC2702\"}},\"fldsmax\":0},\"f\":8},{\"i\":12533941,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2703\"}},\"fldsmax\":0},\"f\":8},{\"i\":12533943,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12533951,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"EX\",\"v\":\"MCC2593 umbenannt am 13.10.15\"},\"2\":{\"id\":2,\"n\":\"EX\",\"v\":\"MCC2700 umbenannt am 14.02.2018\"}},\"fldsmax\":0},\"f\":8},{\"i\":12533975,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex Gerät\",\"v\":\"MCC2266\"}},\"fldsmax\":0},\"f\":8},{\"i\":12533976,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2043\"}},\"fldsmax\":0},\"f\":8},{\"i\":12533977,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12533978,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12533979,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12533982,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12533989,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12533997,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12534000,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12543538,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12543584,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2409\"},\"2\":{\"id\":2,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12543590,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2410\"},\"2\":{\"id\":2,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12543593,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex.\",\"v\":\"MCC2411\"},\"2\":{\"id\":2,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12543601,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2407\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":12543615,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2408\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594200,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594221,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594226,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594228,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594230,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594276,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594290,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594303,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594322,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594333,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594348,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex Gerät getauscht\",\"v\":\"MCC2112\"},\"2\":{\"id\":2,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594367,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594380,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12594412,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12633762,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":12633763,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":12702829,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":12828208,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":12828212,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2113\"},\"2\":{\"id\":2,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12864496,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12873773,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex\",\"v\":\"MCC2461\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":12873783,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":12970880,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":13056095,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13056096,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13056097,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13056098,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13056100,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13056102,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC8631\"}},\"fldsmax\":0},\"f\":8},{\"i\":13056126,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2227\"}},\"fldsmax\":0},\"f\":8},{\"i\":13056129,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2228\"}},\"fldsmax\":0},\"f\":8},{\"i\":13056130,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"MCC2229\"},\"2\":{\"id\":2,\"n\":\"ex\",\"v\":\"MCC2226\"}},\"fldsmax\":0},\"f\":8},{\"i\":13256294,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"MCC2027\"}},\"fldsmax\":0},\"f\":8},{\"i\":13526175,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSGO\"},\"2\":{\"id\":2,\"n\":\"ex.\",\"v\":\"MCC2394\"},\"3\":{\"id\":3,\"n\":\"exex\",\"v\":\"MCC2406\"}},\"fldsmax\":0},\"f\":8},{\"i\":13568523,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13568527,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13568537,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13568538,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13589166,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":13589171,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13619400,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC8054\"}},\"fldsmax\":0},\"f\":8},{\"i\":13631271,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13631274,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13631281,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13639293,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"IMEI\",\"v\":\"356173065609237\"}},\"fldsmax\":0},\"f\":8},{\"i\":13639299,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"IMEI\",\"v\":\"356173065545969\"}},\"fldsmax\":0},\"f\":8},{\"i\":13666399,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13666400,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13794736,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13858039,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":13870229,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13870230,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13883281,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":13914979,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13914980,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":13915011,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":14076102,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":14386573,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":14701017,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":14701018,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":14701022,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":14701024,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":14826229,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"3\":{\"id\":3,\"n\":\"ex.\",\"v\":\"MCC2009\"}},\"fldsmax\":0},\"f\":8},{\"i\":14826231,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"ex. MCC2026\"},\"5\":{\"id\":5,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15078143,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":15078151,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"UTMBA MCC2200\"}},\"fldsmax\":0},\"f\":8},{\"i\":15078155,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"ex UTMBA MCC2259\"}},\"fldsmax\":0},\"f\":8},{\"i\":15078157,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2260 UTMBA\"}},\"fldsmax\":0},\"f\":8},{\"i\":15103401,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2223 UTMBA\"}},\"fldsmax\":0},\"f\":8},{\"i\":15103403,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"ACTROS 2013\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"UTMBA MCC2271\"}},\"fldsmax\":0},\"f\":8},{\"i\":15103406,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"UTMBA MCC2273\"}},\"fldsmax\":0},\"f\":8},{\"i\":15103409,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":15150410,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15150418,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15150421,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15150444,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15150464,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15150472,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15150483,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Ex\",\"v\":\"MCC2272 UTMBA\"}},\"fldsmax\":0},\"f\":8},{\"i\":15201513,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15201517,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":15216045,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15216047,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15216050,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15216051,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15216060,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15234280,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15234284,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC8143\"},\"4\":{\"id\":4,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15260566,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15260567,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15260620,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO Tacho\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15260623,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO Tacho\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15260656,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO Tacho\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15260669,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO Tacho\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15260715,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15260752,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15260776,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15260817,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"ex. UTMBP ex. UTMHH now UTPCZ\"}},\"fldsmax\":0},\"f\":8},{\"i\":15264149,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15264186,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15264347,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho \"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"ex UTSAU jetzt UTSGO\"},\"4\":{\"id\":4,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":15264393,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":15264441,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":15264555,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MERCEDES\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0},\"f\":8},{\"i\":15264578,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":15265037,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15265103,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15265285,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MERCEDES\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":15265323,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MERCEDES\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":15265385,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MERCEDES\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":15292637,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15292641,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15292644,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15292645,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15292649,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15447072,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15447076,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15447079,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15447083,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15447085,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":15858270,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MERCEDES\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0},\"f\":8},{\"i\":15970152,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":16194457,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":16291809,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":16291811,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":16594378,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":16594445,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":16594528,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":16594552,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":16637267,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTMHH\"}},\"fldsmax\":0},\"f\":8},{\"i\":16677536,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":16677592,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":16677675,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":16677692,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":16677727,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":16678102,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":16678223,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":16678323,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB UT 2217\"}},\"fldsmax\":0},\"f\":8},{\"i\":16678346,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":16678391,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":16678433,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2464\"}},\"fldsmax\":0},\"f\":8},{\"i\":16802863,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2390\"}},\"fldsmax\":0},\"f\":8},{\"i\":16802904,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":16802923,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":16802948,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":16802996,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB UT 2215\"}},\"fldsmax\":0},\"f\":8},{\"i\":16803024,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"2\":{\"id\":2,\"n\":\"Start\",\"v\":\"03.2018\"}},\"fldsmax\":0},\"f\":8},{\"i\":16803040,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"2\":{\"id\":2,\"n\":\"Start\",\"v\":\"03.2018\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2320\"}},\"fldsmax\":0},\"f\":8},{\"i\":16803086,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"2\":{\"id\":2,\"n\":\"Start\",\"v\":\"03.2018\"}},\"fldsmax\":0},\"f\":8},{\"i\":16803132,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"2\":{\"id\":2,\"n\":\"Start\",\"v\":\"03.2018\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2333\"}},\"fldsmax\":0},\"f\":8},{\"i\":16826952,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":16826954,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":16826957,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":16866726,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2531 UTMBA\"}},\"fldsmax\":0},\"f\":8},{\"i\":16866755,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"BA UT 2532 MCC2532\"}},\"fldsmax\":0},\"f\":8},{\"i\":16963104,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"IMEI\",\"v\":\"862462039194799\"}},\"fldsmax\":0},\"f\":8},{\"i\":16963304,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"IMEI\",\"v\":\"862462039350300\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17025906,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"IMEI\",\"v\":\"862462039194799\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17039849,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransport Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17040585,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17040937,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17073192,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":17073200,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":17136374,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":17376801,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17376886,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17376979,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17377028,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17377110,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17377135,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":17377155,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":17377467,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17377556,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17377723,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17377769,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB UT 2406\"}},\"fldsmax\":0},\"f\":8},{\"i\":17377975,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"2\":{\"id\":2,\"n\":\"Start\",\"v\":\"06.2018\"}},\"fldsmax\":0},\"f\":8},{\"i\":17378017,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17378643,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":17378648,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":17378649,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":17624326,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"\"},\"5\":{\"id\":5,\"n\":\"Kennzeichen\",\"v\":\"PB UT 2485\"}},\"fldsmax\":0},\"f\":8},{\"i\":17624332,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"\"},\"5\":{\"id\":5,\"n\":\"Kennzeichen\",\"v\":\"PB UT 2486\"}},\"fldsmax\":0},\"f\":8},{\"i\":17624335,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"\"},\"5\":{\"id\":5,\"n\":\"Kennzeichen\",\"v\":\"PB_UT_2487\"}},\"fldsmax\":0},\"f\":8},{\"i\":17624341,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17624519,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":17624522,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2727\"}},\"fldsmax\":0},\"f\":8},{\"i\":17624523,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":17687671,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"ex\",\"v\":\"MCC2395 BA UT 2395 UTMBA\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"Gruppe\",\"v\":\"UTMHH\"}},\"fldsmax\":0},\"f\":8},{\"i\":17687952,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"2412\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"ex\",\"v\":\"MCC2412 UTMBA\"}},\"fldsmax\":0},\"f\":8},{\"i\":17719928,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMBA\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"ex\",\"v\":\"MCC2410 UTMBA BA UT 2410\"}},\"fldsmax\":0},\"f\":8},{\"i\":17720127,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"Kennzeichen\",\"v\":\"MCC2032\"},\"5\":{\"id\":5,\"n\":\"ex.\",\"v\":\"UTMBA MCC2397\"}},\"fldsmax\":0},\"f\":8},{\"i\":17720327,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMBA\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"Kennzeichen\",\"v\":\"BA UT 2391\"}},\"fldsmax\":0},\"f\":8},{\"i\":17720502,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTMPB\"},\"4\":{\"id\":4,\"n\":\"Kennzeichen\",\"v\":\"PB-UT 2359\"}},\"fldsmax\":0},\"f\":8},{\"i\":17721413,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTMHH\"}},\"fldsmax\":0},\"f\":8},{\"i\":17721507,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTMPB\"},\"4\":{\"id\":4,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17929953,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"2\":{\"id\":2,\"n\":\"Start\",\"v\":\"08.2018\"}},\"fldsmax\":0},\"f\":8},{\"i\":17964825,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex.\",\"v\":\"2530\"}},\"fldsmax\":0},\"f\":8},{\"i\":17964879,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":17965363,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":17965420,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":17965484,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":17965546,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"mcc2446\"}},\"fldsmax\":0},\"f\":8},{\"i\":17965652,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":17965723,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":17965884,\"d\":{\"flds\":{},\"fldsmax\":0},\"f\":8},{\"i\":18005786,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18027033,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB UT 2017\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"},\"6\":{\"id\":6,\"n\":\"hw\",\"v\":\"Tacho\"},\"7\":{\"id\":7,\"n\":\"alte HW\",\"v\":\"868597033180460\"},\"8\":{\"id\":8,\"n\":\"alte Phone\",\"v\":\"352602140187980\"}},\"fldsmax\":0},\"f\":8},{\"i\":18089828,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"mcc2446\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTEET = Ägypten\"}},\"fldsmax\":0},\"f\":8},{\"i\":18129550,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18129590,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18129674,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18157194,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMPB\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18245652,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"MCC2530\"}},\"fldsmax\":0},\"f\":8},{\"i\":18279119,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"hw\",\"v\":\"Tacho\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18279122,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"hw\",\"v\":\"Tacho\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18279124,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"hw\",\"v\":\"Tacho\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18592734,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18592744,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18592785,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669922,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"},\"5\":{\"id\":5,\"n\":\"IMEI\",\"v\":\"356173067219720\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669932,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067492327\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669938,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067167838\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669946,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067167846\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669948,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067492368\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669950,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067166715\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669952,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067480652\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669955,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067493531\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669958,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067168760\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669962,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067491261\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669965,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067197116\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669966,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067493333\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669968,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067169206\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669969,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067219134\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669971,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067493655\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669973,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067492681\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669978,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067198130\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669982,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067496310\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669986,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067496088\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669989,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067222153\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18669993,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173064621639\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0},\"f\":8},{\"i\":18740152,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"hw\",\"v\":\"Tacho\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18740209,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"MCC2226\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18740212,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"MCC2226\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18775821,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTPCZ\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18806520,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"WMA13XZZ7KM820497\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTPCZ\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18980190,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\".\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTPCZ\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18980198,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\".\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTPCZ\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":18991522,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\".\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTPCZ\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19028160,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\".\"},\"2\":{\"id\":2,\"n\":\"hw\",\"v\":\"Tacho\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19045765,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":19045767,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":19045770,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":19197554,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2065\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":19197636,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"hw\",\"v\":\"Tacho\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19337859,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2024\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":19337880,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2028\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":19337921,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2030\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":19337949,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2034\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":19337967,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2042\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":19338099,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2630\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"3\":{\"id\":3,\"n\":\"Start\",\"v\":\"05.2019\"}},\"fldsmax\":0},\"f\":8},{\"i\":19338114,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2631\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"3\":{\"id\":3,\"n\":\"Start\",\"v\":\"05.2019\"}},\"fldsmax\":0},\"f\":8},{\"i\":19338123,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2632\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"3\":{\"id\":3,\"n\":\"Start\",\"v\":\"05.2019\"}},\"fldsmax\":0},\"f\":8},{\"i\":19370288,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2019\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0},\"f\":8},{\"i\":19372092,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"Tausch HW 01.06.2019\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"4\":{\"id\":4,\"n\":\"Kennzeichen\",\"v\":\"MCC2032\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413945,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413948,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413953,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413954,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413958,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413960,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413962,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413963,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413968,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413970,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413973,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413977,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413978,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413983,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413988,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413990,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413993,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19413997,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19414001,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19414005,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19414008,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8},{\"i\":19414015,\"d\":{\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0},\"f\":8}]\n"
        }
        private void checkNeedToSaveDriverToWialon() { }

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			previousRecordLength=reader.ReadSInt16();
			currentRecordLength=reader.ReadSInt16();
			recordDate=reader.ReadTimeReal();
			dailyPresenceCounter=reader.ReadBCDString(2);
			distance=reader.ReadSInt16();

            if (DDDReader.Program.mainForm.createXML)
            {
                writer.WriteAttributeString("DateTime", recordDate.ToString("u"));
                writer.WriteAttributeString("DailyPresenceCounter", dailyPresenceCounter.ToString());
                writer.WriteAttributeString("Distance", distance.ToString());
            }

            getMinMaxDate();
            //TODO - save driver to db and wialon
            /*
             *  if driver is NOT in local DB and or in Wialon driver DB,
             *  you need to check, to which subgroup the vehicle belongs to from which you downloaded the ddd file (driver)
             *  We will create for each vehicle a custom field with the name of the customfield “DRIVERGROUP” and the coresponding drivergroup. 
             *  So you can read from vehicle the custom fields, look for “DRIVERGROUP” 
             *  and than you know to which drivergroup the driver needs to be added.

             */
            checkNeedToSaveDriverToDB();
            checkNeedToSaveDriverToWialon();

            string tmp = DDDReader.Program.mainForm.currentFile;
            //?????????????THIS IS NOT NEEDED???????? duplicates next code......
            bool saveToDB = false;
            if (recordDate > DDDReader.Program.mainForm.recordMaxDate) {
                int index2 = tmp.LastIndexOf("_");
                int tmpChkYr = string.Compare(recordDate.Year.ToString(), tmp.Substring(index2 + 1, 4));
                if (index2 != -1)
                {
                    //extract day, eg 4-year, + 2 month, +1 for _=7//20170111
                    if (((Convert.ToInt32(recordDate.Day) - Convert.ToInt32(tmp.Substring(index2 + 7, 2))) == 0) &&
                        ((Convert.ToInt32(recordDate.Month) - Convert.ToInt32(tmp.Substring(index2 + 5, 2))) == 0) &&
                        (tmpChkYr == 0))
                    {
                        saveToDB = false;// MessageBox.Show(tmp.Substring(index2 + 1, 8));//_20170111
                    }
                    else {
                        //if (Convert.ToInt32(recordDate.Year) < 2018) 
                        {//190613 filter out results by year to faster check diff drivers
                            DDDReader.Program.mainForm.recordMaxDate = recordDate; 
                            saveToDB = true; 
                        }
                        
                    }
                }
                //190606 
                //DDDReader.Program.mainForm.recordMaxDate = recordDate; saveToDB = true; 
            }
            //---------------------------------------

            if (DDDReader.Program.mainForm.recordMinDate.Year == 0001) {
                DDDReader.Program.mainForm.recordMinDate = recordDate;
                saveToDB = true;//190613 added
            } else if (recordDate < DDDReader.Program.mainForm.recordMinDate) { 
                int index2 = tmp.LastIndexOf("_");
                int tmpChkYr = string.Compare(recordDate.Year.ToString(), tmp.Substring(index2 + 1, 4));
                if (index2 != -1)
                {
                    //extract day, eg 4-year, + 2 month, +1 for _=7//20170111
                    if (((Convert.ToInt32(recordDate.Day) - Convert.ToInt32(tmp.Substring(index2 + 7, 2))) == 0) &&
                        ((Convert.ToInt32(recordDate.Month) - Convert.ToInt32(tmp.Substring(index2 + 5, 2))) == 0) &&
                        (tmpChkYr == 0))
                    {
                        saveToDB = false;// MessageBox.Show(tmp.Substring(index2 + 1, 8));//_20170111
                    }
                    else
                    {
                        
                            DDDReader.Program.mainForm.recordMinDate = recordDate;
                            saveToDB = true;
                        
                    }
                }
                //190606 
                //DDDReader.Program.mainForm.recordMinDate = recordDate; saveToDB = true; 
            }

            //use savetodb as temp var to make things shorter... 190604
            //use savedaytodb when checking each record activity if it should be written - that will be only needed in the case where 
            //last day being saved is today, and the day records are not completed yet, then next day it should be possible to add missing records

            DDDReader.Program.mainForm.saveDayToDB = saveToDB;

            //if (string.Compare(recordDate.ToString("u").Replace("Z", ""), "2018-04-30") == 0) {
            if (recordDate.ToString("u").Replace("Z", "").Contains("18-04-30"))
            {
                string test = "";
            }
            
            uint recordCount=(currentRecordLength-12)/2;
			WriteLine(LogLevel.DEBUG, "Reading {0} activity records", recordCount);

            //?date=2001-11-02&begin=2007-12-31%2022:59&end=2007-12-31%2023:59&presence=4&distance=5&fileid=6&recordid=7

            //if (saveToDB)//if here then it crashes at cyclicstream.cs ln 39 
            //if (DDDReader.Program.mainForm.done < 3)//just for testing limit to 3 records
            {
                
                string uri = "http://localhost/tacho/getDriverActLastID.php";
                HttpWebRequest httpRequest = (HttpWebRequest)HttpWebRequest.Create(uri);
                HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                Encoding enc = System.Text.Encoding.GetEncoding(1252);
                StreamReader loResponseStream = new StreamReader(httpResponse.GetResponseStream(), enc);
                string Response = loResponseStream.ReadToEnd();
                //DDDReader.Program.mainForm.recordID =
                DDDReader.Program.mainForm.recordID = Int32.Parse(Response)+1;//190604 added +1 to advance the counter
                loResponseStream.Close();
                httpResponse.Close();

                DDDReader.Program.mainForm.recordDateGeneral = recordDate.ToString("u").Replace("Z", "");
                DDDReader.Program.mainForm.distance = distance;
                DDDReader.Program.mainForm.dailyPresenceCounter = dailyPresenceCounter;
                ///////////////////////////
                                                               
                //php code should return 0s if no entries or min/max if entries
                while (recordCount > 0)
                {
                    ActivityChangeRegion acr = new ActivityChangeRegion();

                    acr.Name = "ActivityChangeInfo";
                    acr.Process(reader, writer);
                    recordCount--;                    
                }

                //DDDReader.Program.mainForm.recordMinTime
                //DDDReader.Program.mainForm.recordMaxTime

                //&& (DDDReader.Program.mainForm.driversActivity.Count > 0)
                if (saveToDB )
                    //TODO - check how to avoid driveractivityrecords also!!!!!!!!! maybe comparing with savtodb and with recordMaxTimeHM
                {//190604
                    
                    //190604 moved upload script to here
                    uri = "http://localhost/tacho/uploadDriverActivity.php?date=" + recordDate.ToString("u").Replace("Z", "") + 
                        "&presence=" + dailyPresenceCounter.ToString() + "&distance=" + distance.ToString() +
                        "&driverID=" + DDDReader.Program.mainForm.driverID;
                    httpRequest = (HttpWebRequest)HttpWebRequest.Create(uri);
                    httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                    enc = System.Text.Encoding.GetEncoding(1252);
                    loResponseStream = new StreamReader(httpResponse.GetResponseStream(), enc);
                    Response = loResponseStream.ReadToEnd();
                    //DDDReader.Program.mainForm.recordID = Int32.Parse(Response);
                    loResponseStream.Close();
                    httpResponse.Close();
                    ////////////////////

                    //use string.Format("{0:d2}:{1:d2}", time / 60, time % 60); to convert to string
                    uint startT = DDDReader.Program.mainForm.driversActivity[0].time;
                    uint endT = DDDReader.Program.mainForm.driversActivity[DDDReader.Program.mainForm.driversActivity.Count - 1].time;
                    string startTimeForDB = string.Format("{0:d2}:{1:d2}", startT / 60, startT % 60);
                    string endTimeForDB = string.Format("{0:d2}:{1:d2}", endT / 60, endT % 60);
                    //update timestamps in driver activity....
                    uri = "http://localhost/tacho/updateDriverActivity.php?mints='" + startTimeForDB + "'&maxts='" + 
                        endTimeForDB + "'&id=" + DDDReader.Program.mainForm.recordID;
                    //DDDReader.Program.mainForm.updateTxt(uri);
                    httpRequest = (HttpWebRequest)HttpWebRequest.Create(uri);
                    httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                    enc = System.Text.Encoding.GetEncoding(1252);
                    loResponseStream = new StreamReader(httpResponse.GetResponseStream(), enc);
                    Response = loResponseStream.ReadToEnd();

                    loResponseStream.Close();
                    httpResponse.Close();

                    //*190606 
                    //190613 moved from here
                    //DDDReader.Program.mainForm.recordMaxTimeHM = 0;//clear for next day input 190604
                    //DDDReader.Program.mainForm.recordMinTimeHM = 0;
                    
                    //DDDReader.Program.mainForm.done++;
                }
                DDDReader.Program.mainForm.readyForNextFile = true;
                
                //File.Delete(DDDReader.Program.mainForm.currentFile);//190606 delete ddd file after download
                //if (DDDReader.Program.mainForm.done > 3) MessageBox.Show("finish");
            }
            
		}
	}

	/// Simple logging - can be set on any region. Could be improved
	// TODO: M: make log level command line option
    public enum LogLevel
	{
		NONE=0,
		DEBUG=1,
		INFO=2,
		WARN=3,
		ERROR=4
	}

	/// Abstract base class for all regions. Holds some convenience methods
	public abstract class Region
	{
		// All regions have a name which becomes the XML element on output
		[XmlAttribute]
		public string Name;

		[XmlAttribute]
		public bool GlobalValue;

		[XmlAttribute]
		public LogLevel LogLevel=LogLevel.INFO;

		protected long byteOffset;
		protected long regionLength=0;
		protected static Hashtable globalValues=new Hashtable();
		protected static readonly String[] countries = new string[] {"No information available",
			"Austria","Albania","Andorra","Armenia","Azerbaijan","Belgium","Bulgaria","Bosnia and Herzegovina",
			"Belarus","Switzerland","Cyprus","Czech Republic","Germany","Denmark","Spain","Estonia","France",
			"Finland","Liechtenstein","Faeroe Islands","United Kingdom","Georgia","Greece","Hungary","Croatia",
			"Italy","Ireland","Iceland","Kazakhstan","Luxembourg","Lithuania","Latvia","Malta","Monaco",
			"Republic of Moldova","Macedonia","Norway","Netherlands","Portugal","Poland","Romania","San Marino",
			"Russian Federation","Sweden","Slovakia","Slovenia","Turkmenistan","Turkey","Ukraine","Vatican City",
			"Yugoslavia"};

		public void Process(CustomBinaryReader reader, XmlWriter writer)
		{
			// Store start of region (for logging only)
			byteOffset=reader.BaseStream.Position;

			bool suppress=SuppressElement(reader);

			// Write a new output element
			if ( !suppress )
				writer.WriteStartElement(Name);

			// Call subclass process method
			ProcessInternal(reader, writer);

			// End the element
			if ( !suppress )
				writer.WriteEndElement();

			long endPosition=reader.BaseStream.Position;
			if ( reader.BaseStream is CyclicStream )
				endPosition=((CyclicStream) reader.BaseStream).ActualPosition;

			WriteLine(LogLevel.DEBUG, "{0} [0x{1:X4}-0x{2:X4}/0x{3:X4}] {4}", Name, byteOffset,
				endPosition, endPosition-byteOffset, ToString());

			if ( GlobalValue )
			{
                globalValues[Name] = ToString();
			}
		}

		protected void WriteLine(LogLevel level, string format, params object[] args)
		{
			if ( level >= LogLevel )
				Console.WriteLine(format, args);
		}

		protected abstract void ProcessInternal(CustomBinaryReader reader, XmlWriter writer);

		protected virtual bool SuppressElement(CustomBinaryReader reader)
		{
			// derived classes can override this to suppress the writing of
			// a wrapper element. Used by the ElementaryFileRegion to suppress
			// altogether the signature blocks that occur for some regions
			return false;
		}

		public long RegionLength
		{
			set { regionLength=value; }
		}
	}

	/// Generic class for a region that contains other regions. Used as a simple wrapper
	/// where this is indicated in the specification
	public class ContainerRegion : Region
	{
		public ArrayList regions=new ArrayList();

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			// iterate over all child regions and process them
			foreach ( Region r in regions )
			{
				r.RegionLength=regionLength;
				r.Process(reader, writer);
			}
		}

		// these are the valid regions this class can contain, along with XML name mappings
		[XmlElement("Padding", typeof(PaddingRegion)),
		XmlElement("Collection", typeof(CollectionRegion)),
		XmlElement("Cycle", typeof(CyclicalActivityRegion)),
		XmlElement("DriverCardDailyActivity", typeof(DriverCardDailyActivityRegion)),
		XmlElement("Repeat", typeof(RepeatingRegion)),
		XmlElement("Name", typeof(CodePageStringRegion)),//typeof(NameRegion)),
        //XmlElement("Namestr", typeof(NameRegion)),
		XmlElement("SimpleString", typeof(SimpleStringRegion)),
		//XmlElement("InternationalString", typeof(CodePageStringRegion)),
		XmlElement("ExtendedSerialNumber", typeof(ExtendedSerialNumberRegion)),
		XmlElement("Object", typeof(ContainerRegion)),
		XmlElement("TimeReal", typeof(TimeRealRegion)),
        XmlElement("Datef", typeof(DatefRegion)),
        XmlElement("ActivityChange", typeof(ActivityChangeRegion)),
		XmlElement("CardNumber", typeof(CardNumberRegion)),
		XmlElement("FullCardNumber", typeof(FullCardNumberRegion)),
		XmlElement("Flag", typeof(FlagRegion)),
		XmlElement("UInt24", typeof(UInt24Region)),
		XmlElement("UInt16", typeof(UInt16Region)),
		XmlElement("UInt8", typeof(UInt8Region)),
		XmlElement("BCDString", typeof(BCDStringRegion)),
		XmlElement("Country", typeof(CountryRegion)),
		XmlElement("HexValue", typeof(HexValueRegion))]
		public ArrayList Regions
		{
			get { return regions; }
			set { regions = value; }
		}
	}

	/// Simple class to "eat" a specified number of bytes in the file
	public class PaddingRegion : Region
	{
		[XmlAttribute]
		public int Size;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			byte[] buf=new byte[Size];
			int amountRead=reader.Read(buf, 0, Size);
			//190301 if ( amountRead != Size )
				// throw new InvalidOperationException("End of file reading padding (size "+Size+")");
		}

		public override string ToString()
		{
			return string.Format("{0} bytes (0x{0:X4})", Size);
		}

	}

	// A string that is non-international consisting of a specified number of bytes
	public class SimpleStringRegion : Region
	{
		[XmlAttribute]
		public int Length;
        [XmlAttribute]
        public int Skip;
		protected string text;

		public SimpleStringRegion()
		{
		}

		// this is for the benefit of subclasses
		public SimpleStringRegion(int length)
		{
			this.Length=length;
		}

		// method that will read string from file in specified encoding
		protected void ProcessInternal(CustomBinaryReader s, Encoding enc)
		{
            text = s.ReadString(Length, enc).Trim();
		}

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
            var codepage="";// = reader.ReadByte();
			// we just use the default encoding in the default case
            Encoding _encoding = System.Text.Encoding.GetEncoding(28591);//that is "ISO-8859-1" , https://en.wikipedia.org/wiki/Code_page
            this.ProcessInternal(reader, _encoding);//Encoding.ASCII);
            
            if (text.Length > 0) text = text.Substring(Skip, text.Length - Skip);
            if (DDDReader.Program.mainForm.createXML) writer.WriteString(text);//.Replace("&#x1;", "")
            //190606
            //TODO -refactor this to be separate objects - to remove the ifs !!!!!!!!
            if (string.Compare(Name, "CardHolderSurname") == 0) DDDReader.Program.mainForm.cardHolderLName = text;//DDDReader.Program.mainForm.updateTxt("SimpleStringRegion:" + text + " : "+Name+"\r\n");
            if (string.Compare(Name, "CardHolderFirstNames") == 0) DDDReader.Program.mainForm.cardHolderFName = text;//DDDReader.Program.mainForm.updateTxt("SimpleStringRegion:" + text + " : " + Name + "\r\n");
            if (string.Compare(Name, "CardNumber") == 0) DDDReader.Program.mainForm.cardHolderCardN = text;//DDDReader.Program.mainForm.updateTxt("SimpleStringRegion:" + text + " : " + Name + "\r\n");
            if (Name.Contains("VehicleRegistrationNumber")) DDDReader.Program.mainForm.vehiclePlateNum = text;
		}

		public override string ToString()
		{
			return text;
		}

	}

	// A string that is prefixed by a code page byte
	public class CodePageStringRegion : SimpleStringRegion
	{
		static Dictionary<string, Encoding> encodingCache = new Dictionary<string, Encoding>();
		static Dictionary<byte, string> charsetMapping = new Dictionary<byte, string>();
        
		// private int codepage;

		static CodePageStringRegion() {
			foreach ( var i in Encoding.GetEncodings() ) {
				encodingCache.Add(i.Name.ToUpper(), i.GetEncoding());
			}

			charsetMapping[0] = "ASCII";
			charsetMapping[1] = "ISO-8859-1";
			charsetMapping[2] = "ISO-8859-2";
			charsetMapping[3] = "ISO-8859-3";
			charsetMapping[5] = "ISO-8859-5";
			charsetMapping[7] = "ISO-8859-7";
			charsetMapping[9] = "ISO-8859-9";
			charsetMapping[13] = "ISO-8859-13";
			charsetMapping[15] = "ISO-8859-15";
			charsetMapping[16] = "ISO-8859-16";
			charsetMapping[80] = "KOI8-R";
			charsetMapping[85] = "KOI8-U";

			// CodePagesEncodingProvider (System.Text.Encoding.CodePages package) on .NET Core by default (GetEncodings() method) supports only few encodings
			// https://msdn.microsoft.com/en-us/library/system.text.codepagesencodingprovider.aspx#Anchor_4
			// but if you call GetEncoding directly by name you can get other encodings too
			// so here we add those too to our cache
			foreach (var encodingName in charsetMapping.Values) {
				if (!encodingCache.ContainsKey(encodingName)) {
					try {
						var encoding = Encoding.GetEncoding(encodingName);
						encodingCache.Add(encodingName, encoding);
					} catch (ArgumentException e) {
						Console.WriteLine("Warning! Current platform doesn't support encoding with name {0}\n{1}", encodingName, e.Message);
					}
				}
			}
		}

		public CodePageStringRegion()
		{
		}

		public CodePageStringRegion(int size) : base(size)
		{
		}

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			// get the codepage
			var codepage=reader.ReadByte();
			// codePage specifies the part of the ISO/IEC 8859 used to code this string
            
			//string encodingName = charsetMapping.GetValueOrDefault(codepage, "UNKNOWN");
            string encodingName = "UNKNOWN";
            if (charsetMapping[codepage] != null) encodingName = charsetMapping[codepage];
            encodingName = charsetMapping[1];
			Encoding enc=null;//encodingCache.GetValueOrDefault(encodingName, null);
            enc = encodingCache[encodingName];
            
			if ( enc == null) {
				// we want to warn if we didn't recognize codepage because using wrong codepage will cause use of wrong codepoints and thus incorrect data
				WriteLine(LogLevel.DEBUG, "Unknown codepage {0}", codepage);
				enc=Encoding.ASCII;
			}

			// read string using encoding
			base.ProcessInternal(reader, enc);
            if (DDDReader.Program.mainForm.createXML)  writer.WriteString(text);
		}
	}

	// A name is a string with codepage with fixed length = 35
	public class NameRegion : CodePageStringRegion
	{
		private static readonly int SIZE=35;//190301 was 35

        public NameRegion() : base(SIZE)
        {
        }
        /*
        protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
        {

            writer.WriteAttributeString("CardHolder", reader.ReadBytes(35).ToString());

            base.ProcessInternal(reader, writer);
        }
        
         EquipmentType type;
        byte issuingMemberState;

        protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
        {
            type=(EquipmentType) reader.ReadByte();
            issuingMemberState=reader.ReadByte();

            writer.WriteAttributeString("Type", type.ToString());
            writer.WriteAttributeString("IssuingMemberState", issuingMemberState.ToString());

            base.ProcessInternal(reader, writer);
        }

        public override string ToString()
        {
            return string.Format("type={0}, {1}, {2}, {3}, {4}",
                type, issuingMemberState, driverIdentification, replacementIndex, renewalIndex);
        }
         */
    }

	public class HexValueRegion : Region
	{
		[XmlAttribute]
		public int Length;
		private byte[] values;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			values=new byte[Length];

			for ( int n=0; n< Length; n++ )
				values[n]=reader.ReadByte();

            if (DDDReader.Program.mainForm.createXML) writer.WriteAttributeString("Value", this.ToString());
		}

		public override string ToString()
		{
			if ( values == null )
				return "(null)";

			return ToHexString(values);
		}

		public static string ToHexString(byte[] values)
		{
			StringBuilder sb=new StringBuilder(values.Length*2+2);
			sb.Append("0x");
			foreach ( byte b in values )
				sb.AppendFormat("{0:X2}", b);			

			return sb.ToString();
		}

	}

	// See page 72. Have written class for this as seen in multiple places
	public class ExtendedSerialNumberRegion : Region
	{
		private uint serialNumber;
		private byte month;
		private byte year;
		private byte type;
		private byte manufacturerCode;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			serialNumber=reader.ReadSInt32();
			// BCD coding of Month (two digits) and Year (two last digits)
			uint monthYear=reader.ReadBCDString(2);
			type=reader.ReadByte();
			manufacturerCode=reader.ReadByte();

			month = (byte)(monthYear / 100);
			year = (byte)(monthYear % 100);

            if (DDDReader.Program.mainForm.createXML)
            {
                writer.WriteAttributeString("Month", month.ToString());
                writer.WriteAttributeString("Year", year.ToString());
                writer.WriteAttributeString("Type", type.ToString());
                writer.WriteAttributeString("ManufacturerCode", manufacturerCode.ToString());

                writer.WriteString(serialNumber.ToString());
            }
		}

		public override string ToString()
		{
			return string.Format("{0}, {1}/{2}, type={3}, manuf code={4}",
				serialNumber, month, year, type, manufacturerCode);
		}
	}

	// see page 83 - 4 byte second offset from midnight 1 January 1970
	public class TimeRealRegion : Region
	{
		private DateTime dateTime;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			dateTime=reader.ReadTimeReal();
            /*if (Name.Contains("VehicleFirstUse")) 
                MessageBox.Show("dateTime " + dateTime + Name);*/
            if (Name.Contains("VehicleFirstUse")) DDDReader.Program.mainForm.vehicleFirstUse = dateTime;//dateTime.ToString("yyyy-MM-dd") + "||" + dateTime.ToString("HH:mm:ss");
            if (Name.Contains("VehicleLastUse")) DDDReader.Program.mainForm.vehicleLastUse = dateTime;
            if (DDDReader.Program.mainForm.createXML) writer.WriteAttributeString("DateTime", dateTime.ToString("u"));
		}

		public override string ToString()
		{
			return string.Format("{0}", dateTime);
		}
	}

    // see page 56 (BCDString) and page 69 (Datef) - 2 byte encoded date in yyyy mm dd format
    public class DatefRegion : Region
    {
        private DateTime dateTime;

        protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
        {
            uint year = reader.ReadBCDString(2);
            uint month = reader.ReadBCDString(1);
            uint day = reader.ReadBCDString(1);

            string dateTimeString = null;
            // year 0, month 0, day 0 means date isn't set
            if (year > 0 || month > 0 || day > 0)
            {
                //190401 dateTime = new DateTime((int)year, (int)month, (int)day);
                //dateTimeString = dateTime.ToString("u");
            }

            if (DDDReader.Program.mainForm.createXML) writer.WriteAttributeString("Datef", dateTimeString);
        }

        public override string ToString()
        {
            return string.Format("{0}", dateTime);
        }
    }

    public enum EquipmentType
	{
		DriverCard=1,
		WorkshopCard=2,
		ControlCard=3
		// TODO: M: there are more
	}

	public class CardNumberRegion : Region
	{
		protected string driverIdentification;
		protected byte replacementIndex;
		protected byte renewalIndex;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			driverIdentification=reader.ReadString(16);

            replacementIndex = 0;// 190309 reader.ReadByte();
            renewalIndex = 0;// reader.ReadByte();

            if (DDDReader.Program.mainForm.createXML)
            {
                writer.WriteAttributeString("ReplacementIndex", replacementIndex.ToString());
                writer.WriteAttributeString("RenewalIndex", renewalIndex.ToString());

                writer.WriteString(driverIdentification);
            }
            DDDReader.Program.mainForm.driverID = driverIdentification;
            //DDDReader.Program.mainForm.updateTxt("driverIdentification:" + driverIdentification + " : " + Name + "\r\n");
		}

		public override string ToString()
		{
			return string.Format("{0}, {1}, {2}",
				driverIdentification, replacementIndex, renewalIndex);
		}
	}

	// see page 72 - we only support driver cards
	public class FullCardNumberRegion : CardNumberRegion
	{
		EquipmentType type;
		byte issuingMemberState;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			type=(EquipmentType) reader.ReadByte();
			issuingMemberState=reader.ReadByte();

            if (DDDReader.Program.mainForm.createXML)
            {
                writer.WriteAttributeString("Type", type.ToString());
                writer.WriteAttributeString("IssuingMemberState", issuingMemberState.ToString());
            }
			base.ProcessInternal(reader, writer);
		}

		public override string ToString()
		{
			return string.Format("type={0}, {1}, {2}, {3}, {4}",
				type, issuingMemberState, driverIdentification, replacementIndex, renewalIndex);
		}
	}

	// 3 byte number, as used by OdometerShort (see page 86)
	public class UInt24Region : Region
	{
		private uint uintValue;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			uintValue=reader.ReadSInt24();
            //TODO -refactor this to be separate objects - to remove the ifs !!!!!!!!
            if (string.Compare(Name, "VehicleOdometerBegin") == 0) 
            {
                //MessageBox.Show(uintValue.ToString());//190410 -this extracts the begin odo, need to know when the 
                //DDDReader.Program.mainForm.updTxt(uintValue.ToString());
                DDDReader.Program.mainForm.odometerBegin = uintValue.ToString();
            }
            if (string.Compare(Name, "VehicleOdometerEnd") == 0) DDDReader.Program.mainForm.odometerEnd = uintValue.ToString();

            if (DDDReader.Program.mainForm.createXML) writer.WriteString(uintValue.ToString());
		}

		public override string ToString()
		{
			return uintValue.ToString();
		}
	}

	public class UInt16Region : Region
	{
		private uint uintValue;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			uintValue=reader.ReadSInt16();
            if (DDDReader.Program.mainForm.createXML) writer.WriteString(uintValue.ToString());
		}

		public override string ToString()
		{
			return uintValue.ToString();
		}
	}

	public class CountryRegion : Region
	{
		private string countryName;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			byte byteValue=reader.ReadByte();
			if ( byteValue < countries.Length )
				countryName=countries[byteValue];
			else if (byteValue == 0xFD)
				countryName="European Community";
			else if (byteValue == 0xFE)
				countryName="Europe";
			else if (byteValue == 0xFF)
				countryName="World";
			else
				countryName="UNKNOWN";
            if (DDDReader.Program.mainForm.createXML)
            {
                writer.WriteAttributeString("Name", countryName);
                writer.WriteString(byteValue.ToString());
            }
		}

		public override string ToString()
		{
			return countryName;
		}
	}

	public class UInt8Region : Region
	{
		private byte byteValue;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			byteValue=reader.ReadByte();
            if (DDDReader.Program.mainForm.createXML) writer.WriteString(byteValue.ToString());
		}

		public override string ToString()
		{
			return byteValue.ToString();
		}
	}

	public class BCDStringRegion : Region
	{
		[XmlAttribute]
		public int Size;
		private uint value;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			value=reader.ReadBCDString(Size);
            if (DDDReader.Program.mainForm.createXML) writer.WriteString(value.ToString());
		}

		public override string ToString()
		{
			return value.ToString();
		}
	}

	// Simple class to represent a boolean (0 or 1 in specification)
	public class FlagRegion : Region
	{
		private bool boolValue;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			boolValue=reader.ReadByte() > 0;
            if (DDDReader.Program.mainForm.createXML) writer.WriteString(boolValue.ToString());
		}

		public override string ToString()
		{
			return boolValue.ToString();
		}
	}

	public enum SizeAllocation
	{
		BYTE,
		WORD
	}

	// A collection region is a repeating region prefixed by the count of number of
	// items in the region. The count can be represented by a single byte or a word,
	// depending on the collection, so this supports a SizeAllocation property to specify
	// which it is.
	public class CollectionRegion : ContainerRegion
	{
		[XmlAttribute]
		public SizeAllocation SizeAlloc=SizeAllocation.BYTE;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			// get the count according to allocation size
			uint count;
			switch (SizeAlloc)
			{
				case SizeAllocation.BYTE:
					count=reader.ReadByte();
					break;

				case SizeAllocation.WORD:
					count=reader.ReadSInt16();
					break;

				default:
					throw new InvalidOperationException("Bad size allocation");
			}

			ProcessItems(reader, writer, count);
		}

		protected void ProcessItems(CustomBinaryReader reader, XmlWriter writer, uint count)
		{
			WriteLine(LogLevel.DEBUG, "Processing repeating {0}, count={1}, offset=0x{2:X4}", Name, count, reader.BaseStream.Position);

			// repeat processing of child objects
			uint maxCount = count;
            //if (Name.Contains("CardVehicleRecords")) DDDReader.Program.mainForm.updTxt("CardVehicleRecords start");//MessageBox.Show("CardVehicleRecords start");
			while ( count > 0 )
			{
				try
				{                    
					base.ProcessInternal(reader, writer);
                    //dateTime.ToString("yyyy-MM-dd") + "||" + dateTime.ToString("HH:mm:ss");
                    /*if (Name.Contains("CardVehicleRecords")) DDDReader.Program.mainForm.updTxt("CardVehicleRecords entry " + DDDReader.Program.mainForm.odometerBegin + " : " + DDDReader.Program.mainForm.odometerEnd + " : " + DDDReader.Program.mainForm.vehiclePlateNum + " : " + DDDReader.Program.mainForm.driverID + " : " + DDDReader.Program.mainForm.vehicleFirstUse + " : " + DDDReader.Program.mainForm.vehicleLastUse);
                    //VehiclePointerNewestRecord - todo - read it and break this while loop if reach VehiclePointerNewestRecord value
                    //"&activity=" + activity.ToString() + "&time=" + string.Format("{0:d2}:{1:d2}", time / 60, time % 60) +
                    string uri = "http://localhost/tacho/uploadVehicleActivity.php?platenumber=" + DDDReader.Program.mainForm.vehiclePlateNum +
                        "&odometerbegin=" + DDDReader.Program.mainForm.odometerBegin +
                        "&odometerend=" + DDDReader.Program.mainForm.odometerEnd+
                        "&firstuseday=" + DDDReader.Program.mainForm.vehicleFirstUse.ToString("yyyy-MM-dd") +
                        "&lastuseday=" + DDDReader.Program.mainForm.vehicleLastUse.ToString("yyyy-MM-dd")+
                        "&firstusehour=" + DDDReader.Program.mainForm.vehicleFirstUse.ToString("HH:mm:ss") +
                        "&lastusehour=" + DDDReader.Program.mainForm.vehicleLastUse.ToString("HH:mm:ss")+
                        "&drivercard=" + DDDReader.Program.mainForm.driverID;
                    HttpWebRequest httpRequest = (HttpWebRequest)HttpWebRequest.Create(uri);
                    HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                    Encoding enc = System.Text.Encoding.GetEncoding(1252);
                    StreamReader loResponseStream = new StreamReader(httpResponse.GetResponseStream(), enc);
                    string Response = loResponseStream.ReadToEnd();
                    //MessageBox.Show("recordactivity: "+Response);
                    loResponseStream.Close();
                    httpResponse.Close();*/
                    

					count--;
				} catch (EndOfStreamException ex)
				{
					WriteLine(LogLevel.ERROR, "Repeating {0}, count={1}/{2}: {3}", Name, count, maxCount, ex);
					break;
				}
			}
            //if (Name.Contains("CardVehicleRecords")) DDDReader.Program.mainForm.updTxt("CardVehicleRecords end");//MessageBox.Show("CardVehicleRecords end");
		}

		public override string ToString()
		{
			return string.Format("<< end {0}", Name);
		}
	}

	public class RepeatingRegion : CollectionRegion
	{
		[XmlAttribute]
		public uint Count;

		[XmlAttribute]
		public string CountRef;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			if ( Count == 0 && CountRef != null )
			{
				string refName=CountRef.Substring(1);
				if (globalValues.Contains(refName))
				{
					Count=uint.Parse((string) globalValues[refName]);
				} else
				{
					WriteLine(LogLevel.WARN, "RepeatingRegion {0} doesn't contain ref {1}", Name, refName);
				}
			}
			ProcessItems(reader, writer, Count);
		}
	}

	public enum Activity
	{
		Break,
		Available,
		Work,
		Driving
	}

	// This is the activity change class. It has own class because the fields
	// are packed into two bytes which we need to unpack (see page 55).
	public class ActivityChangeRegion : Region
	{
		byte slot;
		byte status;
		bool inserted;
		Activity activity;
		uint time;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			// format: scpaattt tttttttt (16 bits)
			// s = slot, c = crew status, p = card inserted, a = activity, t = time
			byte b1=reader.ReadByte();
			byte b2=reader.ReadByte();

			slot     = (byte)     ((b1 >> 7) & 0x01);      // 7th bit
			status   = (byte)     ((b1 >> 6) & 0x01);      // 6th bit
			inserted =            ((b1 >> 5) & 0x01) == 0; // 5th bit
			activity = (Activity) ((b1 >> 3) & 0x03);      // 4th and 3rd bits
			time     = (((uint)b1 & 0x07) << 8) | b2;      // 0th, 1st, 2nd bits from b1

			if ( this.LogLevel == LogLevel.DEBUG || this.LogLevel == LogLevel.INFO )
			{
				long position=reader.BaseStream.Position;
				if ( reader.BaseStream is CyclicStream )
					position=((CyclicStream) reader.BaseStream).ActualPosition;

                if (DDDReader.Program.mainForm.createXML) writer.WriteAttributeString("FileOffset", string.Format("0x{0:X4}", position));
			}

            if (DDDReader.Program.mainForm.createXML)
            {
                writer.WriteAttributeString("Slot", slot.ToString());
                writer.WriteAttributeString("Status", status.ToString());
                writer.WriteAttributeString("Inserted", inserted.ToString());
                writer.WriteAttributeString("Activity", activity.ToString());
                writer.WriteAttributeString("Time", string.Format("{0:d2}:{1:d2}", time / 60, time % 60));
            }
            /*190606DDDReader.Program.mainForm.updateTxt("Slot:" + slot.ToString() + " ");
            DDDReader.Program.mainForm.updateTxt("Status:" + status.ToString() + " ");
            DDDReader.Program.mainForm.updateTxt("Inserted:" + inserted.ToString() + " ");
            DDDReader.Program.mainForm.updateTxt("Activity:" + activity.ToString() + " ");
            DDDReader.Program.mainForm.updateTxt("Time:" + string.Format("{0:d2}:{1:d2}", time / 60, time % 60) + "\r\n");*/

            //190604 - be careful, this check will not handle current day  !!!!
            bool saveToDBTime = false;
            if ((DDDReader.Program.mainForm.recordMaxTimeHM < time) || (DDDReader.Program.mainForm.recordMaxTimeHM == 0))
            {
                
                DDDReader.Program.mainForm.recordMaxTimeHM = time;
                string temp = string.Format("{0:d2}:{1:d2}", time / 60, time % 60);
                saveToDBTime = true;
            }
            
            if (DDDReader.Program.mainForm.saveDayToDB && saveToDBTime)//190604
            {
                string uri = "http://localhost/tacho/uploadDriverActivityRecord.php?recordID=" + DDDReader.Program.mainForm.recordID +
                    "&inserted=" + inserted.ToString() + "&activity=" + activity.ToString() + "&time=" + string.Format("{0:d2}:{1:d2}", time / 60, time % 60) +
                    "&slot=" + slot.ToString() + "&status=" + status.ToString();
                HttpWebRequest httpRequest = (HttpWebRequest)HttpWebRequest.Create(uri);
                HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                Encoding enc = System.Text.Encoding.GetEncoding(1252);
                StreamReader loResponseStream = new StreamReader(httpResponse.GetResponseStream(), enc);
                string Response = loResponseStream.ReadToEnd();
                //MessageBox.Show("recordactivity: "+Response);
                loResponseStream.Close();
                httpResponse.Close();

                driverActivity act = new driverActivity();
                act.activity = activity.ToString();
                act.time = time;// string.Format("{0:d2}:{1:d2}", time / 60, time % 60); // 190604 - store time as dt , convert to string when saving to db
                DDDReader.Program.mainForm.addDriverActivity(act);
                if (DDDReader.Program.mainForm.recordMaxTimeHM < time) DDDReader.Program.mainForm.recordMaxTimeHM = time;//190604 update min/max 
                if (DDDReader.Program.mainForm.recordMinTimeHM > time) DDDReader.Program.mainForm.recordMinTimeHM = time;
                //DDDReader.Program.mainForm.updateTxt("++++++++++++++\r\n");
               
                Application.DoEvents();
            }
           
            ///////////////////////////

            /*if (DDDReader.Program.mainForm.dailyPresenceCounter == 206)
            {
                DDDReader.Program.mainForm.updateTxt("------start206-------");
            }
            if (DDDReader.Program.mainForm.dailyPresenceCounter == 207)
            {
                DDDReader.Program.mainForm.updateTxt("------start207-------");
            }*/
		}

		public override string ToString()
		{
			return string.Format("slot={0}, status={1}, inserted={2}, activity={3}, time={4:d2}:{5:d2}",
				slot, status, inserted, activity, time / 60, time % 60);
		}

	}
}

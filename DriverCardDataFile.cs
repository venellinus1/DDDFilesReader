using System;
using System.IO;
using System.Reflection;
using System.Xml;
using DataFileReader;

using System.Windows.Forms;

namespace DataFileReader
//namespace DDDReader
{
	/// <summary>
	/// Wrapper for DataFile that initialises with driver card config
	/// </summary>
	public class DriverCardDataFile : DataFile
	{
		public static DataFile Create()
		{
            /*
             IMPORTANT: 
             -resource build action should be embedded resource - right click in solution explorer to set it...
             -resource should be added in project properties
             -resource type should be binary
             */
			// construct using embedded config
			//Assembly a = typeof(DriverCardDataFile).GetTypeInfo().Assembly;
			//string name = a.FullName.Split(',')[0]+".DriverCardData.config";
            var a = Assembly.GetExecutingAssembly();
            
            //var resourceName = "DriverCardData.config";
            string name = "DDDReader.bin.Debug.DriverCardData.config";

            //string name = a.FullName.Split(',')[0]+".DriverCardData.config";
            //string name = "DriverCardData1.config";
            //MessageBox.Show(a.GetManifestResourceStream(name) + " : " + a.FullName.Split(',')[0] + ".DriverCardData.config");
			Stream stm = a.GetManifestResourceStream(name);
			XmlReader xtr = XmlReader.Create(stm);

			return Create(xtr);
		}
	}
}

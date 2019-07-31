using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using DataFileReader;

namespace DataFileReader
//namespace DDDReader
{
	/// <summary>
	/// This is the main data file handler. It can contain any number of sub-regions that
	/// are marked with a 'magic' number, as defined on page 160 of the specification.
	/// </summary>
	public class VehicleUnitDataFile : DataFile
	{
		public static DataFile Create()
		{
			// construct using embedded config
			//Assembly a = typeof(VehicleUnitDataFile).GetTypeInfo().Assembly;
			//string name = a.FullName.Split(',')[0]+".VehicleUnitData.config";
            var a = Assembly.GetExecutingAssembly();
            string name = "ConsoleApplication1.bin.Debug.VehicleUnitData.config";//ConsoleApplication1.
            var files = Assembly.GetExecutingAssembly().GetManifestResourceNames();
			Stream stm = a.GetManifestResourceStream(name);
			XmlReader xtr = XmlReader.Create(stm);

			return Create(xtr);
		}
	}

}

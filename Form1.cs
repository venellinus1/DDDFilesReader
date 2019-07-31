/**********************************************************
desktop app implementation of jugglingcats ddd reader library
https://github.com/jugglingcats/tachograph-reader

modified to work with wialon ddd files.
Changes include :
 * not using NameString, instead modifying SimpleString to contain Skip attribute then using SimpleString where NameString was used
 the skipped value is the codepage of the namestring, which for the moment can be hardcoded for the sake of MVP target
 * that required adding Skip=0 in DriverCardData for all current SimpleString entries so that they continue working as before
***********************************************************/


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;


using System.IO;
using System.Xml;
using DataFileReader;
using System.Net.NetworkInformation;

using System.Web.Script.Serialization;
using System.Web;
using System.Globalization;
using System.Net;


namespace DDDReader
{
    public partial class Form1 : Form
    {
        //DataFile hex;
        public Form1()
        {
            InitializeComponent();
            
            //hex = new DataFile(this);
        }
        string data = "";
        public string wialonurlfix = "http://hst-api.wialon.com/wialon/";
        public Login wialonLogin ;
        public List<driverActivity> driversActivity = new List<driverActivity>();
        public int recordID;
        public string minTime;
        public string maxTime;
        public string driverID;
        public string odometerBegin;
        public string odometerEnd;
        public DateTime vehicleFirstUse;
        public DateTime vehicleLastUse;
        public string vehiclePlateNum;
        public int done;
        public string cardHolderFName;
        public string cardHolderLName;
        public string cardHolderCardN;
        public bool createXML;

       public string recordDateGeneral;
        public uint distance;
        public uint dailyPresenceCounter;
        public uint recordMinTimeHM;//db record min time  for given driver id, to help with avoiding entries overlapping
        public uint recordMaxTimeHM;//db record max time  for given driver id
        public DateTime recordMinDate;//db record max time  for given driver id
        public DateTime recordMaxDate;//db record max time  for given driver id
        public bool saveDayToDB ;//store state for each day if should be written, additionally will check for each record activity if should be written
        public string currentFile;
        public bool readyForNextFile;
        public List<String> dddFileNames = new List<string>();
        public int fileNameCounter;

        public void clearDrivers() {
            driversActivity.Clear();
        }
        public void addDriverActivity(driverActivity act)
        {
            driversActivity.Add(act);
        }
        public void actStartEnd(driverActivityMain drvActMain) {//datafile line 325
            //MessageBox.Show(driversActivity[0].time + " : " + driversActivity[driversActivity.Count-1].time);
            textBox1.Text += "actStartEnd: " + driversActivity[0].time + " : " + driversActivity[driversActivity.Count - 1].time + " : " +
                drvActMain.date + " : " + drvActMain.dailypresence + "\r\n";
        }

        public void updateTxt(string str) {
            //textBox1.Text += str + "\r\n";
            //textBox1.Select(textBox1.Text.Length-1,0);
            data += str + "\r\n";
        }
        public void loginUpdate(string str) {
            textBox2.Text += str + "\r\n";
            textBox2.SelectionStart = textBox2.Text.Length;
            textBox2.ScrollToCaret();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            String param1 = "C_DF000015148000_V_Gerber_201905290548.ddd";// "C_DF000001601780_H_Becker_201805220154.ddd";// "C_DF000015148000_V_Gerber_201701110333.ddd";//"C_DF000015148000_V_Gerber_201701130420.ddd";//  "filev.ddd"
            String param2 = "testv2.xml";//"C_DF000001601780_H_Becker_201805220154.xml";// "201701110333.xml";// "201701130420.xml";//  "testV1.xml"
            DataFile proc = null;
            proc = DriverCardDataFile.Create();
            proc.LogLevel = LogLevel.DEBUG;
            var xtw = new XmlTextWriter(param2, Encoding.UTF8);
            try
            {
                xtw.Formatting = Formatting.Indented;
                proc.Process(param1, xtw);
            }
            finally
            {
                xtw.Close();
            }
        }

        private void processDDDFile(String param1)
        {
            //String param1 = "filev.ddd";
            String param2 = "testV1.xml";
            DataFile proc = null;
            proc = DriverCardDataFile.Create();
            proc.LogLevel = LogLevel.DEBUG;
            var xtw = new XmlTextWriter(param2, Encoding.UTF8);
            try
            {
                xtw.Formatting = Formatting.Indented;
                proc.Process(param1, xtw);
            }
            finally
            {
                xtw.Close();
            }
        }

        public void updTxt(string test) {
            textBox1.Text += test+"\r\n";
        }
        private void button2_Click(object sender, EventArgs e)
        {
            textBox1.Text += data;
        }

        
        private void btnLogin_Click(object sender, EventArgs e)
        {
            /*
            public string userid = "";
            public string debugData = "";//attach to textbox1.text - check caret ...
            
            
            string appname = "DDDReader";
            bool startstop;
           
            public string txtToken = "";
            public string wialonurlfix = "";
            public string eID = "";
             */
            wialonLogin = new Login();
            wialonLogin.chkUseToken = chkUseToken.Checked;
            wialonLogin.txtToken = txtLoginToken.Text;
            wialonLogin.wialonuser = "";
            wialonLogin.wialonpass = "";
            wialonLogin.DoLoginToken();
            //DDDReader.Program.mainForm.WialonGetAvailableUnits(this.eID); 190728 - moved from wialonclasses - dologintoken in here
            WialonGetAvailableUnits(wialonLogin.eID);
            
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //textBox2.Text = wialonLogin.eID;//wialonLogin.sessionToken;
            //WialonGetAvailableUnits(wialonLogin.eID);
            unitsScan();
        }

        /*190729
         called by WialonGetAvailableUnits
         input: result row - d.flds value which is dict
         output: string with the value of the specified custom field name , currently Gruppe field
         this is saved in tag for each treeview vehicle together with vehicle id. format is: vehicle_id;customfieldvalue
         */
        private string readCustomFieldsForVehicle(Dictionary<string, object> temprowd){
            
            //Dictionary<string, object> rowd = (Dictionary<string, object>)temprow.d;
            //if (string.Compare(temprow.i, "19414015" )==0){
            string result = "";
            //foreach (KeyValuePair<string, object> paird in temprowd)
            {
                //if ((string.Compare(paird.Key, "flds") == 0))
                {
                    Dictionary<string, object> rowflds = (Dictionary<string, object>)temprowd;
                    //if (rowflds.Count > 2)//Gruppe
                    {
                        foreach (KeyValuePair<string, object> fldsobj in rowflds)
                        {//1 , 2 , 3...
                            customFld tmpp = new customFld();

                            foreach (KeyValuePair<string, object> fldsobjsub in (Dictionary<string, object>)fldsobj.Value)
                            {
                                if (string.Compare(fldsobjsub.Key, "v") == 0)
                                {
                                    tmpp.v = fldsobjsub.Value.ToString();
                                }

                                if (string.Compare(fldsobjsub.Key, "n") == 0)
                                {
                                    tmpp.n = fldsobjsub.Value.ToString();
                                }
                            }

                            string testt = "";
                            //customFld tmpp = (customFld)fldsobj.Value;                            
                            //if (string.Compare(tmpp.n, "DRIVERGROUP") == 0)//DRIVERGROUP GRUPPE
                            if (tmpp.n.Contains("DRIVERGROUP"))//DRIVERGROUP GRUPPE
                            {
                                result = tmpp.v;
                                //MessageBox.Show(tmpp.n + " : " + tmpp.v);
                            } else {
                                testt += tmpp.n + " ";
                            }
                        }

                        //string tmp = paird.Value.ToString();
                        string test1 = "";
                    }
                    //break;
                }
            }
            return result;
        }
        
        
        public List<availableUnits> WialonGetAvailableUnits(string sid)
        {//getting objects
            //http://docs.gurtam.com/en/hosting/sdk/webapi/general/flags?s[]=flags
            //details on all flags about how to extract different data for units
            //http://docs.gurtam.com/en/hosting/sdk/webapi/examples/units?s[]=svc&s[]=core&s[]=update&s[]=data&s[]=flags

            textBox2.Text = "";
            string urlGetAUs =
               wialonurlfix + "/ajax.html?svc=core/update_data_flags&sid=" + wialonLogin.eID +
               "&params={" +
                   "\"spec\":[" +
                   "{" +
                       "\"type\":\"type\"," +
                       "\"data\":\"avl_unit\"," +//use avl_unit
                       "\"flags\":129," +//190729 modified the flag from 1 to 9 (8 + 1), flag 8 provides data for custom fields
                /* 1 - get common information(NAMEs) ; 
                 * 256-phone+unique ID ; 
                 * 8192 - current value for km/hours/KB;
                 * 131072 - trip detection details
                 */
                       "\"mode\":0" +                /* set flags */
                   "}" +
                   "]" +
               "}";
            //availableUnits
            WebClient wc = new WebClient();
            wc.Encoding = Encoding.UTF8;
            string result = (wc.DownloadString(urlGetAUs));
            //txtParams.Text = result + "\r\n\r\n\r\n\r\n";
            JavaScriptSerializer ser = new JavaScriptSerializer();
            List<availableUnits> items = ser.Deserialize<List<availableUnits>>(result);
            //MessageBox.Show(items[0].f);

            foreach (availableUnits unit in items)
            {
                
                //textBox2.Text += unit.i + " " + unit.d.cls + " " + unit.d.id + " " + unit.d.nm + " " + unit.d.uacl + "\r\n";
                TreeNode node = new TreeNode(); 
                node.Text = unit.d.nm;
                node.Tag = unit.d.id + ";" + readCustomFieldsForVehicle((Dictionary<string, object>)unit.d.aflds);
                string tempp = readCustomFieldsForVehicle((Dictionary<string, object>)unit.d.aflds);
                
                if (tempp.Length > 0) { 
                    string tempp1 = ""; //FOR REMOVAL - used for debugging....
                }
                treeViewUnits.Nodes.Add(node);
            }
            //output when using flag 9
            //"[{\"i\":11877080,\"d\":{\"nm\":\"MCC2453\",\"cls\":2,\"id\":11877080,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12201684,\"d\":{\"nm\":\"MCC8231\",\"cls\":2,\"id\":12201684,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12201688,\"d\":{\"nm\":\"MCC2275\",\"cls\":2,\"id\":12201688,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12201692,\"d\":{\"nm\":\"MCC2270\",\"cls\":2,\"id\":12201692,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12319401,\"d\":{\"nm\":\"MCC2051\",\"cls\":2,\"id\":12319401,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"MCC2264\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12400845,\"d\":{\"nm\":\"MCC2380\",\"cls\":2,\"id\":12400845,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"MCC2402\"},\"2\":{\"id\":2,\"n\":\"Mercedes\",\"v\":\"Actros\"},\"3\":{\"id\":3,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12401054,\"d\":{\"nm\":\"MCC2197\",\"cls\":2,\"id\":12401054,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12401055,\"d\":{\"nm\":\"MCC2389\",\"cls\":2,\"id\":12401055,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12401060,\"d\":{\"nm\":\"MCC2308\",\"cls\":2,\"id\":12401060,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12410535,\"d\":{\"nm\":\"MCC2189\",\"cls\":2,\"id\":12410535,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12453675,\"d\":{\"nm\":\"MCC8761\",\"cls\":2,\"id\":12453675,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12453680,\"d\":{\"nm\":\"MCC8762\",\"cls\":2,\"id\":12453680,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12488262,\"d\":{\"nm\":\"MCC2521\",\"cls\":2,\"id\":12488262,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12488263,\"d\":{\"nm\":\"MCC2084\",\"cls\":2,\"id\":12488263,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12488266,\"d\":{\"nm\":\"MCC2201\",\"cls\":2,\"id\":12488266,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12503290,\"d\":{\"nm\":\"MCC2510\",\"cls\":2,\"id\":12503290,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2510\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12503292,\"d\":{\"nm\":\"MCC2520\",\"cls\":2,\"id\":12503292,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12503295,\"d\":{\"nm\":\"MCC2073\",\"cls\":2,\"id\":12503295,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12503299,\"d\":{\"nm\":\"MCC2196\",\"cls\":2,\"id\":12503299,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12503303,\"d\":{\"nm\":\"MCC8008\",\"cls\":2,\"id\":12503303,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2131\"},\"2\":{\"id\":2,\"n\":\"ex\",\"v\":\"MCC8630\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12503313,\"d\":{\"nm\":\"MCC2377\",\"cls\":2,\"id\":12503313,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12522927,\"d\":{\"nm\":\"MCC2514\",\"cls\":2,\"id\":12522927,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12522942,\"d\":{\"nm\":\"MCC2522\",\"cls\":2,\"id\":12522942,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12522945,\"d\":{\"nm\":\"MCC2061 alt\",\"cls\":2,\"id\":12522945,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"MCC2020\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12522953,\"d\":{\"nm\":\"MCC2511\",\"cls\":2,\"id\":12522953,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"vormals MCC8146\",\"v\":\"jetzt MCC2511\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12522955,\"d\":{\"nm\":\"MCC2133\",\"cls\":2,\"id\":12522955,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12522972,\"d\":{\"nm\":\"MCC2276\",\"cls\":2,\"id\":12522972,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12522976,\"d\":{\"nm\":\"MCC2502\",\"cls\":2,\"id\":12522976,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12522991,\"d\":{\"nm\":\"MCC2281\",\"cls\":2,\"id\":12522991,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12522993,\"d\":{\"nm\":\"MCC2501\",\"cls\":2,\"id\":12522993,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12523004,\"d\":{\"nm\":\"MCC8144\",\"cls\":2,\"id\":12523004,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12523032,\"d\":{\"nm\":\"MCC2504\",\"cls\":2,\"id\":12523032,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12523041,\"d\":{\"nm\":\"MCC2500\",\"cls\":2,\"id\":12523041,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533926,\"d\":{\"nm\":\"MCC2449\",\"cls\":2,\"id\":12533926,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533931,\"d\":{\"nm\":\"MCC2005\",\"cls\":2,\"id\":12533931,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533939,\"d\":{\"nm\":\"MCC2176\",\"cls\":2,\"id\":12533939,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533940,\"d\":{\"nm\":\"MCC2732\",\"cls\":2,\"id\":12533940,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex\",\"v\":\"MCC2702\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533941,\"d\":{\"nm\":\"MCC2733\",\"cls\":2,\"id\":12533941,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2703\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533943,\"d\":{\"nm\":\"MCC2731\",\"cls\":2,\"id\":12533943,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533951,\"d\":{\"nm\":\"MCC2730\",\"cls\":2,\"id\":12533951,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"EX\",\"v\":\"MCC2593 umbenannt am 13.10.15\"},\"2\":{\"id\":2,\"n\":\"EX\",\"v\":\"MCC2700 umbenannt am 14.02.2018\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533975,\"d\":{\"nm\":\"MCC2175\",\"cls\":2,\"id\":12533975,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex Gerät\",\"v\":\"MCC2266\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533976,\"d\":{\"nm\":\"MCC8055 (ausgebaut)\",\"cls\":2,\"id\":12533976,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2043\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533977,\"d\":{\"nm\":\"MCC2115\",\"cls\":2,\"id\":12533977,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533978,\"d\":{\"nm\":\"MCC2139\",\"cls\":2,\"id\":12533978,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533979,\"d\":{\"nm\":\"MCC2488\",\"cls\":2,\"id\":12533979,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533982,\"d\":{\"nm\":\"MCC2116\",\"cls\":2,\"id\":12533982,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533989,\"d\":{\"nm\":\"MCC2117\",\"cls\":2,\"id\":12533989,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12533997,\"d\":{\"nm\":\"MCC2137\",\"cls\":2,\"id\":12533997,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12534000,\"d\":{\"nm\":\"MCC2140\",\"cls\":2,\"id\":12534000,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12543538,\"d\":{\"nm\":\"MCC2611\",\"cls\":2,\"id\":12543538,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12543584,\"d\":{\"nm\":\"MCC2607\",\"cls\":2,\"id\":12543584,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2409\"},\"2\":{\"id\":2,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12543590,\"d\":{\"nm\":\"MCC2606\",\"cls\":2,\"id\":12543590,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2410\"},\"2\":{\"id\":2,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12543593,\"d\":{\"nm\":\"MCC2604\",\"cls\":2,\"id\":12543593,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex.\",\"v\":\"MCC2411\"},\"2\":{\"id\":2,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12543601,\"d\":{\"nm\":\"MCC2605\",\"cls\":2,\"id\":12543601,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2407\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12543615,\"d\":{\"nm\":\"MCC2610\",\"cls\":2,\"id\":12543615,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2408\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594200,\"d\":{\"nm\":\"MCC2505\",\"cls\":2,\"id\":12594200,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594221,\"d\":{\"nm\":\"MCC2508\",\"cls\":2,\"id\":12594221,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594226,\"d\":{\"nm\":\"MCC2509\",\"cls\":2,\"id\":12594226,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594228,\"d\":{\"nm\":\"MCC2463\",\"cls\":2,\"id\":12594228,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594230,\"d\":{\"nm\":\"MCC2130\",\"cls\":2,\"id\":12594230,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594276,\"d\":{\"nm\":\"MCC2241\",\"cls\":2,\"id\":12594276,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594290,\"d\":{\"nm\":\"MCC2242\",\"cls\":2,\"id\":12594290,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594303,\"d\":{\"nm\":\"MCC2243\",\"cls\":2,\"id\":12594303,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594322,\"d\":{\"nm\":\"MCC2368\",\"cls\":2,\"id\":12594322,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594333,\"d\":{\"nm\":\"MCC2375\",\"cls\":2,\"id\":12594333,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594348,\"d\":{\"nm\":\"MCC2712\",\"cls\":2,\"id\":12594348,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex Gerät getauscht\",\"v\":\"MCC2112\"},\"2\":{\"id\":2,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594367,\"d\":{\"nm\":\"MCC2609\",\"cls\":2,\"id\":12594367,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594380,\"d\":{\"nm\":\"MCC2152\",\"cls\":2,\"id\":12594380,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12594412,\"d\":{\"nm\":\"MCC2219\",\"cls\":2,\"id\":12594412,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12633762,\"d\":{\"nm\":\"MCC2706\",\"cls\":2,\"id\":12633762,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12633763,\"d\":{\"nm\":\"MCC2218\",\"cls\":2,\"id\":12633763,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12702829,\"d\":{\"nm\":\"MCC8041\",\"cls\":2,\"id\":12702829,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12828208,\"d\":{\"nm\":\"MCC5073\",\"cls\":2,\"id\":12828208,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12828212,\"d\":{\"nm\":\"MCC2447\",\"cls\":2,\"id\":12828212,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2113\"},\"2\":{\"id\":2,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12864496,\"d\":{\"nm\":\"MCC2705\",\"cls\":2,\"id\":12864496,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12873773,\"d\":{\"nm\":\"MCC2174\",\"cls\":2,\"id\":12873773,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex\",\"v\":\"MCC2461\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12873783,\"d\":{\"nm\":\"MCC2328\",\"cls\":2,\"id\":12873783,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":12970880,\"d\":{\"nm\":\"MCC2490\",\"cls\":2,\"id\":12970880,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13056095,\"d\":{\"nm\":\"MCC8050\",\"cls\":2,\"id\":13056095,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13056096,\"d\":{\"nm\":\"MCC8039\",\"cls\":2,\"id\":13056096,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13056097,\"d\":{\"nm\":\"MCC8040\",\"cls\":2,\"id\":13056097,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13056098,\"d\":{\"nm\":\"MCC8895 \\/ 678\",\"cls\":2,\"id\":13056098,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13056100,\"d\":{\"nm\":\"MCC8545\",\"cls\":2,\"id\":13056100,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13056102,\"d\":{\"nm\":\"MCC8230\",\"cls\":2,\"id\":13056102,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC8631\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13056126,\"d\":{\"nm\":\"MCC2343\",\"cls\":2,\"id\":13056126,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2227\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13056129,\"d\":{\"nm\":\"MCC2344\",\"cls\":2,\"id\":13056129,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2228\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13056130,\"d\":{\"nm\":\"MCC2345\",\"cls\":2,\"id\":13056130,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"MCC2229\"},\"2\":{\"id\":2,\"n\":\"ex\",\"v\":\"MCC2226\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13256294,\"d\":{\"nm\":\"MCC2149\",\"cls\":2,\"id\":13256294,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"MCC2027\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13526175,\"d\":{\"nm\":\"MCC2608\",\"cls\":2,\"id\":13526175,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Gruppe\",\"v\":\"UTSGO\"},\"2\":{\"id\":2,\"n\":\"ex.\",\"v\":\"MCC2394\"},\"3\":{\"id\":3,\"n\":\"exex\",\"v\":\"MCC2406\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13568523,\"d\":{\"nm\":\"MCC2268\",\"cls\":2,\"id\":13568523,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13568527,\"d\":{\"nm\":\"MCC2716\",\"cls\":2,\"id\":13568527,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13568537,\"d\":{\"nm\":\"MCC2717\",\"cls\":2,\"id\":13568537,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13568538,\"d\":{\"nm\":\"MCC2718\",\"cls\":2,\"id\":13568538,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13589166,\"d\":{\"nm\":\"MCC2550\",\"cls\":2,\"id\":13589166,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13589171,\"d\":{\"nm\":\"MCC2160\",\"cls\":2,\"id\":13589171,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13619400,\"d\":{\"nm\":\"MCC8591\",\"cls\":2,\"id\":13619400,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC8054\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13631271,\"d\":{\"nm\":\"MCC2150\",\"cls\":2,\"id\":13631271,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13631274,\"d\":{\"nm\":\"MCC2153\",\"cls\":2,\"id\":13631274,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13631281,\"d\":{\"nm\":\"MCC2154\",\"cls\":2,\"id\":13631281,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13639293,\"d\":{\"nm\":\"MCC2194\",\"cls\":2,\"id\":13639293,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"IMEI\",\"v\":\"356173065609237\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13639299,\"d\":{\"nm\":\"MCC2338\",\"cls\":2,\"id\":13639299,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"IMEI\",\"v\":\"356173065545969\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13666399,\"d\":{\"nm\":\"MCC2141\",\"cls\":2,\"id\":13666399,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13666400,\"d\":{\"nm\":\"MCC2142\",\"cls\":2,\"id\":13666400,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13794736,\"d\":{\"nm\":\"MCC2052\",\"cls\":2,\"id\":13794736,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13858039,\"d\":{\"nm\":\"MCC2054\",\"cls\":2,\"id\":13858039,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13870229,\"d\":{\"nm\":\"MCC2257\",\"cls\":2,\"id\":13870229,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13870230,\"d\":{\"nm\":\"MCC2258\",\"cls\":2,\"id\":13870230,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13883281,\"d\":{\"nm\":\"MCC2053\",\"cls\":2,\"id\":13883281,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13914979,\"d\":{\"nm\":\"MCC8164\",\"cls\":2,\"id\":13914979,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13914980,\"d\":{\"nm\":\"MCC8165\",\"cls\":2,\"id\":13914980,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":13915011,\"d\":{\"nm\":\"MCC2383\",\"cls\":2,\"id\":13915011,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":14076102,\"d\":{\"nm\":\"MCC2384\",\"cls\":2,\"id\":14076102,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":14386573,\"d\":{\"nm\":\"MCC2255\",\"cls\":2,\"id\":14386573,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":14701017,\"d\":{\"nm\":\"MCC2715\",\"cls\":2,\"id\":14701017,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":14701018,\"d\":{\"nm\":\"MCC2714\",\"cls\":2,\"id\":14701018,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":14701022,\"d\":{\"nm\":\"MCC2697\",\"cls\":2,\"id\":14701022,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":14701024,\"d\":{\"nm\":\"MCC2709\",\"cls\":2,\"id\":14701024,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":14826229,\"d\":{\"nm\":\"MCC2155\",\"cls\":2,\"id\":14826229,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"3\":{\"id\":3,\"n\":\"ex.\",\"v\":\"MCC2009\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":14826231,\"d\":{\"nm\":\"MCC2016\",\"cls\":2,\"id\":14826231,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"ex. MCC2026\"},\"5\":{\"id\":5,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15078143,\"d\":{\"nm\":\"MCC2326\",\"cls\":2,\"id\":15078143,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15078151,\"d\":{\"nm\":\"MCC8040-neu\",\"cls\":2,\"id\":15078151,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"UTMBA MCC2200\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15078155,\"d\":{\"nm\":\"MCC2498\",\"cls\":2,\"id\":15078155,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"ex UTMBA MCC2259\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15078157,\"d\":{\"nm\":\"MCC8010-neu\",\"cls\":2,\"id\":15078157,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2260 UTMBA\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15103401,\"d\":{\"nm\":\"MCC8591-neu\",\"cls\":2,\"id\":15103401,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2223 UTMBA\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15103403,\"d\":{\"nm\":\"MCC8055-neu\",\"cls\":2,\"id\":15103403,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"ACTROS 2013\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"UTMBA MCC2271\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15103406,\"d\":{\"nm\":\"MCC2061\",\"cls\":2,\"id\":15103406,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"UTMBA MCC2273\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15103409,\"d\":{\"nm\":\"MCC2346\",\"cls\":2,\"id\":15103409,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15150410,\"d\":{\"nm\":\"MCC2190\",\"cls\":2,\"id\":15150410,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15150418,\"d\":{\"nm\":\"MCC2118\",\"cls\":2,\"id\":15150418,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15150421,\"d\":{\"nm\":\"MCC2119\",\"cls\":2,\"id\":15150421,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15150444,\"d\":{\"nm\":\"MCC2191\",\"cls\":2,\"id\":15150444,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15150464,\"d\":{\"nm\":\"MCC2057\",\"cls\":2,\"id\":15150464,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15150472,\"d\":{\"nm\":\"MCC2062\",\"cls\":2,\"id\":15150472,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15150483,\"d\":{\"nm\":\"MCC8008-neu\",\"cls\":2,\"id\":15150483,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Ex\",\"v\":\"MCC2272 UTMBA\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15201513,\"d\":{\"nm\":\"MCC2046\",\"cls\":2,\"id\":15201513,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15201517,\"d\":{\"nm\":\"MCC2348\",\"cls\":2,\"id\":15201517,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15216045,\"d\":{\"nm\":\"MCC2305\",\"cls\":2,\"id\":15216045,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15216047,\"d\":{\"nm\":\"MCC2318\",\"cls\":2,\"id\":15216047,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15216050,\"d\":{\"nm\":\"MCC2322\",\"cls\":2,\"id\":15216050,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15216051,\"d\":{\"nm\":\"MCC2317\",\"cls\":2,\"id\":15216051,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15216060,\"d\":{\"nm\":\"MCC2319\",\"cls\":2,\"id\":15216060,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15234280,\"d\":{\"nm\":\"MCC8153\",\"cls\":2,\"id\":15234280,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15234284,\"d\":{\"nm\":\"MCC2038\",\"cls\":2,\"id\":15234284,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC8143\"},\"4\":{\"id\":4,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15260566,\"d\":{\"nm\":\"MCC2006\",\"cls\":2,\"id\":15260566,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15260567,\"d\":{\"nm\":\"MCC2007\",\"cls\":2,\"id\":15260567,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15260620,\"d\":{\"nm\":\"MCC2102\",\"cls\":2,\"id\":15260620,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO Tacho\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15260623,\"d\":{\"nm\":\"MCC2103\",\"cls\":2,\"id\":15260623,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO Tacho\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15260656,\"d\":{\"nm\":\"MCC2104\",\"cls\":2,\"id\":15260656,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO Tacho\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15260669,\"d\":{\"nm\":\"MCC2262\",\"cls\":2,\"id\":15260669,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO Tacho\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15260715,\"d\":{\"nm\":\"MCC2167\",\"cls\":2,\"id\":15260715,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15260752,\"d\":{\"nm\":\"MCC2177\",\"cls\":2,\"id\":15260752,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15260776,\"d\":{\"nm\":\"MCC2185\",\"cls\":2,\"id\":15260776,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15260817,\"d\":{\"nm\":\"MCC2188\",\"cls\":2,\"id\":15260817,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"ex. UTMBP ex. UTMHH now UTPCZ\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15264149,\"d\":{\"nm\":\"MCC2067\",\"cls\":2,\"id\":15264149,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15264186,\"d\":{\"nm\":\"MCC2075\",\"cls\":2,\"id\":15264186,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15264347,\"d\":{\"nm\":\"MCC2330\",\"cls\":2,\"id\":15264347,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho \"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"ex UTSAU jetzt UTSGO\"},\"4\":{\"id\":4,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15264393,\"d\":{\"nm\":\"MCC2331\",\"cls\":2,\"id\":15264393,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15264441,\"d\":{\"nm\":\"MCC2327\",\"cls\":2,\"id\":15264441,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15264555,\"d\":{\"nm\":\"MCC2360\",\"cls\":2,\"id\":15264555,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MERCEDES\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTSAU\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15264578,\"d\":{\"nm\":\"MCC2335\",\"cls\":2,\"id\":15264578,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15265037,\"d\":{\"nm\":\"MCC2251\",\"cls\":2,\"id\":15265037,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15265103,\"d\":{\"nm\":\"MCC2252\",\"cls\":2,\"id\":15265103,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15265285,\"d\":{\"nm\":\"MCC2614\",\"cls\":2,\"id\":15265285,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MERCEDES\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15265323,\"d\":{\"nm\":\"MCC2615\",\"cls\":2,\"id\":15265323,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MERCEDES\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15265385,\"d\":{\"nm\":\"MCC2616\",\"cls\":2,\"id\":15265385,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MERCEDES\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15292637,\"d\":{\"nm\":\"MCC2106\",\"cls\":2,\"id\":15292637,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15292641,\"d\":{\"nm\":\"MCC2107\",\"cls\":2,\"id\":15292641,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15292644,\"d\":{\"nm\":\"MCC2158\",\"cls\":2,\"id\":15292644,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15292645,\"d\":{\"nm\":\"MCC2178\",\"cls\":2,\"id\":15292645,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15292649,\"d\":{\"nm\":\"MCC2253\",\"cls\":2,\"id\":15292649,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15447072,\"d\":{\"nm\":\"MCC2092\",\"cls\":2,\"id\":15447072,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15447076,\"d\":{\"nm\":\"MCC2093\",\"cls\":2,\"id\":15447076,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15447079,\"d\":{\"nm\":\"MCC2094\",\"cls\":2,\"id\":15447079,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15447083,\"d\":{\"nm\":\"MCC2181\",\"cls\":2,\"id\":15447083,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15447085,\"d\":{\"nm\":\"MCC2182\",\"cls\":2,\"id\":15447085,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15858270,\"d\":{\"nm\":\"MCC2613\",\"cls\":2,\"id\":15858270,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MERCEDES\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"GRUPPE\",\"v\":\"UTSGO\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":15970152,\"d\":{\"nm\":\"MCC2239\",\"cls\":2,\"id\":15970152,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16194457,\"d\":{\"nm\":\"MCC2698\",\"cls\":2,\"id\":16194457,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16291809,\"d\":{\"nm\":\"MCC2011\",\"cls\":2,\"id\":16291809,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16291811,\"d\":{\"nm\":\"MCC2023\",\"cls\":2,\"id\":16291811,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16594378,\"d\":{\"nm\":\"MCC8892 \\/\",\"cls\":2,\"id\":16594378,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16594445,\"d\":{\"nm\":\"MCC2699\",\"cls\":2,\"id\":16594445,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16594528,\"d\":{\"nm\":\"MCC5049\",\"cls\":2,\"id\":16594528,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16594552,\"d\":{\"nm\":\"MCC2232\",\"cls\":2,\"id\":16594552,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16637267,\"d\":{\"nm\":\"MCC2207\",\"cls\":2,\"id\":16637267,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTMHH\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16677536,\"d\":{\"nm\":\"MCC2082\",\"cls\":2,\"id\":16677536,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16677592,\"d\":{\"nm\":\"MCC2374\",\"cls\":2,\"id\":16677592,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16677675,\"d\":{\"nm\":\"MCC2393\",\"cls\":2,\"id\":16677675,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16677692,\"d\":{\"nm\":\"MCC2409\",\"cls\":2,\"id\":16677692,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16677727,\"d\":{\"nm\":\"MCC2424\",\"cls\":2,\"id\":16677727,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16678102,\"d\":{\"nm\":\"MCC2098\",\"cls\":2,\"id\":16678102,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16678223,\"d\":{\"nm\":\"MCC2213\",\"cls\":2,\"id\":16678223,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16678323,\"d\":{\"nm\":\"MCC2217\",\"cls\":2,\"id\":16678323,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB UT 2217\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16678346,\"d\":{\"nm\":\"MCC2237\",\"cls\":2,\"id\":16678346,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16678391,\"d\":{\"nm\":\"MCC2212\",\"cls\":2,\"id\":16678391,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16678433,\"d\":{\"nm\":\"MCC8147\",\"cls\":2,\"id\":16678433,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2464\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16802863,\"d\":{\"nm\":\"MCC2246\",\"cls\":2,\"id\":16802863,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2390\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16802904,\"d\":{\"nm\":\"MCC2411\",\"cls\":2,\"id\":16802904,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16802923,\"d\":{\"nm\":\"MCC2422\",\"cls\":2,\"id\":16802923,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16802948,\"d\":{\"nm\":\"MCC2114\",\"cls\":2,\"id\":16802948,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16802996,\"d\":{\"nm\":\"MCC2215\",\"cls\":2,\"id\":16802996,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB UT 2215\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16803024,\"d\":{\"nm\":\"MCC2534\",\"cls\":2,\"id\":16803024,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"2\":{\"id\":2,\"n\":\"Start\",\"v\":\"03.2018\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16803040,\"d\":{\"nm\":\"MCC2333\",\"cls\":2,\"id\":16803040,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"2\":{\"id\":2,\"n\":\"Start\",\"v\":\"03.2018\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2320\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16803086,\"d\":{\"nm\":\"MCC2321\",\"cls\":2,\"id\":16803086,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"2\":{\"id\":2,\"n\":\"Start\",\"v\":\"03.2018\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16803132,\"d\":{\"nm\":\"MCC2320\",\"cls\":2,\"id\":16803132,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"2\":{\"id\":2,\"n\":\"Start\",\"v\":\"03.2018\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2333\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16826952,\"d\":{\"nm\":\"MCC2721\",\"cls\":2,\"id\":16826952,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16826954,\"d\":{\"nm\":\"MCC2722\",\"cls\":2,\"id\":16826954,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16826957,\"d\":{\"nm\":\"MCC2720\",\"cls\":2,\"id\":16826957,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16866726,\"d\":{\"nm\":\"Sub_862462039194054\",\"cls\":2,\"id\":16866726,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"MCC2531 UTMBA\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16866755,\"d\":{\"nm\":\"MCC8231-neu\",\"cls\":2,\"id\":16866755,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex\",\"v\":\"BA UT 2532 MCC2532\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16963104,\"d\":{\"nm\":\"MCC2538\",\"cls\":2,\"id\":16963104,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"IMEI\",\"v\":\"862462039194799\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":16963304,\"d\":{\"nm\":\"MCC2539\",\"cls\":2,\"id\":16963304,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"IMEI\",\"v\":\"862462039350300\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17025906,\"d\":{\"nm\":\"MCC2537\",\"cls\":2,\"id\":17025906,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"IMEI\",\"v\":\"862462039194799\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17039849,\"d\":{\"nm\":\"MCC2525\",\"cls\":2,\"id\":17039849,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransport Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17040585,\"d\":{\"nm\":\"MCC2481\",\"cls\":2,\"id\":17040585,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17040937,\"d\":{\"nm\":\"MCC2482\",\"cls\":2,\"id\":17040937,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17073192,\"d\":{\"nm\":\"MCC2454\",\"cls\":2,\"id\":17073192,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17073200,\"d\":{\"nm\":\"MCC2456\",\"cls\":2,\"id\":17073200,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17136374,\"d\":{\"nm\":\"MCC8010\",\"cls\":2,\"id\":17136374,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17376801,\"d\":{\"nm\":\"MCC2112\",\"cls\":2,\"id\":17376801,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17376886,\"d\":{\"nm\":\"MCC2113\",\"cls\":2,\"id\":17376886,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17376979,\"d\":{\"nm\":\"MCC2135\",\"cls\":2,\"id\":17376979,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17377028,\"d\":{\"nm\":\"MCC2187\",\"cls\":2,\"id\":17377028,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17377110,\"d\":{\"nm\":\"MCC2220\",\"cls\":2,\"id\":17377110,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17377135,\"d\":{\"nm\":\"MCC2483\",\"cls\":2,\"id\":17377135,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17377155,\"d\":{\"nm\":\"MCC2484\",\"cls\":2,\"id\":17377155,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17377467,\"d\":{\"nm\":\"MCC2548\",\"cls\":2,\"id\":17377467,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17377556,\"d\":{\"nm\":\"MCC2433\",\"cls\":2,\"id\":17377556,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17377723,\"d\":{\"nm\":\"MCC2396\",\"cls\":2,\"id\":17377723,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17377769,\"d\":{\"nm\":\"MCC2406\",\"cls\":2,\"id\":17377769,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"...\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB UT 2406\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17377975,\"d\":{\"nm\":\"MCC2339\",\"cls\":2,\"id\":17377975,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"2\":{\"id\":2,\"n\":\"Start\",\"v\":\"06.2018\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17378017,\"d\":{\"nm\":\"MCC2523\",\"cls\":2,\"id\":17378017,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17378643,\"d\":{\"nm\":\"MCC2723\",\"cls\":2,\"id\":17378643,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17378648,\"d\":{\"nm\":\"MCC2725\",\"cls\":2,\"id\":17378648,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17378649,\"d\":{\"nm\":\"MCC2724\",\"cls\":2,\"id\":17378649,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17624326,\"d\":{\"nm\":\"MCC2485\",\"cls\":2,\"id\":17624326,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"\"},\"5\":{\"id\":5,\"n\":\"Kennzeichen\",\"v\":\"PB UT 2485\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17624332,\"d\":{\"nm\":\"MCC2486\",\"cls\":2,\"id\":17624332,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"\"},\"5\":{\"id\":5,\"n\":\"Kennzeichen\",\"v\":\"PB UT 2486\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17624335,\"d\":{\"nm\":\"MCC2487\",\"cls\":2,\"id\":17624335,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"\"},\"5\":{\"id\":5,\"n\":\"Kennzeichen\",\"v\":\"PB_UT_2487\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17624341,\"d\":{\"nm\":\"MCC2524\",\"cls\":2,\"id\":17624341,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17624519,\"d\":{\"nm\":\"MCC2726\",\"cls\":2,\"id\":17624519,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17624522,\"d\":{\"nm\":\"MCC2727\",\"cls\":2,\"id\":17624522,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2727\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17624523,\"d\":{\"nm\":\"MCC2728\",\"cls\":2,\"id\":17624523,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17687671,\"d\":{\"nm\":\"MCC8545-neu\",\"cls\":2,\"id\":17687671,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"ex\",\"v\":\"MCC2395 BA UT 2395 UTMBA\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"Gruppe\",\"v\":\"UTMHH\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17687952,\"d\":{\"nm\":\"MCC8039-neu\",\"cls\":2,\"id\":17687952,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"2412\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"ex\",\"v\":\"MCC2412 UTMBA\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17719928,\"d\":{\"nm\":\"Sub_868597033297595\",\"cls\":2,\"id\":17719928,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMBA\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"ex\",\"v\":\"MCC2410 UTMBA BA UT 2410\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17720127,\"d\":{\"nm\":\"MCC2032\",\"cls\":2,\"id\":17720127,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"Kennzeichen\",\"v\":\"MCC2032\"},\"5\":{\"id\":5,\"n\":\"ex.\",\"v\":\"UTMBA MCC2397\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17720327,\"d\":{\"nm\":\"MCC2097\",\"cls\":2,\"id\":17720327,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMBA\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"Kennzeichen\",\"v\":\"BA UT 2391\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17720502,\"d\":{\"nm\":\"MCC2359\",\"cls\":2,\"id\":17720502,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTMPB\"},\"4\":{\"id\":4,\"n\":\"Kennzeichen\",\"v\":\"PB-UT 2359\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17721413,\"d\":{\"nm\":\"MCC2291\",\"cls\":2,\"id\":17721413,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTMHH\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17721507,\"d\":{\"nm\":\"MCC2040\",\"cls\":2,\"id\":17721507,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTMPB\"},\"4\":{\"id\":4,\"n\":\"hw\",\"v\":\"Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17929953,\"d\":{\"nm\":\"MCC2342\",\"cls\":2,\"id\":17929953,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"2\":{\"id\":2,\"n\":\"Start\",\"v\":\"08.2018\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17964825,\"d\":{\"nm\":\"MCC8046\",\"cls\":2,\"id\":17964825,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"3\":{\"id\":3,\"n\":\"ex.\",\"v\":\"2530\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17964879,\"d\":{\"nm\":\"MCC2533\",\"cls\":2,\"id\":17964879,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17965363,\"d\":{\"nm\":\"MCC2479\",\"cls\":2,\"id\":17965363,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17965420,\"d\":{\"nm\":\"MCC2480\",\"cls\":2,\"id\":17965420,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17965484,\"d\":{\"nm\":\"MCC2458\",\"cls\":2,\"id\":17965484,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17965546,\"d\":{\"nm\":\"MCC2445\",\"cls\":2,\"id\":17965546,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"mcc2446\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17965652,\"d\":{\"nm\":\"MCC2443\",\"cls\":2,\"id\":17965652,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17965723,\"d\":{\"nm\":\"MCC2459\",\"cls\":2,\"id\":17965723,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":17965884,\"d\":{\"nm\":\"MCC2460\",\"cls\":2,\"id\":17965884,\"mu\":0,\"flds\":{},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18005786,\"d\":{\"nm\":\"MCC2370\",\"cls\":2,\"id\":18005786,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMPB\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18027033,\"d\":{\"nm\":\"MCC2017\",\"cls\":2,\"id\":18027033,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB UT 2017\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"},\"6\":{\"id\":6,\"n\":\"hw\",\"v\":\"Tacho\"},\"7\":{\"id\":7,\"n\":\"alte HW\",\"v\":\"868597033180460\"},\"8\":{\"id\":8,\"n\":\"alte Phone\",\"v\":\"352602140187980\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18089828,\"d\":{\"nm\":\"MCC2446\",\"cls\":2,\"id\":18089828,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"mcc2446\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTEET = Ägypten\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18129550,\"d\":{\"nm\":\"MCC2582\",\"cls\":2,\"id\":18129550,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18129590,\"d\":{\"nm\":\"MCC2583\",\"cls\":2,\"id\":18129590,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18129674,\"d\":{\"nm\":\"MCC2584\",\"cls\":2,\"id\":18129674,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18157194,\"d\":{\"nm\":\"MCC2136\",\"cls\":2,\"id\":18157194,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMPB\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18245652,\"d\":{\"nm\":\"MCC2069\",\"cls\":2,\"id\":18245652,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"4\":{\"id\":4,\"n\":\"vormals\",\"v\":\"MCC2530\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18279119,\"d\":{\"nm\":\"MCC8042\",\"cls\":2,\"id\":18279119,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"hw\",\"v\":\"Tacho\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18279122,\"d\":{\"nm\":\"MCC8172\",\"cls\":2,\"id\":18279122,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"hw\",\"v\":\"Tacho\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18279124,\"d\":{\"nm\":\"MCC8173\",\"cls\":2,\"id\":18279124,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"hw\",\"v\":\"Tacho\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18592734,\"d\":{\"nm\":\"MCC8006\",\"cls\":2,\"id\":18592734,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18592744,\"d\":{\"nm\":\"MCC8850\",\"cls\":2,\"id\":18592744,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18592785,\"d\":{\"nm\":\"MCC8852\",\"cls\":2,\"id\":18592785,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669922,\"d\":{\"nm\":\"MCC2535\",\"cls\":2,\"id\":18669922,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"},\"5\":{\"id\":5,\"n\":\"IMEI\",\"v\":\"356173067219720\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669932,\"d\":{\"nm\":\"MCC2563\",\"cls\":2,\"id\":18669932,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067492327\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669938,\"d\":{\"nm\":\"MCC2564\",\"cls\":2,\"id\":18669938,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067167838\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669946,\"d\":{\"nm\":\"MCC2566\",\"cls\":2,\"id\":18669946,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067167846\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669948,\"d\":{\"nm\":\"MCC2574\",\"cls\":2,\"id\":18669948,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067492368\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669950,\"d\":{\"nm\":\"MCC2575\",\"cls\":2,\"id\":18669950,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067166715\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669952,\"d\":{\"nm\":\"MCC2577\",\"cls\":2,\"id\":18669952,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067480652\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669955,\"d\":{\"nm\":\"MCC2578\",\"cls\":2,\"id\":18669955,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067493531\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669958,\"d\":{\"nm\":\"MCC2579\",\"cls\":2,\"id\":18669958,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067168760\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669962,\"d\":{\"nm\":\"MCC2580\",\"cls\":2,\"id\":18669962,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067491261\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669965,\"d\":{\"nm\":\"MCC2581\",\"cls\":2,\"id\":18669965,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067197116\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669966,\"d\":{\"nm\":\"MCC2587\",\"cls\":2,\"id\":18669966,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067493333\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669968,\"d\":{\"nm\":\"MCC2617\",\"cls\":2,\"id\":18669968,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067169206\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669969,\"d\":{\"nm\":\"MCC2619\",\"cls\":2,\"id\":18669969,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067219134\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669971,\"d\":{\"nm\":\"MCC2620\",\"cls\":2,\"id\":18669971,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067493655\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669973,\"d\":{\"nm\":\"MCC2627\",\"cls\":2,\"id\":18669973,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067492681\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669978,\"d\":{\"nm\":\"MCC2707\",\"cls\":2,\"id\":18669978,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067198130\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669982,\"d\":{\"nm\":\"MCC2729\",\"cls\":2,\"id\":18669982,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067496310\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669986,\"d\":{\"nm\":\"MCC2735\",\"cls\":2,\"id\":18669986,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067496088\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669989,\"d\":{\"nm\":\"MCC2736\",\"cls\":2,\"id\":18669989,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173067222153\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18669993,\"d\":{\"nm\":\"MCC2737\",\"cls\":2,\"id\":18669993,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"no Tacho\"},\"4\":{\"id\":4,\"n\":\"IMEI\",\"v\":\"356173064621639\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18740152,\"d\":{\"nm\":\"MCC8152\",\"cls\":2,\"id\":18740152,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"hw\",\"v\":\"Tacho\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18740209,\"d\":{\"nm\":\"MCC2226\",\"cls\":2,\"id\":18740209,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"MCC2226\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18740212,\"d\":{\"nm\":\"MCC2227\",\"cls\":2,\"id\":18740212,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"MCC2226\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18775821,\"d\":{\"nm\":\"MCC2634\",\"cls\":2,\"id\":18775821,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTPCZ\"},\"3\":{\"id\":3,\"n\":\"hw\",\"v\":\"Tacho\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18806520,\"d\":{\"nm\":\"MCC2636\",\"cls\":2,\"id\":18806520,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"WMA13XZZ7KM820497\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTPCZ\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18980190,\"d\":{\"nm\":\"MCC2551\",\"cls\":2,\"id\":18980190,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\".\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTPCZ\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18980198,\"d\":{\"nm\":\"MCC2493\",\"cls\":2,\"id\":18980198,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\".\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTPCZ\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":18991522,\"d\":{\"nm\":\"MCC2442\",\"cls\":2,\"id\":18991522,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\".\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTPCZ\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19028160,\"d\":{\"nm\":\"MCC2228\",\"cls\":2,\"id\":19028160,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\".\"},\"2\":{\"id\":2,\"n\":\"hw\",\"v\":\"Tacho\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19045765,\"d\":{\"nm\":\"MCC8045\",\"cls\":2,\"id\":19045765,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19045767,\"d\":{\"nm\":\"MCC8043\",\"cls\":2,\"id\":19045767,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19045770,\"d\":{\"nm\":\"MCC8044\",\"cls\":2,\"id\":19045770,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Ex\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19197554,\"d\":{\"nm\":\"MCC2065\",\"cls\":2,\"id\":19197554,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2065\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19197636,\"d\":{\"nm\":\"MCC2585\",\"cls\":2,\"id\":19197636,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"2\":{\"id\":2,\"n\":\"hw\",\"v\":\"Tacho\"},\"3\":{\"id\":3,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19337859,\"d\":{\"nm\":\"MCC2024\",\"cls\":2,\"id\":19337859,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2024\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19337880,\"d\":{\"nm\":\"MCC2028\",\"cls\":2,\"id\":19337880,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2028\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19337921,\"d\":{\"nm\":\"MCC2030\",\"cls\":2,\"id\":19337921,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2030\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19337949,\"d\":{\"nm\":\"MCC2034\",\"cls\":2,\"id\":19337949,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2034\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19337967,\"d\":{\"nm\":\"MCC2042\",\"cls\":2,\"id\":19337967,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2042\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19338099,\"d\":{\"nm\":\"MCC2630\",\"cls\":2,\"id\":19338099,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2630\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"3\":{\"id\":3,\"n\":\"Start\",\"v\":\"05.2019\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19338114,\"d\":{\"nm\":\"MCC2631\",\"cls\":2,\"id\":19338114,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2631\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"3\":{\"id\":3,\"n\":\"Start\",\"v\":\"05.2019\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19338123,\"d\":{\"nm\":\"MCC2632\",\"cls\":2,\"id\":19338123,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex\",\"v\":\"MCC2632\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte TACHO\"},\"3\":{\"id\":3,\"n\":\"Start\",\"v\":\"05.2019\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19370288,\"d\":{\"nm\":\"MCC2019\",\"cls\":2,\"id\":19370288,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Gruppe\",\"v\":\"UTLPB\"},\"3\":{\"id\":3,\"n\":\"Kennzeichen\",\"v\":\"PB-UT-2019\"},\"4\":{\"id\":4,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"},\"5\":{\"id\":5,\"n\":\"vormals\",\"v\":\"\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19372092,\"d\":{\"nm\":\"MCC2032 (Tausch)\",\"cls\":2,\"id\":19372092,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"ex.\",\"v\":\"Tausch HW 01.06.2019\"},\"2\":{\"id\":2,\"n\":\"Fahrzeug\",\"v\":\"IVECO\"},\"3\":{\"id\":3,\"n\":\"Gruppe\",\"v\":\"UTMHH\"},\"4\":{\"id\":4,\"n\":\"Kennzeichen\",\"v\":\"MCC2032\"},\"5\":{\"id\":5,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413945,\"d\":{\"nm\":\"MCC2137(Tacho)\",\"cls\":2,\"id\":19413945,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413948,\"d\":{\"nm\":\"MCC2117(Tacho)\",\"cls\":2,\"id\":19413948,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413953,\"d\":{\"nm\":\"MCC2054(Tacho)\",\"cls\":2,\"id\":19413953,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413954,\"d\":{\"nm\":\"MCC2051(Tacho)\",\"cls\":2,\"id\":19413954,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413958,\"d\":{\"nm\":\"MCC2151(Tacho)\",\"cls\":2,\"id\":19413958,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413960,\"d\":{\"nm\":\"MCC2149(Tacho)\",\"cls\":2,\"id\":19413960,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413962,\"d\":{\"nm\":\"MCC2194(Tacho)\",\"cls\":2,\"id\":19413962,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413963,\"d\":{\"nm\":\"MCC2140(Tacho)\",\"cls\":2,\"id\":19413963,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413968,\"d\":{\"nm\":\"MCC2116(Tacho)\",\"cls\":2,\"id\":19413968,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413970,\"d\":{\"nm\":\"MCC2073(Tacho)\",\"cls\":2,\"id\":19413970,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413973,\"d\":{\"nm\":\"MCC2052(Tacho)\",\"cls\":2,\"id\":19413973,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413977,\"d\":{\"nm\":\"MCC2154(Tacho)\",\"cls\":2,\"id\":19413977,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413978,\"d\":{\"nm\":\"MCC2596(Tacho)\",\"cls\":2,\"id\":19413978,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413983,\"d\":{\"nm\":\"MCC2175(Tacho)\",\"cls\":2,\"id\":19413983,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413988,\"d\":{\"nm\":\"MCC2139(Tacho)\",\"cls\":2,\"id\":19413988,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413990,\"d\":{\"nm\":\"MCC2115(Tacho)\",\"cls\":2,\"id\":19413990,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413993,\"d\":{\"nm\":\"MCC2053(Tacho)\",\"cls\":2,\"id\":19413993,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19413997,\"d\":{\"nm\":\"MCC2153(Tacho)\",\"cls\":2,\"id\":19413997,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19414001,\"d\":{\"nm\":\"MCC2150(Tacho)\",\"cls\":2,\"id\":19414001,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19414005,\"d\":{\"nm\":\"MCC2196(Tacho)\",\"cls\":2,\"id\":19414005,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19414008,\"d\":{\"nm\":\"MCC2509(Tacho)\",\"cls\":2,\"id\":19414008,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9},{\"i\":19414015,\"d\":{\"nm\":\"MCC2734(Tacho)\",\"cls\":2,\"id\":19414015,\"mu\":0,\"flds\":{\"1\":{\"id\":1,\"n\":\"Fahrzeug\",\"v\":\"MAN\"},\"2\":{\"id\":2,\"n\":\"Kunde\",\"v\":\"Hilltronic \\/ Universaltransporte Tacho\"}},\"fldsmax\":0,\"uacl\":827718688759},\"f\":9}]\n"
            textBox2.Text += "\r\n";
            textBox2.Text += "=============================================";

            /*190729 comented these out
            urlGetAUs =
               wialonurlfix + "/ajax.html?svc=core/update_data_flags&sid=" + wialonLogin.eID +
               "&params={" +
                   "\"spec\":[" +
                   "{" +
                       "\"type\":\"type\"," +
                       "\"data\":\"avl_unit\"," +//use avl_unit
                       "\"flags\":256," +
                // 1 - get common information(NAMEs) ; 
                 // 256-phone+unique ID ; 
                 // 8192 - current value for km/hours/KB;
                 // 131072 - trip detection details
                 //
                       "\"mode\":0" +                // set flags 
                   "}" +
                   "]" +
               "}";
            //availableUnits
            WebClient wc1 = new WebClient();
            wc1.Encoding = Encoding.UTF8;
            result = (wc1.DownloadString(urlGetAUs));
            //txtParams.Text += result + "\r\n\r\n\r\n\r\n";
            JavaScriptSerializer ser1 = new JavaScriptSerializer();
            List<driversphones> items1 = ser.Deserialize<List<driversphones>>(result);

            //foreach (driversphones item in items1)
            //{
            //    txtParams.Text += item.i + ":" + item.d.ph + "\r\n";
            //}
            //txtParams.Text += "\r\n\r\n=============================\r\n\r\n";
            //=====================================================
            urlGetAUs =
               wialonurlfix + "/ajax.html?svc=core/update_data_flags&sid=" + wialonLogin.eID +
               "&params={" +
                   "\"spec\":[" +
                   "{" +
                       "\"type\":\"type\"," +
                       "\"data\":\"avl_unit\"," +//use avl_unit
                       "\"flags\":8192," +
                        // 1 - get common information(NAMEs) ; 
                         // 256-phone+unique ID ; 
                         // 8192 - current value for km/hours/KB;
                         // 131072 - trip detection details
                         //
                       "\"mode\":0" +  // set flags 
                   "}" +
                   "]" +
               "}";
            //availableUnits
            WebClient wc2 = new WebClient();
            wc2.Encoding = Encoding.UTF8;
            result = (wc2.DownloadString(urlGetAUs));
            //txtParams.Text += result + "\r\n\r\n\r\n\r\n";
            JavaScriptSerializer ser2 = new JavaScriptSerializer();
            List<driverscounters> items2 = ser.Deserialize<List<driverscounters>>(result);
            //foreach (driverscounters item in items2)
            //{
            //    txtParams.Text += item.i + ":" + item.d.cfl + "\r\n";
            //}
            ///==============================
            urlGetAUs =
            wialonurlfix + "/ajax.html?svc=core/update_data_flags&sid=" + wialonLogin.eID +
            "&params={" +
                "\"spec\":[" +
                "{" +
                    "\"type\":\"type\"," +
                    "\"data\":\"avl_unit\"," +//use avl_unit
                    "\"flags\":1," +
                // 1 - get common information(NAMEs) ; 
                 // 256-phone+unique ID ; 
                 // 8192 - current value for km/hours/KB;
                 // 131072 - trip detection details
                 //
                    "\"mode\":0" +                // set flags 
                "}" +
                "]" +
            "}";
            //availableUnits
            WebClient wc3 = new WebClient();
            wc3.Encoding = Encoding.UTF8;
            result = (wc3.DownloadString(urlGetAUs));
            //txtParams.Text = result + "\r\n\r\n\r\n\r\n";
            JavaScriptSerializer ser3 = new JavaScriptSerializer();
            List<unitconstrip> items3 = ser.Deserialize<List<unitconstrip>>(result);
            
            //foreach (unitconstrip item in items3)
            //{
            //txtParams.Text += item.d.rfc.fuelConsImpulse.maxImpulses;

            //}
            */

            return items;
        }

        List<filedata> filesTimestamps = new List<filedata>();
        private void unitsScan()
        {
            dddFileNames.Clear();//Clear list before start
            foreach (TreeNode node in treeViewUnits.Nodes)//iterate vehicles
            {
                //if (string.Compare(node.Tag.ToString(), "0") == 0)
                //{
                //    continue;
                //}

                string itemID = node.Tag.ToString().Split(';')[0]; //190729 
                string urlGetAUs =
                           wialonurlfix + "ajax.html?svc=file/list&sid=" + wialonLogin.eID +
                           "&params={" +
                    //"\"itemId\":" + treeViewUnits.SelectedNode.Tag.ToString() + "," + //node.Tag.ToString() + "," +
                                "\"itemId\":" + itemID+ ","+ //node.Tag.ToString() + "," + //190729 modified tag to contain tag and vehicle group
                                "\"storageType\":2," +//use tachograph folder
                                "\"path\":\"" + textBox5.Text + "\"," +//"\"path\":\"unit\"," +
                                "\"mask\":\"*\"," +
                                "\"recursive\":true," +
                                "\"fullPath\":true" +
                           "}";
                //textBox4.Text += urlGetAUs + "\r\n";
                /*if (node.Text.Contains("2009"))
                {
                    textBox4.Text += "found 2009\r\n";
                }*/
                WebClient wcgr = new WebClient();
                wcgr.Encoding = Encoding.UTF8;
                string resultgr = (wcgr.DownloadString(urlGetAUs));
                //textBox2.Text += resultgr + "\r\n";

                if (resultgr.Contains("error")) continue;

                JavaScriptSerializer ser = new JavaScriptSerializer();
                Object[] items = ser.Deserialize<Object[]>(resultgr);
                
                foreach (Object obj in items)//iterate files in tacho folder for this vehicle
                {
                    Dictionary<string, object> geoin = new Dictionary<string, object>();
                    geoin = (Dictionary<string, object>)obj;
                    //(geoin["n"].ToString().Contains("DF000015148000")) ||
                    //read min/max from db for each new file
                    /*if (
                        //(geoin["n"].ToString().Contains("DF000015148000"))
                        (geoin["n"].ToString().Contains("DF000001601780")) || 
                        (geoin["n"].ToString().Contains("DF000001575480"))
                        )//driver DF000015148000 "C_" 
                     */
                   
                    {
                        textBox2.Text += geoin["n"].ToString()+"\r\n";// Convert.ToInt64(geoin["ct"].ToString()) + "\r\n";
                        textBox2.SelectionStart = textBox2.Text.Length;
                        textBox2.ScrollToCaret();
                        readyForNextFile  = false;//190605
                        currentFile = geoin["n"].ToString().Replace(textBox5.Text, "").Replace("/", "");
                        if (((currentFile.Split('_').Length) > 4) && (currentFile.StartsWith("C_")))
                        {
                            if (!File.Exists(Path.Combine("dddfiles", currentFile)))
                            {
                                downloadFile(node.Tag.ToString(), geoin["n"].ToString(), "dddfiles");

                                dddFileNames.Add(currentFile);
                                processDDDFile(currentFile);
                                textBox2.Text += "\r\n";
                            }
                            else {
                                textBox2.Text += currentFile + " exists already \r\n";
                            }
                        }
                        else {//only download 
                            if (currentFile.StartsWith("M_")) downloadFile(node.Tag.ToString(), geoin["n"].ToString(), "vehicles");
                            else downloadFile(node.Tag.ToString(), geoin["n"].ToString(), "failed");
                        }
                    }
                    Application.DoEvents();//TODO - refactor this with async/await
                }
                Application.DoEvents();
                /*
                foreach (Object obj in items)
                {
                    Dictionary<string, object> geoin = new Dictionary<string, object>();
                    geoin = (Dictionary<string, object>)obj;
                    //MessageBox.Show(geoin["n"].ToString());
                    //foreach (KeyValuePair<string, object> pair in geoin)
                    {
                        //if (pair.Key == "n")
                        {
                            //download was here

                            bool found = false;
                            int pos = -1;
                            foreach (filedata unit in filesTimestamps)
                            {
                                if (String.Compare(unit.unitname, node.Text) == 0)
                                {
                                    found = true; pos++; break;
                                }
                                else
                                {
                                    pos++;
                                }
                            }
                            string tempunixtime = filesTimestamps[pos].unixtime;
                            if (found)
                            {//unit already in list, check if we need to update unix time
                                if (Convert.ToInt64(tempunixtime) <
                                    Convert.ToInt64(geoin["ct"].ToString()))
                                {//if current unix time is less, then update timestamp and download file
                                    //filedata unittemp = filesTimestamps[pos];
                                    //unittemp.unixtime = geoin["ct"].ToString();
                                    
                                    //filesTimestamps[pos] = unittemp;
                                    //MessageBox.Show(geoin["n"].ToString() + " : " + filesTimestamps[pos].unitname + " : " + geoin["ct"].ToString() + " : " + filesTimestamps[pos].unixtime);
                                    //downloadFile(node.Tag.ToString(), geoin["n"].ToString());
                                    textBox2.Text +="found "+ node.Tag.ToString()+" : "+geoin["n"].ToString()+"\r\n";
                                }
                            }
                        }
                    }
                }//end for loop
                */
            }
            textBox2.Text += "==============FINISHED download !================";


            
            
        }
        /*
         customizable webclient - allows timeout setup
         */
        private class MyWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri uri)
            {
                WebRequest w = base.GetWebRequest(uri);
                w.Timeout = 20 * 60 * 1000;
                return w;
            }
        }

        
        

        /********************************************
         * item id = resource id
         ********************************************/
        private void downloadFile(string itemID, string path, string filefolder)
        {
            
            string urlGetAUs =
            wialonurlfix + "ajax.html?sid=" + wialonLogin.eID +
                "&svc=file/get&params={" +
                //"\"itemId\":" + treeViewUnits.SelectedNode.Tag.ToString() + "," + //node.Tag.ToString() + "," +
                //"\"itemId\":" + node.Tag.ToString() + "," + 
                "\"itemId\":" + itemID + "," +
                "\"storageType\":2," +//use tachograph folder
                //"\"path\":\""+pair.Value.ToString()+"\"," +//"\"path\":\"unit\"," +                                   
                //"\"path\":\"" + geoin["n"].ToString() + "\"," +                               
                "\"path\":\"" + path + "\"," +
                "\"format\":1" +
            "}";
            //textBox4.Text += urlGetAUs + "\r\n";

            //return here if download timeouts
            //repeat: WebClient wcgr = new WebClient();
        repeat: MyWebClient wcgr = new MyWebClient();//190613 use customized webclient to modify timeout

            wcgr.Encoding = Encoding.UTF8;
            try {//handle error timeout 190613 - repeat
                string filename = path.Replace(textBox5.Text, "").Replace("/", "");
                string folder = filename.Replace(".ddd", "");//tachograph/C_DF000001601780_H_Becker_201707111458.ddd
                /*
                  try to get only this part of string: C_DF000001601780_H_Becker which should be the same for 
                 same driver with same card id. if driver card id changes then his files will go to different folder associated with new card id
                 */
                int pos = folder.LastIndexOf("_");

                if (pos > 0) {//just in case handle situation for filename with no underscores, should not be possible but anyway...
                    folder = folder.Substring(0,pos);
                }
                string filepath = @Application.StartupPath + "\\" + filefolder + "\\" + filename;
            
                wcgr.DownloadFile(urlGetAUs, filepath);
            } catch {
                goto repeat;
            }
            
            
            //resultgr = (wcgr.DownloadString(urlGetAUs));
            //textBox4.Text += resultgr + "\r\n";
        }

        private void button4_Click(object sender, EventArgs e)
        {
            
            if (fileNameCounter < dddFileNames.Count - 1) {
                processDDDFile(dddFileNames[fileNameCounter]);
            }
            fileNameCounter++;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            currentFile = textBox3.Text;
            processDDDFile(textBox3.Text);
        }

        private void chkCreateXML_CheckedChanged(object sender, EventArgs e)
        {
            createXML = chkCreateXML.Checked;
        }

        private void txtLoginToken_TextChanged(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

    }
}

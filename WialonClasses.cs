using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Web.Script.Serialization;
using System.Web;
using System.Globalization;
using System.Net;
using System.IO;
using System.Xml;
using System.Collections;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace DDDReader
{
    #region Classes
    public class filedata
    {
        public string unixtime { get; set; }
        public string unitname { get; set; }
    }

    public class tokens
    {
        public string h { get; set; }
        public string app { get; set; }
        public string ct { get; set; }
        public string at { get; set; }
        public string dur { get; set; }
        public string fl { get; set; }
        public object p { get; set; }
        public object items { get; set; }

    }
    /*{"token":"", "operateAs":""}*/
    public class browserUrl
    {
        public string token { get; set; }
        public string operateAs { get; set; }
    }
    public class Item
    {
        public string eid { get; set; }
        public string tm { get; set; }
        public user user { get; set; }
        public userclasses classes { get; set; }
    }

    public class user
    {
        public string nm { get; set; }
        public string cls { get; set; }
        public string id { get; set; }
        public userprop prp { get; set; }
        public string crt { get; set; }
        public string bact { get; set; }
        public string fl { get; set; }
        public string hm { get; set; }
        public string uacl { get; set; }
    }

    public class userclasses
    {
        public string avl_hw { get; set; }
        public string avl_resource { get; set; }
        public string avl_retranslator { get; set; }
        public string avl_unit { get; set; }
        public string avl_unit_group { get; set; }
        public string user { get; set; }
        public string avl_route { get; set; }
    }

    public class userprop
    {
        public string dst { get; set; }
        public string language { get; set; }
        public string msakey { get; set; }
        public string pcal { get; set; }
        public string tz { get; set; }
        public string us_units { get; set; }
    }

    public class driversphones
    {
        public string i { get; set; }
        public phonedetails d { get; set; }
        public string f { get; set; }
    }

    public class phonedetails
    {
        public string uid { get; set; }
        public string hw { get; set; }
        public string ph { get; set; }
        public string ph2 { get; set; }
        public string psw { get; set; }
    }

    public class driverscounters
    {
        public string i { get; set; }
        public drivercountersdata d { get; set; }
        public string f { get; set; }
    }

    public class drivercountersdata
    {
        public string cfl { get; set; }
        public string cnm { get; set; }
        public string cneh { get; set; }
        public string cnkb { get; set; }
    }

    public class unitconstrip//unit consumption/trip details
    {
        public string i { get; set; }
        public unitconstripdata d { get; set; }
        public string f { get; set; }
    }

    public class unitconstripdata
    {
        public rtdtype rtd { get; set; }
        public rfctype rfc { get; set; }
    }
    public class rtdtype
    {
        public string type { get; set; }
        public string gpsCorrection { get; set; }
        public string minSat { get; set; }
        public string minMovingSpeed { get; set; }
        public string minStayTime { get; set; }
        public string maxMessagesDistance { get; set; }
        public string minTripTime { get; set; }
        public string minTripDistance { get; set; }
    }

    public class rfctype
    {
        public string calcTypes { get; set; }
        public fuelLevelParamstype fuelLevelParams { get; set; }
        public fuelConsMathtype fuelConsMath { get; set; }
        public fuelConsRatestype fuelConsRates { get; set; }
        public fuelConsImpulsetype fuelConsImpulse { get; set; }
    }

    public class fuelLevelParamstype
    {
        public string flags { get; set; }
        public string ignoreStayTimeout { get; set; }
        public string minFillingVolume { get; set; }
        public string minTheftTimeout { get; set; }
        public string minTheftVolume { get; set; }
        public string filterQuality { get; set; }
    }

    public class fuelConsMathtype
    {
        public string idling { get; set; }
        public string urban { get; set; }
        public string suburban { get; set; }
        public string loadCoef { get; set; }

    }

    public class fuelConsRatestype
    {
        public string consSummer { get; set; }
        public string consWinter { get; set; }
        public string winterMonthFrom { get; set; }
        public string winterDayFrom { get; set; }
        public string winterMonthTo { get; set; }
        public string winterDayTo { get; set; }
    }

    public class fuelConsImpulsetype
    {
        public string maxImpulses { get; set; }
        public string skipZero { get; set; }
    }

    public class availableUnits
    {
        public string i { get; set; }//item id
        public itemData d { get; set; }/* item data */
        public string f { get; set; }//current flags        
    }

    public class itemData
    {
        public string nm { get; set; }	/* unit name */
        public string cls { get; set; }/* superclass ID (avl_unit) */
        public string id { get; set; } /* unit ID */
        public string uacl { get; set; }	/*  */
        public Object aflds { get; set; }//190729 - when using flag 9 there is aflds data in result
    }

    public class userAccData
    {
        public string nm { get; set; }	/* user name */
        public string id { get; set; }/* user acc itemID */       
    }

    public class reportRow
    {
        /*public string n { get; set; }
        public string i1 { get; set; }
        public string i2 { get; set; }
        public string t1 { get; set; }
        public string t2 { get; set; }
        public string d { get; set; }

        public Object[] c { get; set; }*/
        
        public string i { get; set; }
        public Object d { get; set; }
    }

    public class customFld {
        public int id { get; set; }
        public string n { get; set; }
        public string v { get; set; }
    }
    #endregion

    #region timerbrowser
    class TimerBrowser{
        System.Windows.Forms.Timer timerbrowser = new System.Windows.Forms.Timer();

        public void startMyTmrBrowser()
        {            
            timerbrowser.Tick += new EventHandler(timerbrowser_Tick); // Everytime timer ticks, timer_Tick will be called
            timerbrowser.Interval = (1000) * (1);              // Timer will tick every X miliseconds
            timerbrowser.Enabled = true;                       // Enable the timer
            timerbrowser.Start();
        }

        public bool startstop;
        void timerbrowser_Tick(object sender, EventArgs e)
        {
            //DDDReader.Program.mainForm.loginUpdate("Timer tick...");
            startstop = false;
            timerbrowser.Stop();
        }
        #endregion
    }
    public class Login
    {
        public string userid = "";
        public string debugData = "";//attach to textbox1.text - check caret ...
        public bool chkUseToken=false;//chkUseToken.Checked
        WebBrowser webBrowser1 = new WebBrowser();//replacing webbrowse1 from form
        string appname = "DDDReader";
        //bool startstop;
        public string wialonuser = "Universaltransporte";
        public string wialonpass = "michels";
        public string txtToken = "";
        public string wialonurlfix = "http://hst-api.wialon.com/wialon/";
        public string eID = "";
        public bool done = false;
        public string sessionToken;//token to use - either provided by user (chkusetoken is checked + token string) or read token from auth page
        // TODO public bool tokenOK;// store token state - in case token shall be read from auth page and that does not succeed then repeat login
        public bool DoLoginToken()
        {//ADD http://gps.mycarcontrol.de/login.html in exceptions for IE!!!!!!!!!!!!!!!!
            /*
             PR = p1.Send(wialonurlping);//
             testpr = PR.Status.ToString();//10.10.2014 ping declared outside this scope                       

             // check when the ping is not success
             if (!testpr.Equals("Success"))
             {
                 textBox1.Text += DateTime.Now + " No access to server...\r\n";
                 textBox1.SelectionStart = textBox1.Text.Length;
                 textBox1.ScrollToCaret();

                 treeViewUnits.Nodes.Clear();
                 return false;
             }
             */
            string res = "";
            string startdata = "access_token=";
            int startpos = 0;
            int endpos = 0;
            TimerBrowser tmrBrowser = new TimerBrowser();
            if (this.chkUseToken) { goto usetoken; }//180823 - added this to skip weblogin and directly go to use token
        loginstart:
            webBrowser1.Navigate("http://gps.mycarcontrol.de/login.html?client_id=" + this.appname + "&access_type=-1&activation_time=0&duration=0");

            //webBrowser1.Navigate("http://gps.mycarcontrol.de/login_simple.html");
            debugData += "starting login process....\r\n";
            //textBox1.SelectionStart = textBox1.Text.Length;
            //textBox1.ScrollToCaret();            
            tmrBrowser = new TimerBrowser();
            for (int i = 0; i < 5; i++)//5 sec delay
            {                
                tmrBrowser.startMyTmrBrowser();
                tmrBrowser.startstop = true;
                while (tmrBrowser.startstop)
                {
                    Application.DoEvents();
                }
            }
            debugData += "filling in login details\r\n";
            //textBox1.SelectionStart = textBox1.Text.Length;
            //textBox1.ScrollToCaret();

            foreach (HtmlElement elem in webBrowser1.Document.All)
            {
                if ((string.Compare(elem.TagName.ToLower(), "input") == 0) &&
                    (string.Compare(elem.GetAttribute("name"), "login") == 0))
                {
                    elem.InnerText = wialonuser;
                    break;
                }
            }
            foreach (HtmlElement elem in webBrowser1.Document.All)
            {
                if ((string.Compare(elem.TagName.ToLower(), "input") == 0) &&
                    (string.Compare(elem.GetAttribute("name"), "passw") == 0))
                {
                    elem.InnerText = wialonpass;
                    break;
                }
            }
            tmrBrowser = new TimerBrowser();
            for (int i = 0; i < 2; i++)//2 sec delay
            {
                tmrBrowser.startMyTmrBrowser();
                tmrBrowser.startstop = true;
                while (tmrBrowser.startstop)
                {
                    Application.DoEvents();
                }
            }


            //foreach (HtmlElement element in webBrowser1.Document.All)
            //{
            //    if (((element.TagName.ToLower() == "input")) && element.GetAttribute("type").ToLower() == "submit")
            //    {
            //        if (element.GetAttribute("value").ToLower().Contains("authorize"))
            //        {
            //            element.InvokeMember("click");
            //        }
            //    }
            //}


            webBrowser1.Document.Forms[0].InvokeMember("submit");
            tmrBrowser = new TimerBrowser();
            for (int i = 0; i < 2; i++)//2 sec delay 180823
            {
                tmrBrowser.startMyTmrBrowser();
                tmrBrowser.startstop = true;
                while (tmrBrowser.startstop)
                {
                    Application.DoEvents();
                }
            }

            debugData += "click Authorize button\r\n";
            DDDReader.Program.mainForm.loginUpdate("click Authorize button\r\n");
            //textBox1.SelectionStart = textBox1.Text.Length;
            //textBox1.ScrollToCaret();
            bool loggedok = false;
            tmrBrowser = new TimerBrowser();
            for (int i = 0; i < 10; i++)//10 sec delay
            {
                tmrBrowser.startMyTmrBrowser();
                tmrBrowser.startstop = true;
                while (tmrBrowser.startstop)
                {
                    Application.DoEvents();
                    if (webBrowser1.DocumentText.Contains("Authorized successfully"))
                    {
                        i = 11;//exit main loop
                        loggedok = true;
                        break;
                    }

                    if (webBrowser1.DocumentText.Contains("Invalid user name or password"))//Invalid user name or password
                    {//handle wrong user/pass here
                        i = 11;//exit main loop
                        //bool loginok still = false by default
                        break;
                    }
                }

            }
            DDDReader.Program.mainForm.loginUpdate("authorized successfully, read token");
            /*if (!loggedok)
            {
                textBox1.Text += "not logged , wrong user or pass for mycarcontrol\r\n";
                textBox1.SelectionStart = textBox1.Text.Length;
                textBox1.ScrollToCaret();
                return false;
            }

            textBox1.Text += "authorized successfully, read token\r\n";
            textBox1.SelectionStart = textBox1.Text.Length;
            textBox1.ScrollToCaret();*/
            //use below 2x lines with login_simple
            //object token = webBrowser1.Document.InvokeScript("eval", new object[] { "token" });           

            //use below with login.html
            //access_token= &svc_err
            res = webBrowser1.Url.ToString();
            startdata = "access_token=";
            string enddata = "&svc_err";

            //check if required details are found in url otherwise return to start
            //infinite loop here
            startpos = res.IndexOf(startdata);
            if (startpos < 0)
            {
                debugData += "Cant find login details access_token=, retrying in 15 secs\r\n";
                DDDReader.Program.mainForm.loginUpdate("1 Cant find login details access_token=, retrying in 15 secs\r\n");
                //textBox1.SelectionStart = textBox1.Text.Length;
                //textBox1.ScrollToCaret();
                tmrBrowser = new TimerBrowser();
                for (int i = 0; i < 15; i++)//15 sec delay
                {
                    tmrBrowser.startMyTmrBrowser();
                    tmrBrowser.startstop = true;
                    while (tmrBrowser.startstop)
                    {
                        Application.DoEvents();
                    }
                }
                goto loginstart;
            }

            endpos = res.IndexOf(enddata, startpos);

            if (endpos < 0)
            {
                debugData += "Cant find login details &svc_err, retrying in 15 secs\r\n";
                DDDReader.Program.mainForm.loginUpdate("2 Cant find login details &svc_err, retrying in 15 secs");
                //textBox1.SelectionStart = textBox1.Text.Length;
                //textBox1.ScrollToCaret();
                tmrBrowser = new TimerBrowser();
                for (int i = 0; i < 15; i++)//15 sec delay
                {
                    tmrBrowser.startMyTmrBrowser();
                    tmrBrowser.startstop = true;
                    while (tmrBrowser.startstop)
                    {
                        Application.DoEvents();
                    }
                }
                goto loginstart;
            }
        usetoken:
            string token = "";
            //MessageBox.Show(res.Substring(startpos,endpos-startpos).Replace(startdata,""));

            //Authorized successfully
            //textBox2.Text = token + "\r\n"; ;
            //http://hst-api.wialon.com/wialon/ajax.html?svc=token/login&params={"token":"f17be4ebfb2165efbced6c6c49e3d0e338134EB5264A9AF7D076F677E58A6A4A431F4339", "operateAs":"venelinvasilev"}

            if (chkUseToken)
            {
                token = txtToken;
            }
            else
            {
                token = res.Substring(startpos, endpos - startpos).Replace(startdata, "");
            }
            string urlDoLogin =
                wialonurlfix + "ajax.html?svc=token/login&params={\"token\":\"" + token + "\", \"operateAs\":\"" + wialonuser + "\"}";


            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = 2147483646;
            try
            {
                Item items = ser.Deserialize<Item>((new WebClient()).DownloadString(urlDoLogin));                
                this.eID = items.eid;
                debugData += eID + "\r\n";
                this.userid = items.user.id;
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                debugData += urlDoLogin + "\r\n";
                debugData += "1 Crash at mycarcontrol login : " + ex.Message + "\r\n\r\n\r\n\r\n";
                DDDReader.Program.mainForm.loginUpdate("1 Crash : " + urlDoLogin + " : " + ex.Message);
                //textBox1.SelectionStart = textBox1.Text.Length;
                //textBox1.ScrollToCaret();
            }
            DDDReader.Program.mainForm.loginUpdate("EID is "+eID);
            

            debugData += "check number of existing tokens and delete unneeded\r\n";
            DDDReader.Program.mainForm.loginUpdate("check number of existing tokens and delete unneeded\r\n");
            //textBox1.SelectionStart = textBox1.Text.Length;
            //textBox1.ScrollToCaret();
            //check number of existing tokens and delete
            string urlCheckTokenCount = "http://hst-api.wialon.com/wialon/ajax.html?svc=token/list&sid=" + eID + "&params={\"userId\":\"" + userid + "\"}";
            debugData += urlCheckTokenCount + "\r\n";
            JavaScriptSerializer ser1 = new JavaScriptSerializer();
            try
            {
                List<tokens> tokenslist = ser1.Deserialize<List<tokens>>((new WebClient()).DownloadString(urlCheckTokenCount));
                //MessageBox.Show(tokenslist.Count.ToString());
                if (tokenslist.Count > 900)//max allowed number of tokens per user is 1000; control how many tokens, delete all if too many
                {//will delete only tokens under this app name and this user
                    debugData += "too many tokens for user :" + tokenslist.Count.ToString() + "starting deletion tokens for this user and this app\r\n";
                    for (int i = 0; i < 900; i++)
                    {
                        string currenttokenappname = tokenslist[i].app;
                        if (string.Compare(currenttokenappname, appname) == 0)
                        {
                            string currenttoken = tokenslist[i].h;

                            if (string.Compare(currenttoken, token) != 0)//skip current token , delete all the rest, 
                            {
                                string urlDeleteToken = wialonurlfix + "ajax.html?svc=token/update&params=" +
                                "{\"callMode\":\"delete\",\"userId\":\"" + userid + "\",\"h\":\"" + currenttoken + "\",\"app\":\"" + appname + "\",\"at\":0,\"dur\":0,\"fl\":256,\"p\":\"{}\",\"items\":[],\"deleteAll\":\"\"}";

                                string tokendel = (new WebClient()).DownloadString(urlDeleteToken);

                                debugData += "deleting token " + currenttoken + "\r\n";
                                //textBox1.SelectionStart = textBox1.Text.Length;
                                //textBox1.ScrollToCaret();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                debugData += urlDoLogin + "\r\n";
                debugData += "2 Crash at mycarcontrol login : " + ex.Message + "\r\n\r\n\r\n\r\n";
                
                //textBox1.SelectionStart = textBox1.Text.Length;
                //textBox1.ScrollToCaret();
            }

            debugData += "Login finished" + "\r\n";
            //textBox1.SelectionStart = textBox1.Text.Length;
            //textBox1.ScrollToCaret();
            
            DDDReader.Program.mainForm.loginUpdate("Login finished");
            DDDReader.Program.mainForm.loginUpdate(token);
            
            
            this.sessionToken = token;
            

            return true;
        }
    }
}



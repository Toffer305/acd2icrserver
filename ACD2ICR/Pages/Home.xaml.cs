using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;
using HtmlAgilityPack;
using MySql.Data.MySqlClient;

namespace ACD2ICR.Pages
{
    /// <summary>
    /// Interaction logic for Home.xaml
    /// </summary>
    public partial class Home : UserControl
    {
        int timetosendemails = ACD2ICR.Properties.Settings.Default.EMAILsendtime;  //Hour Component of 24-Hour Clock.
        int smtpport = ACD2ICR.Properties.Settings.Default.SMTPport;
        string smtphost = ACD2ICR.Properties.Settings.Default.SMTPhost;
        string smtpusername = ACD2ICR.Properties.Settings.Default.SMTPusername;
        string smtppassword = ACD2ICR.Properties.Settings.Default.SMTPpassword;
        string smtpsubjectline = ACD2ICR.Properties.Settings.Default.SMTPsubjectline;
        string smtpmessagebody = string.Empty;
        string emailistfilepath = ACD2ICR.Properties.Settings.Default.EMAILlistfilepath;

        int numofpagesneeded = 0;
        int inumofrecords = 0;
        int firstdigit = 1;

        List<string> emailrecipientslist = new List<string>();
        //List<string> agentstats = new List<string>();
        StringBuilder myStringBuilder = new StringBuilder();

        DispatcherTimer dt = new DispatcherTimer();
        Stopwatch stopWatch = new Stopwatch();        

        CookieContainer ACDcookies = new CookieContainer();
        //CookieContainer VMcookies = new CookieContainer();
        private const string NBSP = @"&nbsp;";

        //private WebClient vmwebclient;
        //Uri _vmlogin = new Uri(ACD2ICR.Properties.Settings.Default.VMLoginUrl);
        //Uri _vmviewlogin = new Uri(ACD2ICR.Properties.Settings.Default.VMVXView);
        //Uri _vmviewroot = new Uri(ACD2ICR.Properties.Settings.Default.VMVXViewRoot);
        //Uri _vmviewdo = new Uri(ACD2ICR.Properties.Settings.Default.VMVXViewDo);

        //string htmlreceived = string.Empty;

        public Home()
        {
            Debug.WriteLine("initial startup");
            InitializeComponent();

            //InitializeVoicemails();

            //VoiceMailLoginProcesses();
            //Thread.Sleep(500);
            
            ACD_Login();
            Thread.Sleep(750);

            ACD_GoBusy();
            Thread.Sleep(750);

            ACD_OverViewReport();
            Thread.Sleep(750);

            dt.Tick += new EventHandler(dt_Tick);
            dt.Interval = new TimeSpan(0, 0, 0, 20, 0);
            dt.Start();
            stopWatch.Start();   
        }

        private void dt_Tick(object sender, EventArgs e)
        {
            if (stopWatch.IsRunning)
            {
                // ACD Check
                ACD_OverViewReport();                
            }
        }

        #region ACD Login
        private void addCookies(HttpWebRequest request)
        {
            Debug.Write("adding Cookies, current Count: ");
            if (ACDcookies.Count < 20)
            {
                Cookie PHPSess = new Cookie("PHPSESSID", "392icrheh33") { Domain = request.Host };
                ACDcookies.Add(PHPSess);
            }
            Debug.WriteLine(ACDcookies.Count);
        }

        private void ACD_Login()
        {
            Debug.WriteLine("ACD_Login()");
            HtmlDocument doc = new HtmlDocument();
            Uri cookieuri = new Uri(ACD2ICR.Properties.Settings.Default.ACDLoginUrl);
            string acd_parameters = string.Format("login={0}&password={1}&Button_DoLogin={2}", "ic4215", "i4215", "Login");
            var request = (HttpWebRequest)HttpWebRequest.Create(cookieuri);
            request.Timeout = 5000;
            addCookies(request);
            request.CookieContainer = ACDcookies;
            request.ContentType = "application/x-www-form-urlencoded";
            request.Method = "POST";
            byte[] bytes = Encoding.ASCII.GetBytes(acd_parameters);
            request.ContentLength = bytes.Length;
            using (Stream os = request.GetRequestStream())
            {
                os.Write(bytes, 0, bytes.Length);
            }
            HttpWebResponse resp = (HttpWebResponse)request.GetResponse();
            foreach (Cookie _cookie in resp.Cookies)
            {
                Debug.WriteLine("added Cookie");
                ACDcookies.Add(_cookie);
                Debug.WriteLine(_cookie.Name);
            }
        }

        private void ACD_GoBusy()
        {
            Debug.WriteLine("ACD_GoBusy()");
            Uri cookieuri = new Uri(ACD2ICR.Properties.Settings.Default.ACDGoBusy);
            var request = (HttpWebRequest)HttpWebRequest.Create(cookieuri);
            request.Timeout = 5000;
            addCookies(request);
            request.CookieContainer = ACDcookies;
            request.Method = "GET";
            HttpWebResponse WebResp = (HttpWebResponse)request.GetResponse();

            foreach (Cookie _cookie in WebResp.Cookies)
            {
                ACDcookies.Add(_cookie);
                Debug.WriteLine(_cookie.Value);
            }
            WebResp.Close();
        }
        
        #endregion

        #region ACD Logic
        private void ACD_OverViewReport()
        {
            Debug.WriteLine("ACD_OverViewReport()");
            HtmlDocument doc = new HtmlDocument();
            Uri cookieuri = new Uri(ACD2ICR.Properties.Settings.Default.ACDOverViewUrl);
            var request = (HttpWebRequest)HttpWebRequest.Create(cookieuri);
            request.Timeout = 5000;
            addCookies(request);
            Cookie URLDAcc = new Cookie("URL_Dial_Account", "36056") { Domain = request.Host };
            Cookie URLDExt = new Cookie("URL_Dial_Extension", "1000394215") { Domain = request.Host };
            Cookie URLDLoc = new Cookie("URL_Dial_Location", "0") { Domain = request.Host };
            Cookie URLDNam = new Cookie("URL_Dial_Name", "John+Bartel") { Domain = request.Host };
            ACDcookies.Add(URLDAcc);
            ACDcookies.Add(URLDExt);
            ACDcookies.Add(URLDLoc);
            ACDcookies.Add(URLDNam);
            request.CookieContainer = ACDcookies;
            request.Method = "GET";
            try
            {
                HttpWebResponse WebResp = (HttpWebResponse)request.GetResponse();
                Debug.WriteLine("Overview Request Success");
                foreach (Cookie _cookie in WebResp.Cookies)
                {
                    ACDcookies.Add(_cookie);
                }
                Stream answer = WebResp.GetResponseStream();
                StreamReader _Answer = new StreamReader(answer);
                string htmlresponse = _Answer.ReadToEnd();
                doc.LoadHtml(htmlresponse);
                ExtractACDOverviewtables(doc);
                WebResp.Close();
            }
            catch (WebException we)
            {
                System.Diagnostics.Debug.WriteLine(we);
            }
        }

        private async void ExtractACDOverviewtables(HtmlDocument doc)
        {
            Debug.WriteLine("ExtractACDOverviewtables()");
            MySqlConnection dbConn = null;
            MySqlCommand cmd = null;
            //List<string> t1Cells = new List<string>();
            //List<string> t2Cells = new List<string>();

            var tablenodes = doc.DocumentNode.SelectNodes("//table[@class='Grid']");
            var t1rownodes = tablenodes[0].SelectNodes("tr[@class='Row'] | tr[@class='AltRow']");
            var t2rownodes = tablenodes[1].SelectNodes("tr[@class='Row'] | tr[@class='AltRow']");
            dbConn = new MySqlConnection("Server=" + ACD2ICR.Properties.Settings.Default.DBServer
                            + ";Database=" + ACD2ICR.Properties.Settings.Default.DBName
                            + ";Uid=" + ACD2ICR.Properties.Settings.Default.DBUser
                            + ";Pwd=" + ACD2ICR.Properties.Settings.Default.DBPass + ";");
            dbConn.Open();
            cmd = new MySqlCommand(ACD2ICR.Properties.Settings.Default.ACDTrunc, dbConn);
            cmd.Prepare();
            cmd.ExecuteNonQuery();
            dbConn.Close();
            // Table1
            for (int i = 1; i < t1rownodes.Count; i++)
            {
                var t1cellnodes = t1rownodes[i].SelectNodes("td");
                string uid = t1cellnodes[2].InnerText.Replace(NBSP, "");
                string status = t1cellnodes[3].InnerText.Replace(NBSP, "");
                string calldate = t1cellnodes[4].InnerText.Replace(NBSP, "");
                string answertime = t1cellnodes[5].InnerText.Replace(NBSP, "");
                string holdtime = t1cellnodes[6].InnerText.Replace(NBSP, "");
                int _holdtime = Int32.Parse(holdtime);
                _holdtime = (_holdtime / 60);
                string talktime = t1cellnodes[7].InnerText.Replace(NBSP, "");
                int _talktime = Int32.Parse(talktime);
                _talktime = (_talktime / 60);
                string agentname = t1cellnodes[10].InnerText.Replace(NBSP, "");
                string queuename = t1cellnodes[11].InnerText.Replace(NBSP, "");

                if (!(agentname.StartsWith("ICR") || agentname.Equals("Jason Zandman")
                    || agentname.Equals("John Bartel") || agentname.Equals("Brian Roberts")
                    || agentname.Equals("Michael Olivera") || agentname.Equals("NONE") || status.Equals("Ringing")))
                {
                    if (queuename.Equals("Outbound Call"))
                    {
                        _holdtime = 0;
                    }
                    //Check calls table for uid
                    else if (CheckCallsForUID(uid))
                    {
                        ACD_UpdateRecord();
                    }
                    else
                    {
                        //Create new entry

                        dbConn = new MySqlConnection("Server=" + ACD2ICR.Properties.Settings.Default.DBServer
                            + ";Database=" + ACD2ICR.Properties.Settings.Default.DBName
                            + ";Uid=" + ACD2ICR.Properties.Settings.Default.DBUser
                            + ";Pwd=" + ACD2ICR.Properties.Settings.Default.DBPass + ";");
                        dbConn.Open();
                        // DB Command Preparation
                        cmd = new MySqlCommand(ACD2ICR.Properties.Settings.Default.DBInsertActiveCalls, dbConn);
                        cmd.Parameters.AddWithValue("?uid", uid.Trim());
                        cmd.Parameters.AddWithValue("?status", status.Trim());
                        cmd.Parameters.AddWithValue("?date", calldate);
                        cmd.Parameters.AddWithValue("?startTime", answertime);
                        cmd.Parameters.AddWithValue("?holdtime", Convert.ToString(_holdtime));
                        cmd.Parameters.AddWithValue("?duration", Convert.ToString(_talktime));
                        cmd.Parameters.AddWithValue("?tech", agentname);
                        cmd.Parameters.AddWithValue("?queuename", queuename);
                        cmd.Prepare();
                        // DB Command Execution
                        int insertcompletion = cmd.ExecuteNonQuery();
                        dbConn.Close();
                    }
                }
            }

            // Table2
            for (int i = 1; i < t2rownodes.Count; i++)
            {
                var t2cellnodes = t2rownodes[i].SelectNodes("td");
                string name = t2cellnodes[1].InnerText.Replace(NBSP, "");
                string phone = t2cellnodes[2].InnerText.Replace(NBSP, "");
                string status = t2cellnodes[4].InnerText.Replace(NBSP, "");
                string callstoday = t2cellnodes[6].InnerText.Replace(NBSP, "");
                string passed = t2cellnodes[7].InnerText.Replace(NBSP, "");

                if (!(name.StartsWith("ICR") || name.Equals("Jason Zandman")
                    || name.Equals("John Bartel") || name.Equals("Brian Roberts")
                    || name.Equals("Michael Olivera") || name.Equals("NONE")))
                {

                    //Check for a today record                
                    // If found, then update
                    //if (CheckStatsExisitingRecord(name))
                    //{

                    //    //Update said record

                    //}
                    //else
                    //{

                    //}
                    // Else , create record

                    // DB Connection
                    dbConn = new MySqlConnection("Server=" + ACD2ICR.Properties.Settings.Default.DBServer
                        + ";Database=" + ACD2ICR.Properties.Settings.Default.DBName
                        + ";Uid=" + ACD2ICR.Properties.Settings.Default.DBUser
                        + ";Pwd=" + ACD2ICR.Properties.Settings.Default.DBPass + ";");
                    await dbConn.OpenAsync();
                    // DB Command Preparation
                    cmd = new MySqlCommand(ACD2ICR.Properties.Settings.Default.DBInsertOverview, dbConn);
                    cmd.Parameters.AddWithValue("?name", name);
                    cmd.Parameters.AddWithValue("?date", System.DateTime.Today.ToShortDateString());
                    cmd.Parameters.AddWithValue("?phone", phone);
                    cmd.Parameters.AddWithValue("?status", status);
                    cmd.Parameters.AddWithValue("?passed", passed);
                    cmd.Prepare();
                    // DB Command Execution
                    int insertcompletion = await cmd.ExecuteNonQueryAsync();
                    dbConn.Close();
                }
            }
        }

        private bool CheckCallsForUID(string uid)
        {
            Debug.WriteLine("CheckCallsForUID()");
            MySqlConnection dbConn = null;
            MySqlDataReader dbReader = null;
            string preparedquery = ACD2ICR.Properties.Settings.Default.DBCheckUID + uid;
            int result = 0;
            bool flag = false;
            using (dbConn = new MySqlConnection("Server=" + ACD2ICR.Properties.Settings.Default.DBServer +
                                                ";Database=" + ACD2ICR.Properties.Settings.Default.DBName +
                                                ";Uid=" + ACD2ICR.Properties.Settings.Default.DBUser +
                                                ";Pwd=" + ACD2ICR.Properties.Settings.Default.DBPass + ";"))
            {
                dbConn.Open();
                using (MySqlCommand cmd = new MySqlCommand(preparedquery, dbConn))
                {
                    dbReader = (MySqlDataReader)cmd.ExecuteReader();
                    while (dbReader.Read())
                    {
                        result = Convert.ToInt32(dbReader["NumOfRows"]);
                    }
                }
                dbReader.Close();
                dbConn.Close();
            }
            if (result > 0)
            {
                flag = true;
            }
            return flag;
        }

        private bool CheckStatsExisitingRecord(string name)
        {
            Debug.WriteLine("CheckStatsExisitingRecord");
            MySqlConnection dbConn = null;
            MySqlDataReader dbReader = null;
            string preparedquery = ACD2ICR.Properties.Settings.Default.DVCheck_OviewViewURL + name;
            int result = 0;
            bool flag = false;
            using (dbConn = new MySqlConnection("Server=" + ACD2ICR.Properties.Settings.Default.DBServer +
                                                ";Database=" + ACD2ICR.Properties.Settings.Default.DBName +
                                                ";Uid=" + ACD2ICR.Properties.Settings.Default.DBUser +
                                                ";Pwd=" + ACD2ICR.Properties.Settings.Default.DBPass + ";"))
            {
                dbConn.Open();
                using (MySqlCommand cmd = new MySqlCommand(preparedquery, dbConn))
                {
                    dbReader = (MySqlDataReader)cmd.ExecuteReader();
                    while (dbReader.Read())
                    {
                        result = Convert.ToInt32(dbReader["NumOfRows"]);
                    }
                }
                dbReader.Close();
                dbConn.Close();
            }
            if (result > 0)
            {
                flag = true;
            }
            return flag;
        }

        private void ACD_UpdateRecord()
        {
            Debug.WriteLine("ACD_UpdateRecord()");
            inumofrecords = 0;
            HtmlDocument doc = new HtmlDocument();
            Uri cookieuri = new Uri(ACD2ICR.Properties.Settings.Default.ACDQueueLanding + firstdigit);
            var request = (HttpWebRequest)HttpWebRequest.Create(cookieuri);
            request.Timeout = 10000;
            request.CookieContainer = ACDcookies;
            request.Method = "GET";
            try
            {
                HttpWebResponse WebResp = (HttpWebResponse)request.GetResponse();

                foreach (Cookie _cookie in WebResp.Cookies)
                {
                    ACDcookies.Add(_cookie);
                }
                Stream answer = WebResp.GetResponseStream();
                StreamReader _Answer = new StreamReader(answer);
                string htmlresponse = _Answer.ReadToEnd();
                doc.LoadHtml(htmlresponse);
                var tablenodes = doc.DocumentNode.SelectNodes("//table[@class='Grid']");
                var t1rownodes = tablenodes[0].SelectNodes("tr[@class='Row'] | tr[@class='AltRow']");

                // Get Number of Records
                var t1cellnode = t1rownodes[0].SelectNodes("td");
                string numberofrecords = t1cellnode[0].InnerText.Replace("Total Records:&nbsp;", "");
                inumofrecords = Int32.Parse(numberofrecords);

                //Calculcate Number of Pages
                if (inumofrecords < 10)
                    firstdigit = 0;
                else if (inumofrecords < 100)
                    firstdigit = 0;
                else if (inumofrecords < 1000)
                    firstdigit = inumofrecords / 100;
                else if (inumofrecords < 10000)
                    firstdigit = inumofrecords / 1000;
                numofpagesneeded = firstdigit + 1;

                for (int i = 0; i < numofpagesneeded; i++)
                {
                    ACD_QueueMonitor(i);
                }
            }
            catch (WebException we)
            {
                System.Diagnostics.Debug.WriteLine(we);
            }
        }

        private void ACD_QueueMonitor(int pagenumber)
        {
            pagenumber++;
            HtmlDocument doc = new HtmlDocument();
            Uri cookieuri = new Uri(ACD2ICR.Properties.Settings.Default.ACDQueueLanding + pagenumber);
            var request = (HttpWebRequest)HttpWebRequest.Create(cookieuri);
            request.Timeout = 10000;
            request.CookieContainer = ACDcookies;
            request.Method = "GET";
            try
            {
                HttpWebResponse WebResp = (HttpWebResponse)request.GetResponse();

                foreach (Cookie _cookie in WebResp.Cookies)
                {
                    ACDcookies.Add(_cookie);
                }
                Stream answer = WebResp.GetResponseStream();
                StreamReader _Answer = new StreamReader(answer);
                string htmlresponse = _Answer.ReadToEnd();
                doc.LoadHtml(htmlresponse);
                ExtractACDQueue(doc);
            }
            catch (WebException we)
            {
                System.Diagnostics.Debug.WriteLine(we);
            }

        }

        private void ExtractACDQueue(HtmlDocument doc)
        {
            Debug.WriteLine("ExtractACDQueue");
            MySqlConnection dbConn = null;
            MySqlCommand cmd = null;

            var tablenodes = doc.DocumentNode.SelectNodes("//table[@class='Grid']");
            var t1rownodes = tablenodes[0].SelectNodes("tr[@class='Row'] | tr[@class='AltRow']");

            for (int i = 1; i < t1rownodes.Count; i++)
            {
                var t1cellnodes = t1rownodes[i].SelectNodes("td");

                string uid = t1cellnodes[1].InnerText.Replace(NBSP, "");

                string status = t1cellnodes[3].InnerText.Replace(NBSP, "");

                string date = t1cellnodes[4].InnerText.Replace(NBSP, "");

                string answertime = t1cellnodes[5].InnerText.Replace(NBSP, "");

                string releasetime = t1cellnodes[6].InnerText.Replace(NBSP, "");

                string holdtime = t1cellnodes[9].InnerText.Replace(NBSP, "");
                int heldtime = Int32.Parse(holdtime);

                string talktime = t1cellnodes[10].InnerText.Replace(NBSP, "");
                int duration = Int32.Parse(talktime);

                string agentname = t1cellnodes[15].InnerText.Replace(NBSP, "");

                string queuename = t1cellnodes[16].InnerText.Replace(NBSP, "");

                // DB Connection
                using (dbConn = new MySqlConnection("Server=" + ACD2ICR.Properties.Settings.Default.DBServer
                    + ";Database=" + ACD2ICR.Properties.Settings.Default.DBName
                    + ";Uid=" + ACD2ICR.Properties.Settings.Default.DBUser + ";Pwd="
                    + ACD2ICR.Properties.Settings.Default.DBPass + ";"))
                {
                    dbConn.Open();
                    using (cmd = new MySqlCommand(ACD2ICR.Properties.Settings.Default.DBInsertCompletedCall, dbConn))
                    {
                        cmd.Parameters.AddWithValue("?status", status.Trim());
                        cmd.Parameters.AddWithValue("?startTime", answertime);
                        cmd.Parameters.AddWithValue("?stopTime", releasetime);
                        cmd.Parameters.AddWithValue("?holdtime", (heldtime / 60));
                        cmd.Parameters.AddWithValue("?duration", (duration / 60));
                        cmd.Parameters.AddWithValue("?uid", uid);
                        cmd.Prepare();
                        // DB Command Execution
                        int insertcompletion = cmd.ExecuteNonQuery();
                    }
                    dbConn.Close();
                }

            }

        } 
        #endregion

        #region GatherReports
        private void SendDailyReport()
        {
            ReadEmailFile();
            GetAgentDailyStats();
            SendEmailMessages();
        }

        private void SendMonthlyReport()
        {
            ReadEmailFile();
            GetMonthlyReport();
            SendEmailMessages();
        }

        private void ReadEmailFile()
        {
            string line;
            emailrecipientslist.Clear();
            try
            {
                using (StreamReader sr = new StreamReader(emailistfilepath))
                {
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (IsValidMailAddress(line))
                        {
                            emailrecipientslist.Add(line);
                        }
                    }
                    sr.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private bool IsValidMailAddress(string inputemail)
        {
            try
            {
                MailAddress mailaddress = new MailAddress(inputemail);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private void GetAgentDailyStats()
        {
            try
            {
                MySqlConnection dbConn = null;
                MySqlDataReader dbReader = null;
                string receivedemail = string.Empty;
                int receivedNumOfRows = 0;
                DateTime rightnow = DateTime.Now;

                myStringBuilder.Clear();
                myStringBuilder.AppendLine("DAILY AGENT REPORT");
                myStringBuilder.AppendLine(Convert.ToString(System.DateTime.Today.ToShortDateString()));
                myStringBuilder.AppendLine("");
                myStringBuilder.AppendLine("");
                myStringBuilder.AppendLine("Agent : Number of Calls : Average Call Duration (minutes) : Passed Calls");

                using (dbConn = new MySqlConnection("Server=" + ACD2ICR.Properties.Settings.Default.DBServer +
                                                ";Database=" + ACD2ICR.Properties.Settings.Default.DBName +
                                                ";Uid=" + ACD2ICR.Properties.Settings.Default.DBUser +
                                                ";Pwd=" + ACD2ICR.Properties.Settings.Default.DBPass + ";"))
                {
                    dbConn.Open();

                    using (MySqlCommand cmd = new MySqlCommand(ACD2ICR.Properties.Settings.Default.DBGetAgentDailyStats, dbConn))
                    {
                        dbReader = cmd.ExecuteReader();
                        while (dbReader.Read())
                        {
                            receivedemail = Convert.ToString(dbReader["tech"]);
                            receivedNumOfRows = Convert.ToInt32(dbReader["NumOfRows"]);
                            int averageduration = Convert.ToInt32(dbReader["TechAverageDuration"]);
                            int convertedduration = (averageduration / 60);
                            int passedcalls = Convert.ToInt32(dbReader["passed"]);
                            myStringBuilder.AppendLine(receivedemail + "    :    "
                                                    + receivedNumOfRows + "    :    "
                                                    + convertedduration + "    :    "
                                                    + passedcalls);
                        }
                    }

                    dbReader.Close();
                    dbConn.Close();

                }

            }
            catch (MySqlException err)
            {
                System.Diagnostics.Debug.WriteLine(err);
            }
        }

        private void GetMonthlyReport()
        {
            try
            {
                MySqlConnection dbConn = null;
                MySqlDataReader dbReader = null;
                string receivedemail = string.Empty;
                int receivedNumOfRows = 0;

                var today = DateTime.Today;
                var month = new DateTime(today.Year, today.Month, 1);
                var first = month.AddMonths(-1);
                var last = month.AddDays(-1);

                myStringBuilder.Clear();
                myStringBuilder.AppendLine("MONTHLY AGENT REPORT");
                myStringBuilder.AppendLine(Convert.ToString(first.ToShortDateString()));
                myStringBuilder.AppendLine("");
                myStringBuilder.AppendLine("");
                myStringBuilder.AppendLine("Agent   : Calls : Avg Talk Time : Passed Calls");

                using (dbConn = new MySqlConnection("Server=" + ACD2ICR.Properties.Settings.Default.DBServer +
                                                ";Database=" + ACD2ICR.Properties.Settings.Default.DBName +
                                                ";Uid=" + ACD2ICR.Properties.Settings.Default.DBUser +
                                                ";Pwd=" + ACD2ICR.Properties.Settings.Default.DBPass + ";"))
                {
                    dbConn.Open();

                    using (MySqlCommand cmd = new MySqlCommand(ACD2ICR.Properties.Settings.Default.DBGetMonthlyReport, dbConn))
                    {
                        dbReader = cmd.ExecuteReader();
                        while (dbReader.Read())
                        {
                            receivedemail = Convert.ToString(dbReader["tech"]);
                            receivedNumOfRows = Convert.ToInt32(dbReader["NumOfRows"]);
                            int averageduration = Convert.ToInt32(dbReader["TechAverageDuration"]);
                            int convertedduration = (averageduration / 60);
                            int passedcalls = Convert.ToInt32(dbReader["passed"]);
                            myStringBuilder.AppendLine(receivedemail + "    :    "
                                                    + receivedNumOfRows + "    :    "
                                                    + convertedduration + "    :    "
                                                    + passedcalls);
                        }
                    }
                    dbReader.Close();
                    dbConn.Close();

                }

            }
            catch (MySqlException err)
            {
                System.Diagnostics.Debug.WriteLine(err);
            }
        }

        private void SendEmailMessages()
        {
            smtpmessagebody = myStringBuilder.ToString();
            for (int i = 0; i < emailrecipientslist.Count; i++)
            {
                try
                {
                    MailMessage mail = new MailMessage(smtpusername, emailrecipientslist[i], smtpsubjectline, smtpmessagebody);
                    SmtpClient client = new SmtpClient(smtphost);
                    client.Port = smtpport;
                    client.Credentials = new System.Net.NetworkCredential(smtpusername, smtppassword);
                    client.EnableSsl = true;
                    client.Send(mail);
                }
                catch (SmtpException se)
                {
                    System.Diagnostics.Debug.WriteLine(se);
                    throw;
                }
            }
        }
        
        #endregion

        #region Voicemails

        //private void GetListOfEmployees()
        //{
        //    try
        //    {
        //        List<string> listofemployees = new List<string>();
        //        MySqlConnection dbConn = null;
        //        MySqlDataReader dbReader = null;
        //        dbConn = new MySqlConnection("Server=" + ACD2ICR.Properties.Settings.Default.DBServer
        //                                                + ";Database=" + ACD2ICR.Properties.Settings.Default.DBName
        //                                                + ";Uid=" + ACD2ICR.Properties.Settings.Default.DBUser
        //                                                + ";Pwd=" + ACD2ICR.Properties.Settings.Default.DBPass + ";");
        //        dbConn.Open();
        //        MySqlCommand cmd = new MySqlCommand("SELECT * ", dbConn);
        //        dbReader = cmd.ExecuteReader();
        //        while (dbReader.Read())
        //        {
        //            receivedcardholder = Convert.ToString(dbReader["sCardholder"]);
        //            cardholderlist.Add(receivedcardholder);
        //            //System.Diagnostics.Debug.WriteLine(receivedcardholder);                    
        //        }
        //        dbReader.Close();
        //        dbConn.Close();
        //    }
        //    catch (MySqlException err)
        //    {
        //        System.Diagnostics.Debug.WriteLine(err);
        //        throw;
        //    }

        //}

        //private void InitializeVoicemails()
        //{
        //    vmwebclient = new WebClient();
        //    vmwebclient.DownloadDataCompleted += new DownloadDataCompletedEventHandler(stringDataCompletedEvent);
        //}

        //private void VoiceMailLoginProcesses()
        //{
        //    VMLogin();
        //    Thread.Sleep(250);
        //    VMVXView();
        //    Thread.Sleep(250);
        //    VMVXViewRoot();
        //    Thread.Sleep(250);
        //    VMVXViewDo();
        //}

        //private void VMLogin()
        //{
        //    string txt = string.Empty;
        //    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_vmlogin);
        //    request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:30.0) Gecko/20100101 Firefox/30.0";
        //    request.Accept = "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
        //    request.Headers.Add("Accept-Language: en-US,en;q=0.5");
        //    request.Headers.Add("Accept-Encoding: gzip,deflate");
        //    request.KeepAlive = true;
        //    request.CookieContainer = new CookieContainer();

        //    HttpWebResponse response = (HttpWebResponse)request.GetResponse();           
        //}

        //private void VMVXView()
        //{
        //    HttpWebRequest loginrequest = (HttpWebRequest)WebRequest.Create(_vmviewlogin);
        //    loginrequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:30.0) Gecko/20100101 Firefox/30.0";
        //    loginrequest.Accept = "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
        //    loginrequest.Headers.Add("Accept-Language: en-US,en;q=0.5");
        //    loginrequest.Headers.Add("Accept-Encoding: gzip,deflate");
        //    loginrequest.Referer = ACD2ICR.Properties.Settings.Default.VMLoginUrl;
        //    loginrequest.KeepAlive = true;
        //    loginrequest.CookieContainer = new CookieContainer();

        //    HttpWebResponse loginresponse = (HttpWebResponse)loginrequest.GetResponse();            
        //}

        //private void VMVXViewRoot()
        //{
        //    HttpWebRequest rootrequest = (HttpWebRequest)WebRequest.Create(_vmviewroot);
        //    rootrequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:30.0) Gecko/20100101 Firefox/30.0";
        //    rootrequest.Accept = "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
        //    rootrequest.Headers.Add("Accept-Language: en-US,en;q=0.5");
        //    rootrequest.Headers.Add("Accept-Encoding: gzip,deflate");
        //    rootrequest.Referer = ACD2ICR.Properties.Settings.Default.VMLoginUrl;
        //    rootrequest.KeepAlive = true;
        //    rootrequest.CookieContainer = new CookieContainer();

        //    HttpWebResponse rootresponse = (HttpWebResponse)rootrequest.GetResponse();

        //}

        //private void VMVXViewDo()
        //{
        //    //VMVXView
        //    string txt = string.Empty;
        //    HttpWebRequest viewdorequest = (HttpWebRequest)WebRequest.Create(_vmviewdo);
        //    viewdorequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:30.0) Gecko/20100101 Firefox/30.0";
        //    viewdorequest.Accept = "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
        //    viewdorequest.Headers.Add("Accept-Language: en-US,en;q=0.5");
        //    viewdorequest.Headers.Add("Accept-Encoding: gzip,deflate");
        //    viewdorequest.Referer = ACD2ICR.Properties.Settings.Default.VMVXViewRoot;
        //    viewdorequest.KeepAlive = true;
        //    viewdorequest.CookieContainer = new CookieContainer();

        //    HttpWebResponse viewdoresponse = (HttpWebResponse)viewdorequest.GetResponse();

        //    txt = "Cookies Count=" + viewdoresponse.Cookies.Count.ToString() + "\n";
        //    foreach (Cookie c in viewdoresponse.Cookies)
        //    {
        //        txt += c.ToString() + "\n";
        //    }
        //    MessageBox.Show(txt);
        //}

        //private async void ExtractVoicemails()
        //{
        //    int activetrunccomplete = 0;

        //    // DB Connection 
        //    using (MySqlConnection dbConn = new MySqlConnection("Server=" + ACD2ICR.Properties.Settings.Default.DBServer + ";Database=" + ACD2ICR.Properties.Settings.Default.DBName + ";Uid=" + ACD2ICR.Properties.Settings.Default.DBUser + ";Pwd=" + ACD2ICR.Properties.Settings.Default.DBPass + ";"))
        //    {
        //        await dbConn.OpenAsync();
        //        using (MySqlCommand cmd = new MySqlCommand(ACD2ICR.Properties.Settings.Default.DBTruncateVoicemails, dbConn))
        //        {
        //            activetrunccomplete = await cmd.ExecuteNonQueryAsync();                   
        //        }
        //        dbConn.Close();
        //    }
        //    if (activetrunccomplete == 0) // 0= Success, 1= Fail
        //    {
        //        vmwebclient.DownloadDataAsync(_vmlogin);
        //    }                      
        //}

        //private void stringDataCompletedEvent(object sender, DownloadDataCompletedEventArgs e)
        //{
        //    if (e.Error == null)
        //    {
        //        try
        //        {
        //            byte[] mString = e.Result;
        //            HtmlDocument doc = new HtmlDocument();
        //            doc.Load(new StreamReader(new MemoryStream(mString)));
        //            System.Diagnostics.Debug.WriteLine(doc.DocumentNode.InnerHtml);
        //        }
        //        catch (Exception exp)
        //        {
        //            System.Diagnostics.Debug.WriteLine(exp);
        //        }
        //    }
        //}
        
        #endregion
        
    }
}

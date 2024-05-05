using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace BackupClient
{
    public class Initialize
    {
        private string singleArgs = string.Empty;
        public string plaintextKey, ip, binLoc, xmlLoc = string.Empty;
        public int port, compressionLevel;
        public bool initConnection, hasFailed, backupMode;
        public List<Tuple<string, string>> userData = new List<Tuple<string, string>>(); // list system
        public List<string> excludedItems = new List<string>();// list system
        public Initialize() {
            File.WriteAllText("errorLog.txt", DateTime.Now.ToString() + " - start of log\n");
            PreInitXML xml;
            try
            {
                string[] cliArgs = Environment.GetCommandLineArgs();
                singleArgs = string.Join(" ", cliArgs);
                Regex regex = new Regex("--ip=([0-9]+(?:\\.[0-9]+){3}) --port[:=](\\d+) --initConnection=(\\w+) --xmlLocation=\"(.*?)\" --compressionLevel[:=](\\d+) --backupMode=(\\w+) --plaintextKey=\"(.*?)\" --binLocation=\"(.*?)\""); // args
                Match match = regex.Match(singleArgs);
                if (match.Success)
                {
                    ip = match.Groups[1].Value;
                    port = int.Parse(match.Groups[2].Value);
                    if (match.Groups[3].Value.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        initConnection = true;
                    }
                    xmlLoc = match.Groups[4].Value;
                    compressionLevel = int.Parse(match.Groups[5].Value);
                    if (match.Groups[6].Value.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        backupMode = true;
                    }
                    plaintextKey = match.Groups[7].Value;
                    binLoc = match.Groups[8].Value; 
                } else
                {
                    hasFailed = true;
                }
                if (backupMode)
                {
                    if (Directory.Exists(xmlLoc) || xmlLoc == "default") {
                        if (xmlLoc != "default")
                        {
                            xml = new PreInitXML(xmlLoc,userData,excludedItems);
                        }
                        else
                        {
                            xml = new PreInitXML(userData,excludedItems);
                        }
                    } else
                    {
                        hasFailed = true ;
                    }
                } else
                {
                    if (File.Exists(xmlLoc) || xmlLoc == "default") {
                        if (xmlLoc == "default")
                        {
                            xmlLoc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PortableBackup.xml");
                        }
                    } else
                    {
                        hasFailed = true ;
                    }
                }
                
            } catch
            {
                hasFailed = true;
            }
            
        }
        public  async Task<string> getUser()
        {
            Process getDU = new Process();
            getDU.StartInfo.FileName = "cmd.exe";
            getDU.StartInfo.Arguments = "/c whoami";
            getDU.StartInfo.CreateNoWindow = true;
            getDU.StartInfo.UseShellExecute = false;
            getDU.StartInfo.RedirectStandardOutput = true;
            getDU.Start();
            string currentUser = await getDU.StandardOutput.ReadToEndAsync();
            getDU.WaitForExit();
            return currentUser.Trim();
        }
        public  async Task allowConnection(string user)
        {
            Process addUrlReservation = new Process();
            addUrlReservation.StartInfo.FileName = "netsh";
            addUrlReservation.StartInfo.Arguments = $"http add urlacl url=http://{ip}:{port}/ user={user}";
            addUrlReservation.StartInfo.CreateNoWindow = true;
            addUrlReservation.StartInfo.UseShellExecute = false;
            addUrlReservation.StartInfo.RedirectStandardOutput = true;
            addUrlReservation.Start();
            string output = await addUrlReservation.StandardOutput.ReadToEndAsync();
            addUrlReservation.WaitForExit();
        }
    }

    public class PreInitXML
    {
        protected XmlDocument doc = new XmlDocument();
        protected string docPath = string.Empty;
        protected List<Tuple<string, string>> userData;
        protected List<string> excludedItems;
        public bool hasFailed;

        public PreInitXML(string location, List<Tuple<string, string>> userData, List<string> excludedItems)
        {
            docPath = location;
            this.userData = userData;
            this.excludedItems = excludedItems;
            initLoader();
        }

        public PreInitXML(List<Tuple<string, string>> userData, List<string> excludedItems)
        {
            docPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PortableBackup.xml");
            this.userData = userData;
            this.excludedItems = excludedItems;
            initLoader();
        }

        private void initLoader()
        {
            doc.Load(docPath);
            popData();
        }

        protected virtual void popData()
        {
            XmlNodeList lst = doc.SelectNodes("//User"); // read xml
            foreach (XmlNode node in lst)
            {
                string username = node.SelectSingleNode("Username").InnerText;
                string userDirectory = node.SelectSingleNode("userDirectory").InnerText;
                userData.Add(new Tuple<string, string>(username, userDirectory));
            }
            XmlNodeList exLst = doc.SelectNodes("//ExclusionItems/Item");
            foreach (XmlNode node in exLst)
            {
                string exclusionItem = node.InnerText.Trim('"');
                excludedItems.Add(exclusionItem);
            }
        }
    }

    public class makeUsers : PreInitXML
    {
        List<User> users = new List<User>();

        public makeUsers(List<User> users, string location) : base(location, new List<Tuple<string, string>>(), new List<string>())
        {
            this.users = users;
            popData(); 
        }

        protected override void popData()
        {
            try
            {
                base.popData(); 
                XmlNodeList lst = doc.SelectNodes("//User"); // read xml
                foreach (XmlNode node in lst)
                {
                    User user = new User();
                    user.username = node.SelectSingleNode("Username").InnerText;
                    if (node.SelectSingleNode("isMainUser").InnerText == "true")
                    {
                        user.mainUser = true;
                    }
                    if (node.SelectSingleNode("isFolderUser").InnerText == "true")
                    {
                        user.folderUser = true;
                    }
                    if (node.SelectSingleNode("createThisUser").InnerText == "true")
                    {
                        user.createThisUser = true;
                    }
                    users.Add(user);
                }
            }
            catch
            {
                hasFailed = true;
            }
        }

        
    }
    public class transferFiles : CopySystem
    {
        POSTConnection connect;
        List<string> allItemsToBackup = new List<string>(); // list system
        CopySystem copy = new CopySystem(); 
        User user;
        public transferFiles(POSTConnection connect)
        {
            this.connect = connect;
        }
        static int allItems = 0;
        static int current = 0;
        int prev = -1;
        public async Task PutData(User user)
        {
            this.user = user;
            string userFolder = "C:\\PBWF\\" + user.username;
            if (Directory.Exists(userFolder))
            {
                if (user.mainUser)
                {
                     CopyDirectory("C:\\PBWF\\" + user.username, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),false);
                } else if (user.folderUser)
                {
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string username = user.username; 
                    string userFolderPath = Path.Combine(desktopPath, username);
                    Directory.CreateDirectory(userFolderPath);
                    CopyDirectory("C:\\PBWF\\" + user.username, userFolderPath,false);
                } else if (user.createThisUser)
                {
                    if (!Directory.Exists("C:\\Users\\" + user.username))
                    {
                        runCommand("cmd.exe", $"/c net user /add {user.username} pass & net localgroup administrators {user.username} /add", string.Empty);
                        runCommand("cmd.exe", $"/c runas /user:{user.username} rundll32", "pass");
                        runCommand("cmd.exe", $"/c net user {user.username} \"\"", string.Empty);
                        CopyDirectory("C:\\PBWF\\" + user.username, "C:\\Users\\" + user.username,true);
                    } else
                    {
                        CopyDirectory("C:\\PBWF\\" + user.username, "C:\\Users\\" + user.username,true);
                    }


                }
            }
        }
        
        private void runCommand(string program, string command, string passthrough)
        {
            Process pro = new Process();
            pro.StartInfo.FileName = program;
            pro.StartInfo.Arguments = command;
            pro.StartInfo.CreateNoWindow = true;
            pro.StartInfo.UseShellExecute = false;
            if (!string.IsNullOrEmpty(passthrough))
            {
                pro.StartInfo.RedirectStandardInput = true;
            }
            pro.Start();
            if (!string.IsNullOrEmpty(passthrough))
            {
                pro.StandardInput.Write(passthrough);
                pro.StandardInput.Close();
            }
            pro.WaitForExit();
        }

       private void CopyDirectory(string sourceDir, string targetDir, bool replace) // user made algo
        {
            try
            {
                int totalFiles = Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories).Count();
                int copiedFiles = 0;
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);

                }

                foreach (string file in Directory.GetFiles(sourceDir))
                {
                    string targetFile = Path.Combine(targetDir, Path.GetFileName(file));

                    if (File.Exists(targetFile))
                    {
                        if (replace)
                        {
                            File.Delete(targetFile);
                        } else
                        {
                            string fileName = Path.GetFileNameWithoutExtension(file);
                            string fileExtension = Path.GetExtension(file);
                            string newFileName = $"{fileName} - PB{fileExtension}";
                            targetFile = Path.Combine(targetDir, newFileName);
                        }
                    }

                    File.Copy(file, targetFile); // writing and reading a file
                    copiedFiles++;
                    showProg(copiedFiles, totalFiles);
                }

                foreach (string subdir in Directory.GetDirectories(sourceDir))
                {
                    string targetSubDir = Path.Combine(targetDir, Path.GetFileName(subdir));

                    if (Directory.Exists(targetSubDir))
                    {
                        if (replace)
                        {
                            Directory.Delete(targetSubDir);
                            CopyDirectory(subdir, targetSubDir, replace);// recursive algorithm
                        }
                        else {
                            string newSubDir = $"{targetSubDir} - PB";
                            CopyDirectory(subdir, newSubDir,replace);// recursive algorithm
                        }
                        
                    }
                    else
                    {
                        CopyDirectory(subdir, targetSubDir,replace); // recursive algorithm
                    }

                }
            } catch(Exception ex) { 
            addToLog(ex.Message);
            }
            
        }

        private async void showProg(int current, int total) // user algo
        {
            if (total == 0)
            {
                Console.WriteLine("Error: Total cannot be zero.");
                return; 
            }

            double progress = (double)current / total * 100; 
            int percentage = (int)Math.Round(progress); 

           await connect.POSTSender(percentage.ToString()); 
        }
    }
    public class User // simple oop
    {
        public string username, directory;
        public bool mainUser, folderUser, createThisUser;
        
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace BackupLib
{
    public class User // simple oop
    {
        public string username, directory;
        public bool mainUser, folderUser, createThisUser;
        public User() { 
        
        }
    }
    public class DiskDriveSystem // simple oop
    {
        private List<Tuple<char, int>> disks = new List<Tuple<char, int>>();
        protected char selectedDisk;

        public DiskDriveSystem()
        {
            initDisks();
        }

        public List<Tuple<char, int>> Disks
        {
            get { return disks; }
        }
        
        public void setDisk(char input)
        {
            selectedDisk = input;
        }
        public char getDisk()
        {
            return selectedDisk;
        }
        private void initDisks()
        {
            string[] initDrives = Environment.GetLogicalDrives();
            foreach (string driveL in initDrives)
            {
                int usage = getDrivePercentage(driveL);
                if (usage != -1)
                {
                    disks.Add(new Tuple<char, int>(driveL[0], usage));
                }
            }
        }

        private static int getDrivePercentage(string driveName)
        {
            try
            {
                DriveInfo drive = new DriveInfo(driveName);

                if (!drive.IsReady)
                {
                    return -1;
                }

                double usedSpace = drive.TotalSize - drive.TotalFreeSpace;
                double usagePercentage = (usedSpace / drive.TotalSize) * 100;

                return (int)Math.Round(usagePercentage);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return -1;
            }
        }
        public List<string> userLists (char letter)
        {
            try
            {
                string[] fullPaths = Directory.GetDirectories( letter.ToString() +":\\Users"); 
                return fullPaths.Select(fullPath => Path.GetFileName(fullPath)).Where(dir => !dir.Contains("All") && !dir.Contains("Default") && !dir.Contains("Public")).ToList();
            }
            catch 
            {
               return null;
            }
        }
    }

    public class SendReceive
    {
        protected static string ipAddress;
        protected static int port;
        protected HttpListener listener;
        public SendReceive() {
            string hostName = Dns.GetHostName();
            IPAddress[] addresses = Dns.GetHostAddresses(hostName);
            ipAddress = addresses[0].ToString(); // NOTE you might get IPv6 Address here!
            port = 8080;
        }
        public SendReceive(string ipAddresss, int portt)
        {
            ipAddress = ipAddresss;
            port = portt;   
        }
        public async Task startPOSTServer()
        {
            listener = new HttpListener();
            listener.Prefixes.Add($"http://{ipAddress}:{port}/");
            listener.Start();
            Debug.WriteLine($"Listening on http://{ipAddress}:{port}/!");
        }
        public async Task<string> POSTReceiver()
        {
            while (listener.IsListening)
            {
                HttpListenerContext context = null;
                try
                {
                    context = await listener.GetContextAsync();
                    HttpListenerRequest request = context.Request;
                    string data;
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        data = await reader.ReadToEndAsync();
                    }

                    string responseString = "Ack OK";
                    byte[] responseBytes = Encoding.UTF8.GetBytes(responseString);
                    context.Response.ContentType = "text/plain";
                    context.Response.ContentLength64 = responseBytes.Length;
                    await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);

                    // Check if the received data indicates that the server should stop listening
                    if (data == "endofsend")
                    {
                        break;
                    }
                    return data;
                }
                catch (Exception ex)
                {
                    context?.Response?.Abort();
                    return "error:" + ex.Message;
                }
            }

            return "endofsend";
        }



    }

    public class createScript : User
    {
        string xmlLoc;
        XElement portableBackup;
        public createScript()
        {
            xmlLoc = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            portableBackup = new XElement("PortableBackup");
        }
        public void compileScript()
        {
            try
            {
                if (File.Exists(Path.Combine(xmlLoc, "PortableBackup.xml")))
                {
                    File.Delete(Path.Combine(xmlLoc, "PortableBackup.xml"));
                }
                XDocument doc = new XDocument(portableBackup);
                doc.Save(Path.Combine(xmlLoc, "PortableBackup.xml"));
                Debug.WriteLine("Successfully created the script!");
            } catch (Exception e) 
            {
                Debug.WriteLine("Error: " + e.Message);
            }
        }
        public void addUserToScript(User user)
        {
            portableBackup.Add(
                new XElement("User",
                    new XElement("Username", user.username),
                    new XElement("isMainUser", user.mainUser),
                    new XElement("isFolderUser", user.folderUser),
                    new XElement("createThisUser", user.createThisUser),
                    new XElement("userDirectory", user.directory)
                )
            );
        }
        public void initExclusion()
        {
            portableBackup.Add(new XElement("ExclusionItems"));
        }
        public void addExclusionToScript(string exclusion)
        {
            XElement exclusionItems = portableBackup.Element("ExclusionItems");
            if (exclusionItems != null)
            {
                exclusionItems.Add(new XElement("Item", exclusion));
            }
            else
            {
                Debug.WriteLine("ExclusionItems element is not initialized.");
            }
        }

    }
    public class PreInitXML
    {
        public bool hasFailed;
        XmlDocument doc = new XmlDocument();
        string docPath = string.Empty;
        public List<User> userList;
        public PreInitXML(string location, List<User> userData)
        {
            docPath = location;
            userList = userData;
            initLoader();
        }
        public PreInitXML(List<User> userData)
        {
            docPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PortableBackup.xml");
            userList = userData;
            initLoader();
        }

        private void initLoader()
        {
            try
            {
                doc.Load(docPath);
                popData();
            } catch { 
            hasFailed = true;
            }
            
        }
        private void popData()
        {
            try
            {
                XmlNodeList lst = doc.SelectNodes("//User");
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
                    userList.Add(user);
                }
            } catch
            {
                hasFailed = true;
            }
            
            
        }
    }

}

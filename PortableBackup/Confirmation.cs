using BackupLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace PortableBackup
{
    public partial class Confirmation : Form
    {
        public List<string> excludedFiles { get; set; }
        public List<User> users { get; set; }
        public User mainUser { get; set; }
        bool alreadyChecked, showMessage, makeMainUserSeperate;
        bool showPassword = true;
        int port = 8080;
        private bool closeButtonDisabled = false;
        int compressionLevel = 5;
        string password = "changeme";
        string[] compressionLevels = { "0 - no compression" , "1" , "2", "3", "4", "5 - default", "6","7","8","9 - CPU intensive" };
        public Confirmation()
        {
            InitializeComponent();
        }
        
        private void Confirmation_Load(object sender, EventArgs e)
        {
            richTextBox1.AppendText("You are one step closer to making your backup! You can select if you want your extra users as folders or created and make the main user a folder user or created too. Please remember that some files and users might not be created due to permission problems. Passwords must have a 8 character minimum too!");
            int point = 0;
            checkedListBox1.Items.Add(mainUser.username + " (main)");
            checkedListBox1.SetItemChecked(point, false);
            point++;
            if (users != null) {
                foreach (var user in users)
                {
                    checkedListBox1.Items.Add(user.username);
                    checkedListBox1.SetItemChecked(point, true);
                    point++;
                }
            }
            foreach (string text in excludedFiles)
            {
                richTextBox2.AppendText("- "+text + "\n");
            }
            string hostName = Dns.GetHostName();
            IPAddress[] addresses = Dns.GetHostAddresses(hostName);
            foreach (IPAddress address in addresses)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                   comboBox2.Items.Add(address);
                }
            }
            comboBox2.SelectedIndex = 0;
            textBox2.Text = port.ToString();
            comboBox1.Items.AddRange(compressionLevels);
            comboBox1.SelectedItem = "5 - default";
            textBox3.Text = password;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (password.Length < 8)
            {
                MessageBox.Show("Password must have at least 8 characters!", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } else
            {
                button1.Enabled = false;
                closeButtonDisabled = true;
                if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "POSTDone.txt")))
                {
                    File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "POSTDone.txt"));
                }
                if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ReadyToSend.txt")))
                {
                    File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ReadyToSend.txt"));
                }
                createScript script = new createScript();
                script.addUserToScript(mainUser);
                foreach (User users in users)
                {
                    script.addUserToScript(users);
                }
                script.initExclusion();
                foreach (string dir in excludedFiles)
                {
                    script.addExclusionToScript("\"" + dir + "\"");
                }
                script.compileScript();
                try
                {
                    Process pro = new Process();
                    pro.StartInfo.FileName = "powershell.exe";
                    pro.StartInfo.Arguments = "-Command \"if (dotnet --list-runtimes -OutVariable dotnetOutput | Where-Object { $_ -match 'Microsoft\\.NETCore\\.App 6\\.' }) { Write-Output 'Yes' } else { Write-Output 'No' }\"";
                    pro.StartInfo.CreateNoWindow = true;
                    pro.StartInfo.UseShellExecute = false;
                    pro.StartInfo.RedirectStandardOutput = true;
                    pro.Start();
                    string resp = await pro.StandardOutput.ReadToEndAsync();
                    pro.WaitForExit();
                    if (resp != "Yes")
                    {
                        installNet6();
                    }
                }
                catch
                {
                    installNet6();
                }
                string ipaddress = comboBox2.SelectedItem.ToString();
                SendReceive newReceiver = new SendReceive(ipaddress, port);
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "Client\\BackupClient.exe";
                startInfo.Arguments = "--ip=" + ipaddress + " --port=" + port + " --initConnection=true --xmlLocation=\"\"\"default\"\"\" --compressionLevel=" + compressionLevel + " --backupMode=true --plaintextKey=\"\"\"" + password + "\"\"\"" + " --binLocation=\"\"\"na\"\"\"";
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(startInfo);

                Console.WriteLine("--ip=" + ipaddress + " --port=" + port + " --initConnection=true --xmlLocation=\"\"\"default\"\"\" --compressionLevel=" + compressionLevel + " --backupMode=true --plaintextKey=\"\"\"" + password + "\"\"\"" + " --binLocation=\"\"\"na\"\"\"");
                do
                {
                    System.Threading.Thread.Sleep(1000);
                }
                while (!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "POSTDone.txt")));
                try
                {
                    await newReceiver.startPOSTServer();

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ReadyToSend.txt"), "all ok");
                string result = string.Empty;
                result = await newReceiver.POSTReceiver();
                while (result != "endofsend")
                {
                    result = await newReceiver.POSTReceiver();
                    Debug.WriteLine(result);
                    await showProgress(result);

                }
            }
        }
        private async Task showProgress(string input)
        {
            await Task.Run(() =>
            {
                if (int.TryParse(input, out int result))
                {
                    if (result <= progressBar1.Maximum)
                    {
                        progressBar1.BeginInvoke(new Action(() => progressBar1.Style = ProgressBarStyle.Continuous));
                        progressBar1.BeginInvoke(new Action(() => progressBar1.Value = result));
                        label8.BeginInvoke(new Action(() => label8.Text = result.ToString() + "%"));
                    }
                }
                else
                {
                    switch (input)
                    {
                        default:
                                label7.BeginInvoke(new Action(() => label7.Text = "Progress: Awaiting commands"));
                                progressBar1.BeginInvoke(new Action(() => progressBar1.Style = ProgressBarStyle.Marquee));
                            break;
                        case "copyfiles":
                            label7.BeginInvoke(new Action(() => label7.Text = "Progress: Searching files"));
                            progressBar1.BeginInvoke(new Action(() => progressBar1.Style = ProgressBarStyle.Marquee));
                            break;
                        case "startingbinmake":
                            label7.BeginInvoke(new Action(() => label7.Text = "Progress: Compressing files"));
                            progressBar1.BeginInvoke(new Action(() => progressBar1.Style = ProgressBarStyle.Marquee));
                            break;
                        case "startingencrypt":
                           label7.BeginInvoke(new Action(() => label7.Text = "Progress: Encrypting data"));
                            progressBar1.BeginInvoke(new Action(() => progressBar1.Style = ProgressBarStyle.Marquee));
                            break;
                        case "alldone":
                            label7.BeginInvoke(new Action(() => label7.Text = "Progress: Done"));
                            progressBar1.BeginInvoke(new Action(() => progressBar1.Style = ProgressBarStyle.Continuous));
                            progressBar1.BeginInvoke(new Action(() => progressBar1.Value = progressBar1.Maximum));
                            label8.BeginInvoke(new Action(() => label8.Text = progressBar1.Maximum.ToString() + "%"));
                            break;
                        case "endofsend":
                            MessageBox.Show("Thank you for using PortableBackup! Support me on github.com/eliasailenei", "THANKS", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            closeButtonDisabled = false;
                            Environment.Exit(0);
                            break;
                    }
                }
            });
        }
        private async void installNet6()
        {
            string installerFileName = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\net6.exe";
            try
            {
                string installerUrl = "https://download.visualstudio.microsoft.com/download/pr/3f02cb28-18d2-41d8-a5e3-411aac7b7e5d/69fb6f7f450993f326ead2575ab783d0/windowsdesktop-runtime-6.0.28-win-x64.exe";
                using (var client = new WebClient())
                {
                    client.DownloadFile(installerUrl, installerFileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = installerFileName,
                Arguments = "/passive /norestart",
                UseShellExecute = true,
                Verb = "runas"
            };
            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                }
            }
            catch
            {
                Debug.WriteLine("It somehow failed");
            }
            finally
            {
                File.Delete(installerFileName);
            }
        }
        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(textBox2.Text))
                {
                    port = int.Parse(textBox2.Text);
                }
                
            } catch {
                textBox2.Text = "8080";
                MessageBox.Show("Only put numbers please!","WARNING", MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
            if (port > 65535 || 0 > port)
            {
                textBox2.Text = "8080";
                MessageBox.Show("Unrealistic port number, please give one that's not above 65535 or below 0.","ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            if (showPassword)
            {
                showPassword = false;
            } else
            {
                textBox3.PasswordChar = '*';
            }
            password = textBox3.Text;

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            char item1 = comboBox1.SelectedItem.ToString()[0];
            compressionLevel = int.Parse(item1.ToString());
        }

        private void Confirmation_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!closeButtonDisabled)
            {
                Environment.Exit(0);
            }
        }

        private void checkedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            int index = e.Index;
            bool isChecked = e.NewValue == CheckState.Checked;

            if (index == 0)
            {
                if (isChecked)
                {
                    if (!alreadyChecked)
                    {
                        var response = MessageBox.Show("By checking this, you will create another account for the main user and not extract the files on the current user who will run the backup software. Do you agree?", "WARNING", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (response == DialogResult.Yes)
                        {
                            mainUser.mainUser = false;
                            mainUser.createThisUser = true;
                            mainUser.folderUser = false;
                        }
                        else
                        {
                            var responses = MessageBox.Show("Would you like to extract the files in a folder which will be placed on the Desktop instead?", "WARNING", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (responses == DialogResult.Yes)
                            {
                                mainUser.mainUser = false;
                                mainUser.createThisUser = false;
                                mainUser.folderUser = true;
                            }
                            else
                            {
                                MessageBox.Show("Cannot remove users at this stage! Contents of the main user will still extracted on the current user who will run the program, which is the default option.", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                mainUser.mainUser = true;
                                mainUser.createThisUser = false;
                                mainUser.folderUser = false;
                                alreadyChecked = true;
                               showMessage = true; 
                                e.NewValue = CheckState.Unchecked;
                            }
                        }
                    }
                    else
                    {
                        alreadyChecked = false;
                    }
                } else
                {
                    MessageBox.Show("Main user is at default option.", "WARNING", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    mainUser.mainUser = true;
                    mainUser.createThisUser = false;
                    mainUser.folderUser = false;
                }
            } else
            {
                if (users != null && checkedListBox1.SelectedItem != null && checkedListBox1.Items.Count > 0)
                {
                    foreach (User user in users)
                    {
                        if (user.username == checkedListBox1.SelectedItem.ToString())
                        {
                            if (isChecked)
                            {
                                user.folderUser = false;
                                user.createThisUser = true;
                            } else
                            {
                                user.folderUser = true;
                                user.createThisUser = false;
                            }
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("Skipping");
                }


            }
        }

        private void checkedListBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (showMessage)
            {
                int index = checkedListBox1.IndexFromPoint(e.Location);
                if (index == 0)
                {
                    checkedListBox1.SetItemChecked(0, false); 
                    showMessage = false; 
                }
            }
        }

       

        
    }
}

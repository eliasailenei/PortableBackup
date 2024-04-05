using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using BackupLib;
using System.IO.Compression;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.Net.Http;
namespace PortableBackup
{
    public partial class RestoreBackup : Form
    {
        public string xmlLoc,binLoc,password,ip;
        private bool closeButtonDisabled = false;
        bool hasScript;
        public List<User> userList = new List<User>();
        int port = 8080;

        public RestoreBackup()
        {
            InitializeComponent();
        }

        private void RetoreBackup_Load(object sender, EventArgs e)
        {
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
            textBox5.Text = port.ToString();
            
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(xmlLoc))
            {
                listView1.Items.Clear();
                PreInitXML loadUsers = new PreInitXML(xmlLoc, userList);
                if (!loadUsers.hasFailed)
                {
                    foreach (User user in userList)
                    {
                        ListViewItem newItem = new ListViewItem(new[] { user.username, user.mainUser.ToString(), user.folderUser.ToString(), user.createThisUser.ToString() });
                        listView1.Items.Add(newItem);
                    }
                    if (listView1.Items.Count > 0)
                    {
                        hasScript = true;
                        button2.Enabled = false;
                    }
                    else
                    {
                        MessageBox.Show("The XML file given is not a PortableBackup script. Try again or make a new backup now!","ERROR",MessageBoxButtons.OK,MessageBoxIcon.Error);
                        xmlLoc = string.Empty;
                        textBox1.Text = string.Empty;
                    }
                } else
                {
                    MessageBox.Show("The XML file given is not a PortableBackup script. Try again or make a new backup now!", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    xmlLoc = string.Empty;
                    textBox1.Text = string.Empty;
                }
                
            } else
            {
                MessageBox.Show("No file was selected. Try again or make a new backup now!", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "PB XML files (*.xml)|*.xml";
            openFileDialog.Title = "Select the script made";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                xmlLoc = openFileDialog.FileName;
                textBox1.Text = openFileDialog.FileName;
            }
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            ip = comboBox2.SelectedItem.ToString();
        }

        private async void button5_Click(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(password) && password.Length >= 8 && !string.IsNullOrEmpty(binLoc) && hasScript && File.Exists(binLoc))
                {
                    button5.Enabled = false;
                    closeButtonDisabled = true;
                    string toPass = "--ip=" + ip + " --port=" + port + " --initConnection=true --xmlLocation=\"\"\"" + xmlLoc + "\"\"\" --compressionLevel=0 --backupMode=false --plaintextKey=\"\"\"" + password + "\"\"\"" + " --binLocation=\"\"\"" + binLoc + "\"\"\"";
                    if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "POSTDone.txt")))
                    {
                        File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "POSTDone.txt"));
                    }
                    if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ReadyToSend.txt")))
                    {
                        File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ReadyToSend.txt"));
                    }
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
                    startInfo.Arguments = toPass;
                    startInfo.UseShellExecute = true;
                    startInfo.Verb = "runas";
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    Process.Start(startInfo);

                    Console.WriteLine(toPass);
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
                else
                {
                    MessageBox.Show("One or more fields are empty/wrong. Remember that passwords must be at least 8 characters", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                }
            } catch (Exception ex)
            {
                    MessageBox.Show("Unknown error: " + ex.Message + "\n\n That's all we know, try to make a new backup instead.", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                closeButtonDisabled = false;
                this.Close();
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
                        case "initextractbackup":
                            label7.BeginInvoke(new Action(() => label7.Text = "Progress: Starting Backup"));
                            progressBar1.BeginInvoke(new Action(() => progressBar1.Style = ProgressBarStyle.Marquee));
                            break;
                        case "startingbindecrpyt":
                            label7.BeginInvoke(new Action(() => label7.Text = "Progress: Decrypting Backup"));
                            progressBar1.BeginInvoke(new Action(() => progressBar1.Style = ProgressBarStyle.Marquee));
                            break;
                        case "startingbinextract":
                            label7.BeginInvoke(new Action(() => label7.Text = "Progress: Extracting Backup"));
                            progressBar1.BeginInvoke(new Action(() => progressBar1.Style = ProgressBarStyle.Marquee));
                            break;
                        case "makingusers":
                            label7.BeginInvoke(new Action(() => label7.Text = "Progress: Applying Users"));
                            progressBar1.BeginInvoke(new Action(() => progressBar1.Style = ProgressBarStyle.Marquee));
                            break;
                        case "alldone":
                            label7.BeginInvoke(new Action(() => label7.Text = "Progress: Done"));
                            progressBar1.BeginInvoke(new Action(() => progressBar1.Style = ProgressBarStyle.Continuous));
                            progressBar1.BeginInvoke(new Action(() => progressBar1.Value = progressBar1.Maximum));
                            label8.BeginInvoke(new Action(() => label8.Text = progressBar1.Maximum.ToString() + "%"));
                            break;
                        case "endofsend":
                            MessageBox.Show("Thank you for using PortableBackup! Support me on github.com/eliasailenei", "THANKS", MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
                            closeButtonDisabled = false;
                            Environment.Exit(0);
                            break;
                        case "wrongpasswordgiven":
                            MessageBox.Show("The wrong password was given for the backup, try again with the correct password this time!", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            break;
                        case "unknowndecrypterror":
                            MessageBox.Show("Failed to decrypt bin file, this might be due to a corrupt file, try again with the correct password this time!", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            } catch (Exception ex)
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

        private void button4_Click(object sender, EventArgs e)
        {
            string message = "Q: Can I use any regular .bin files?\nA: No, even if you rename it will not work. All PortableBackup .bin files are encrypted with DES and has a strict folder structure.\n\n" +
                             "Q: My program freezes when using .bin file from a network location.\nA: This is due to permission problems, please copy and paste the backup and the script onto the desktop and use it there.\n\n" +
                             "Q: I don't know the password / what password are you talking about?\nA: For security, you must use a 8-charater password. If you kept the default, the password is changeme\n\n" +
                             "Q: My new user wasn't properly created.\nA: This is seen when you make a backup in Windows 11 to Windows 10. Its mainly just the AppData folder, so you can add that to the exclusion and it should work. You can also just drag and drop the files once you have logged in manually.\n\n" +
                             "Any bugs or questions? Ask it at github.com/eliasailenei";

            MessageBox.Show(message, "Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(textBox2.Text))
                {
                    port = int.Parse(textBox2.Text);
                }
            }
            catch
            {
                textBox2.Text = "8080";
                MessageBox.Show("Only put numbers please!", "WARNING", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (port > 65535 || 0 > port)
            {
                textBox2.Text = "8080";
                MessageBox.Show("Unrealistic port number, please give one that's not above 65535 or below 0.", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreBackup_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (!closeButtonDisabled)
            {
                this.Close();
            }
            
        }

        private void label8_Click(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            password = textBox3.Text;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "PB BIN Backup files (*.bin)|*.bin";
            openFileDialog.Title = "Select the backup made";
            if (openFileDialog.ShowDialog() == DialogResult.OK && openFileDialog.FileName.Contains("PB"))
            {
                binLoc = openFileDialog.FileName;
                textBox2.Text = openFileDialog.FileName;
            } else
            {
                MessageBox.Show("Only provide BIN files made by PortableBackup! They all have PB at the end!", "WARNING", MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
        }
    }
}

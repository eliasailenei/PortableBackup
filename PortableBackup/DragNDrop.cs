using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using BackupLib;
using System.Net.NetworkInformation;
namespace PortableBackup
{
    public partial class DragNDrop : Form
    {
        public List<User> users {  get; set; }
        private List<string> acceptedDir { get; set; }
        private List<string> allExceptions = new List<string>(); 
        public User mainUser {  get; set; }
        public DragNDrop()
        {
            InitializeComponent();
        }

        private void DragNDrop_Load(object sender, EventArgs e)
        {
            richTextBox1.AppendText("By default, the program will backup all the folders and files from the selected user (even junk). You can drop the files and folders you don't want here. Here are some exceptions:\n- Item must be from the User folder\n- Item must be a file or folder\n- Files must not be from existing folders e.g cannot do C:\\Users\\Jake\\Desktop\\screenshot.png if C:\\Users\\Jake\\Desktop\\ is present\n- You can't put your User folder e.g If user Jake was selected you can't put C:\\Users\\Jake\\");
            acceptedDir = new List<string>();
            if (users != null)
            {
                foreach (User user in users)
                {
                    acceptedDir.Add(user.directory);
                }
            }
            acceptedDir.Add(mainUser.directory);
        }

        private void listBox1_DragDrop(object sender, DragEventArgs e)
        {
            string[] droppedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (string fileOrFolder in droppedFiles)
            {
                
                if (Directory.Exists(fileOrFolder) || File.Exists(fileOrFolder))
                {
                    bool isValidPath = false;
                    foreach (string acceptedPath in acceptedDir)
                    {
                        if (fileOrFolder == acceptedPath)
                        {
                            isValidPath = false;
                            break;
                        }
                        if (fileOrFolder.StartsWith(acceptedPath, StringComparison.OrdinalIgnoreCase))
                        {

                            isValidPath = true;
                            break;
                        }
                        
                    }
                    if (listBox1.Items.Count > 0)
                    {
                        foreach(string acceptedPath in listBox1.Items)
                        {
                            if (fileOrFolder.StartsWith(acceptedPath, StringComparison.OrdinalIgnoreCase))
                            {
                                isValidPath= false;
                                break;
                            } else if (acceptedPath.StartsWith(fileOrFolder, StringComparison.OrdinalIgnoreCase))
                            {
                                isValidPath = false;
                                break;
                            }
                            else
                            {
                                isValidPath =true;
                            }
                        }
                    }
                    
                    if (isValidPath)
                    {
                        listBox1.Items.Add(fileOrFolder);
                    }
                    else
                    {
                        MessageBox.Show("The item(s) added goes against exclusion rules, maybe you uploaded a file that isn't in your user folder.", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }


        private void listBox1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                listBox1.Items.Remove(listBox1.SelectedItem);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Confirmation confirm = new Confirmation();
            foreach (var item in listBox1.Items)
            {
                allExceptions.Add(item.ToString()); 
            }
            confirm.excludedFiles = allExceptions;
            confirm.mainUser = mainUser;
            confirm.users = users;
            this.Close();
            confirm.Show();
        }

        private void DragNDrop_FormClosing(object sender, FormClosingEventArgs e)
        {
            
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BackupLib;
namespace PortableBackup
{
    public partial class NewBackup : Form
    {
        DiskDriveSystem disk = new DiskDriveSystem();
        User mainUser = new User();
        string[] addUserList;
        List<Tuple<char, int>> diskList;
        List<User> userList = new List<User>();
        char selectedDisk;
        bool InMainMode;
        private Form1 form1;
        public NewBackup()
        {
            diskList = disk.Disks;
            InitializeComponent();
            form1 = new Form1();
            form1.FormClosed += (sender, e) => this.Close();
        }

        private void NewBackup_Load(object sender, EventArgs e)
        {
            richTextBox1.AppendText("To begin, click the \"Assign main\" button to select the main user e.g after performing a clean install of Windows, the files of the main backup user will be extracted onto the first user created after install. You can also separate the main user from the actual user during backup. You also have the option to import your other users as well too.");
            ImageList imageList = new ImageList();
            System.Net.WebRequest request = System.Net.WebRequest.Create("https://raw.githubusercontent.com/eliasailenei/PortableISO/main/Videos/users.png");
            System.Net.WebResponse resp = request.GetResponse();
            System.IO.Stream respStream = resp.GetResponseStream();
            Bitmap bmp = new Bitmap(respStream);
            respStream.Dispose();
            imageList.Images.Add("userIcon", bmp);
            imageList.ImageSize = new Size(64, 64);
            imageList.ColorDepth = ColorDepth.Depth32Bit;
            listView1.LargeImageList = imageList;
            foreach (var item in diskList)
            {
                if (Directory.Exists(item.Item1.ToString() + ":\\Users"))
                {
                    comboBox1.Items.Add(item.Item1 + ": " + item.Item2 + "% free");
                }
            }
            comboBox1.SelectedIndex = 0;
        }

        private void setUsers (char letter)
        {
            List<string> userLists = disk.userLists(letter);
            foreach (string path in userLists)
            {
                ListViewItem item = new ListViewItem(path);
                item.ImageKey = "userIcon";
                item.ImageIndex = 0;
                listView1.Items.Add(item);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            refreshAll();
            if (Directory.Exists(comboBox1.SelectedItem.ToString()[0] + ":\\Users"))
            {
                selectedDisk = comboBox1.SelectedItem.ToString()[0];
                setUsers(selectedDisk);
            }
            else
            {
                MessageBox.Show("Choose a drive where there is a Windows install present!","ERROR",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        }
        private void refreshAll()
        {
            listView1.Clear();
            listBox1.Items.Clear();
            addUserList = new string[0];
            mainUser = new User();
            textBox1.Text = string.Empty;   
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (!InMainMode)
            {
                comboBox1.Enabled = false;
                button1.Text = "Turn off selector";
                InMainMode = true;
            } else
            {
                comboBox1.Enabled = true;
                button1.Text = "Assign main user";
                InMainMode = false;
            }
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                ListViewItem items = listView1.SelectedItems[0];
                if (InMainMode)
                {
                    if (addUserList == null) 
                    {
                        addUserList = new string[] { string.Empty }; //placeholder
                    }
                        if (!addUserList.Contains(items.Text)) 
                        {
                            mainUser.username = items.Text;
                            textBox1.Text = mainUser.username;
                            mainUser.mainUser = true;
                        mainUser.folderUser = false;
                        mainUser.createThisUser = false;
                        mainUser.directory = selectedDisk.ToString() + ":\\Users\\" + mainUser.username;
                        }
                        else
                        {
                            MessageBox.Show("Main user cannot be an additional user! Remove user first and then select them as the main user!", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    addUserList = addUserList.Where(x => x != string.Empty).ToArray(); //remove placeholder
                }
                else
                {
                    if (items.Text != mainUser.username)
                    {
                        if (addUserList == null)
                        {
                            addUserList = new string[] { items.Text }; 
                        }
                        else
                        {
                            if (addUserList.Contains(items.Text))
                            {
                                addUserList = addUserList.Where(item => item != items.Text).ToArray();
                            }
                            else
                            {
                                addUserList = addUserList.Concat(new string[] { items.Text }).ToArray();
                            }
                        }
                        listBox1.Items.Clear();
                        listBox1.Items.AddRange(addUserList.Where(item => item != null).ToArray());
                    }
                    else
                    {
                        MessageBox.Show("Main user is already selected!", "WARNING", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private void NewBackup_FormClosing(object sender, FormClosingEventArgs e)
        {
            
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (addUserList != null)
            {
                foreach (string user in addUserList)
                {
                    User addUser = new User();
                    addUser.username = user;
                    addUser.mainUser = false;
                    addUser.folderUser = false;
                    addUser.createThisUser = true;
                    addUser.directory = selectedDisk.ToString() + ":\\Users\\" + addUser.username;
                    userList.Add(addUser);
                }
            }
           
            if (string.IsNullOrEmpty(mainUser.username))
            {
                MessageBox.Show("No main user detected! Please select a main user.", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } else
            {
                DragNDrop drag = new DragNDrop();
                drag.users = userList;
                drag.mainUser = mainUser;
                drag.Show();
                this.Close();
            }
            
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }
    }
}

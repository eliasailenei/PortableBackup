using BackupClient;
using System;

public class MainProgram
{
    
    private static async Task Main()
    {
        if (File.Exists("C:\\nocrypt.bin"))
        {
            File.Delete("C:\\nocrypt.bin");
        }
        Initialize initialize = new Initialize();
        POSTConnection connect = new POSTConnection(initialize.ip, initialize.port); // simple oop

        if (initialize.hasFailed)
        {
            Console.WriteLine("Invalid input! Program is not standalone, please use PortableBackup!");
            Console.ReadLine();
            System.Environment.Exit(0);
        }

        if (initialize.initConnection)
        {
            string currentUser = await initialize.getUser();
            await initialize.allowConnection(currentUser);
            File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "POSTDone.txt"), "all ok"); //  writing and reading a file
        }
        do
        {
            System.Threading.Thread.Sleep(1000);
        }
        while (!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ReadyToSend.txt")));
        await connect.POSTSender("success");
        if (initialize.backupMode)
        {
            await connect.POSTSender("initnewbackup");
            MakeBackup newBackup = new MakeBackup(initialize.userData, initialize.excludedItems, connect);
            await connect.POSTSender("copyfiles");
            int progress = await newBackup.MakeList();
            if (progress == 100)
            {
                await connect.POSTSender("startingbinmake");
                BackupSystem backup = new BackupSystem(connect);
                backup.CreateBinFile("C:\\PBWF", "C:\\nocrypt.bin", initialize.compressionLevel); // writing and reading a file
                Directory.Delete("C:\\PBWF", true);
                string currentTime = DateTime.Now.ToString("dd-MM-yyyy HH-mm-ss tt");
                string fileName = currentTime + " PB.bin";
                await connect.POSTSender("startingencrypt");
                await backup.EncryptDecryptFile("C:\\nocrypt.bin", "C:\\" + fileName, initialize.plaintextKey, true);
                File.Delete("C:\\nocrypt.bin");
                await connect.POSTSender("alldone");
                await connect.POSTSender("endofsend");
                Environment.Exit(0);
            }
        } else
        {
            List<User> users1 = new List<User>(); // use of list system
            if (Directory.Exists("C:\\PBWF"))
            {
                Directory.Delete("C:\\PBWF", true);
            }
            Directory.CreateDirectory("C:\\PBWF");
            await connect.POSTSender("initextractbackup");
            await connect.POSTSender("startingbindecrpyt");
            BackupSystem newBackup = new BackupSystem(connect); // simple oop
            try
            {
                await newBackup.EncryptDecryptFile(initialize.binLoc, "C:\\decrypted.bin", initialize.plaintextKey, false);
            }
            catch (Exception ex)
            {
                if (ex is System.Security.Cryptography.CryptographicException)
                {
                    await connect.POSTSender("wrongpasswordgiven");
                    File.Delete("C:\\decrypted.bin");
                    Environment.Exit(0);
                }
                else
                {
                    await connect.POSTSender("unknowndecrypterror");
                    if (File.Exists("C:\\decrypted.bin"))
                    {
                        File.Delete("C:\\decrypted.bin");
                    }
                    Environment.Exit(0);
                }
            }
            await connect.POSTSender("startingbinextract");
            await newBackup.ExtractBinFile("C:\\decrypted.bin", "C:\\PBWF");
            File.Delete("C:\\decrypted.bin");
            await connect.POSTSender("makingusers");
            makeUsers users = new makeUsers(users1, initialize.xmlLoc);
            transferFiles transfer = new transferFiles(connect);
            foreach (User user in users1)
            {
                await transfer.PutData(user);
            }
            Directory.Delete("C:\\PBWF", true);
            await connect.POSTSender("alldone");
            await connect.POSTSender("endofsend");
        }
    }

}

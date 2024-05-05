using System;
using System.Collections.Generic;
using System.IO;

namespace BackupClient
{
    internal class MakeBackup
    {
        CopySystem copy = new CopySystem();
        private List<Tuple<string, string>> userData; // list system
        private List<string> excludedItems; // list system
        public List<string> allItemsToBackup = new List<string>(); // list system
        private List<string> initFoldersToAdd = new List<string>(); // list system
        private POSTConnection connect;


        public MakeBackup(List<Tuple<string, string>> userData, List<string> excludedItems, POSTConnection connect)
        {
            if (File.Exists("errorLog.txt"))
            {
                File.Delete("errorLog.txt");
            }
            this.userData = userData;
            this.excludedItems = excludedItems;
            createInitFolders();
            this.connect = connect;
        }

        private void createInitFolders()
        {
            initFoldersToAdd.Add("C:\\PBWF");
            foreach (var item in userData)
            {
                initFoldersToAdd.Add("C:\\PBWF\\" + item.Item1);
            }
            foreach (string dirs in initFoldersToAdd)
            {
                if (Directory.Exists(dirs))
                {
                    Directory.Delete(dirs, true);
                }
                Directory.CreateDirectory(dirs); // writing and reading a file
            }
        }

        public async Task<int> MakeList()
        {
            int totalProgress = 0;
            int numUsers = userData.Count;
            foreach (var item in userData)
            {
                BackupUserFolder(item.Item2); // recursive algo

                foreach (var excludedItem in excludedItems)
                {
                    allItemsToBackup.RemoveAll(item => item.Contains(excludedItem));
                }
                List<string> itemsToRemove = new List<string>();
                foreach (var folder in allItemsToBackup)
                {
                    foreach (var existingItem in allItemsToBackup)
                    {
                        if (existingItem != folder && existingItem.StartsWith(folder))
                        {
                            itemsToRemove.Add(folder);
                            break;
                        }
                    }
                }
                foreach (var itemToRemove in itemsToRemove)
                {
                    allItemsToBackup.Remove(itemToRemove);
                }
                int progress = await copyList("C\\PBWF\\" + item.Item1);
                totalProgress += progress;
                allItemsToBackup.Clear();
            }
            int averageProgress = 0;
            try
            {
                 averageProgress = totalProgress / numUsers; // simple math

            } catch (Exception ex) 
            {
                if (ex is DivideByZeroException)
                {
                    await connect.POSTSender("zeroerror");
                }
            }
                return averageProgress;
        }


        private void BackupUserFolder(string folder)
        {
            try
            {
                string[] files = Directory.GetFiles(folder);
                string[] subDirectories = Directory.GetDirectories(folder);

                foreach (string file in files)
                {
                    if (!IsExcluded(file))
                    {
                        allItemsToBackup.Add(file);
                    }
                }

                if (!IsExcluded(folder))
                {
                    allItemsToBackup.Add(folder);
                }

                foreach (string subDirectory in subDirectories)
                {
                    BackupUserFolder(subDirectory); // recursive algo
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                copy.addToLog($"Unauthorised access error, couldn't backup: {folder}");
                excludedItems.Add(folder);
            }
        }
        private bool IsExcluded(string path)
        {
            foreach (string exclusion in excludedItems)
            {
                if (path.Contains(exclusion))
                {
                    return true;
                }
            }
            return false;
        }

        int prev = -1;
        public async Task<int> copyList(string destinationFolder)
        {
            CopySystem copy = new CopySystem();
            int totalFiles = allItemsToBackup.Count;
            int filesProcessed = 0;
            int progress = -1;

            foreach (string sourcePath in allItemsToBackup)
            {
                string destinationPath = sourcePath.Replace("C:\\Users\\", "C:\\PBWF\\");

                if (File.Exists(sourcePath))
                {
                    await copy.CopyFileAsync(sourcePath, destinationPath);
                }
                else if (Directory.Exists(sourcePath))
                {
                    await copy.CopyFolderAsync(sourcePath, destinationPath);
                }

                filesProcessed++;
                progress = (int)((double)filesProcessed / totalFiles * 100);
                
                if (progress != prev)
                {
                    prev = progress;
                   await connect.POSTSender(progress.ToString());
                }
                
            }

            return progress; 
        }
        

    }
}

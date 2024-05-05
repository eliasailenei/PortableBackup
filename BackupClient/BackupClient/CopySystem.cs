using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupClient
{
    public class CopySystem
    {
        public virtual async Task CopyFileAsync(string sourceFilePath, string destinationFilePath) // user made algo
        {
            try
           {
                string fileName = "\\" + Path.GetFileName(sourceFilePath);
                if (!Directory.Exists(Path.GetDirectoryName(destinationFilePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));
                }

                using (FileStream sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
                using (FileStream destinationStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                {
                    await sourceStream.CopyToAsync(destinationStream);// writing and reading a file
                }
            }
            catch (Exception ex)
            {
               addToLog("Couldn't move file " + sourceFilePath + " to " + destinationFilePath + ". Error: " + ex.Message);
            }
        }


        public virtual async Task CopyFolderAsync(string sourceFolder, string destinationFolder)
        {
            try
            {
                DirectoryInfo sourceDirectory = new DirectoryInfo(sourceFolder);
                DirectoryInfo destinationDirectory = Directory.CreateDirectory(destinationFolder);

                foreach (var sourceItem in sourceDirectory.GetFileSystemInfos())
                {
                    string destinationPath = Path.Combine(destinationFolder, sourceItem.Name);

                    if (sourceItem is FileInfo file)
                    {
                        await CopyFileAsync(file.FullName, destinationPath); // recursive algorithm
                    }
                    else if (sourceItem is DirectoryInfo directory)
                    {
                        await CopyFolderAsync(directory.FullName, destinationPath);// recursive algorithm
                    }
                }
            }
            catch
           {
                addToLog("Couldn't move file " + sourceFolder + " to " + destinationFolder);
            }

        }
        public void addToLog(string text)
        {
            try
            {
                string dateTimeString = DateTime.Now.ToString();
                string line = $"{dateTimeString}: {text}";

                using (StreamWriter sw = File.AppendText("errorLog.txt"))
                {
                    sw.WriteLine(line);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error appending to file: " + ex.Message);
            }
        }
    }
}

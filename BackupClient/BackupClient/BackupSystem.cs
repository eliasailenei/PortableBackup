using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

namespace BackupClient
{
    internal class BackupSystem
    {
        private long totalBytesProcessed = 0;
        private long totalBytes;
        private int prev;
        private POSTConnection connect;

        public BackupSystem(POSTConnection connect)
        {
            this.connect = connect;
        }
       public void CreateBinFile(string sourceFolder, string outputFile, int compressionLevel)// user made algo
        {
            using (var fsOut = File.Create(outputFile))
            using (var zipStream = new ZipOutputStream(fsOut))
            {
                zipStream.SetLevel(compressionLevel);

                CompressFolder(sourceFolder, zipStream, sourceFolder.Length, async (progress) =>
                {
                   await connect.POSTSender(progress.ToString());
                });

                zipStream.IsStreamOwner = true;
                zipStream.Close();
            }

            Console.WriteLine("Backup created successfully.");
        }

        static void CompressFolder(string path, ZipOutputStream zipStream, int folderOffset, Action<int> progressCallback, int progress = 0)// user made algo
        {
            string[] files = Directory.GetFiles(path);
            string[] subfolders = Directory.GetDirectories(path);

            int totalFiles = files.Length + subfolders.Length;
            int processedFiles = 0;

            foreach (string file in files)
            {
                FileInfo fi = new FileInfo(file);
                string entryName = file.Substring(folderOffset);
                entryName = ZipEntry.CleanName(entryName);

                ZipEntry newEntry = new ZipEntry(entryName);
                newEntry.DateTime = fi.LastWriteTime;
                newEntry.Size = fi.Length;

                zipStream.PutNextEntry(newEntry);

                byte[] buffer = new byte[4096];
                using (FileStream streamReader = File.OpenRead(file))
                {
                    StreamUtils.Copy(streamReader, zipStream, buffer); // reading and writing files
                }
                zipStream.CloseEntry();

                processedFiles++;
                int currentProgress = (int)(((double)processedFiles / totalFiles) * 100);
                progressCallback(currentProgress);
            }

            foreach (string subfolder in subfolders)
            {
                CompressFolder(subfolder, zipStream, folderOffset, progressCallback, progress);
                processedFiles++;
                int currentProgress = (int)(((double)processedFiles / totalFiles) * 100);
                progressCallback(currentProgress);
            }
        }



        public async Task ExtractBinFile(string inputFile, string outputFolder)// user made algo
        {
            int prev = 0;
            Console.WriteLine("Extracting backup...");
            int totalProgress = 0;
            long totalFileSize = new FileInfo(inputFile).Length;

            using (FileStream fsIn = new FileStream(inputFile, FileMode.Open, FileAccess.Read)) // writing and reading a file
            using (ZipInputStream zipStream = new ZipInputStream(fsIn))
            {
                ZipEntry entry;
                long totalBytesProcessed = 0;
                int totalFiles = 0;
                int filesProcessed = 0;

                while ((entry = zipStream.GetNextEntry()) != null)
                {
                    totalFiles++;
                }

                zipStream.Close(); 

                using (FileStream fsIn2 = new FileStream(inputFile, FileMode.Open, FileAccess.Read)) //writing and reading afile
                using (ZipInputStream zipStream2 = new ZipInputStream(fsIn2))
                {
                    while ((entry = zipStream2.GetNextEntry()) != null)
                    {
                        string entryFileName = Path.Combine(outputFolder, entry.Name);
                        string entryDirectory = Path.GetDirectoryName(entryFileName);

                        if (!Directory.Exists(entryDirectory))
                            Directory.CreateDirectory(entryDirectory);

                        using (FileStream fsOut = new FileStream(entryFileName, FileMode.Create, FileAccess.Write))
                        {
                            byte[] buffer = new byte[4096];
                            int bytesRead;
                            long bytesProcessedThisFile = 0;

                            while ((bytesRead = zipStream2.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                fsOut.Write(buffer, 0, bytesRead);
                                bytesProcessedThisFile += bytesRead;
                                totalBytesProcessed += bytesRead;
                            }
                            
                            totalProgress = (int)((filesProcessed / (double)totalFiles) * 100);
                            filesProcessed++;
                            if (totalProgress != prev)
                            {
                                prev = totalProgress;
                                await connect.POSTSender(totalProgress.ToString());
                            }
                            
                        }
                    }
                }
            }
            await connect.POSTSender("100");
        }


        public async Task EncryptDecryptFile(string inputFile, string outputFile, string keyString, bool encrypt)//user made algo
        {
            int prev = 0;
            byte[] keyBytes = Encoding.UTF8.GetBytes(keyString);
            byte[] truncatedKey = new byte[8];
            Array.Copy(keyBytes, truncatedKey, Math.Min(keyBytes.Length, 8));
            using (DESCryptoServiceProvider desProvider = new DESCryptoServiceProvider())
            {
                desProvider.Key = truncatedKey;
                desProvider.IV = new byte[8];

                ICryptoTransform transform = encrypt ? desProvider.CreateEncryptor() : desProvider.CreateDecryptor();

                using (FileStream inputStream = File.Open(inputFile, FileMode.Open, FileAccess.Read))
                using (FileStream outputStream = File.Open(outputFile, FileMode.Create, FileAccess.Write))
                using (CryptoStream cryptoStream = new CryptoStream(outputStream, transform, CryptoStreamMode.Write))// reading and writing a file
                {
                    long totalBytesProcessed = 0;
                    long fileSize = inputStream.Length;
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await cryptoStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesProcessed += bytesRead;
                        int progress = (int)((double)totalBytesProcessed / fileSize * 100);
                        if (progress != prev)
                        {
                            prev = progress;
                            await connect.POSTSender(progress.ToString());
                        }
                    }
                }
            }
        }
    }
}

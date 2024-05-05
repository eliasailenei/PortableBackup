using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BackupClient
{
    public class POSTConnection
    {
        private string ip;
        private int port;

        public POSTConnection(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }

        public async Task<int> POSTSender(string message)
        {
            Console.WriteLine(message);
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = $"http://{ip}:{port}";
                    StringContent content = new StringContent(message, Encoding.UTF8, "text/plain");
                    HttpResponseMessage response = await client.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine("Response Content: " + responseContent);
                        return 200;
                    }
                    else
                    {
                        return (int)response.StatusCode;
                    }
                }
            }
            catch
            {
                return -1;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using VisiPlacement;

namespace TopicFilterer
{
    class UrlDownloader : ValueProvider<String>
    {
        public UrlDownloader(WebClient webClient, String url)
        {
            this.webClient = webClient;
            this.url = url;
        }

        public String Get()
        {
            try
            {
                byte[] data = this.webClient.DownloadData(this.url);
                string text = System.Text.Encoding.UTF8.GetString(data);
                System.Diagnostics.Debug.Write("From " + this.url + ", downloaded data of " + text);
                return text;
            }
            catch (WebException e)
            {
                System.Diagnostics.Debug.WriteLine("Failed to open " + this.url + ", " + e);
            }
            return null;
        }
        private WebClient webClient;
        private string url;


    }
}

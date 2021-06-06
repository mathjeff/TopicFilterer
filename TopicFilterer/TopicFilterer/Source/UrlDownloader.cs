using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using VisiPlacement;

namespace TopicFilterer
{
    class UrlDownloader
    {
        public UrlDownloader(WebClient webClient, string url)
        {
            this.webClient = webClient;
            this.url = url;
        }

        public string Get()
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
        public string Url
        {
            get
            {
                return this.url;
            }
        }
        private WebClient webClient;
        private string url;


    }
}

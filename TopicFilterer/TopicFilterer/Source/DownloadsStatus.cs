using System;
using System.Collections.Generic;
using System.Text;

namespace TopicFilterer
{
    class DownloadsStatus
    {
        public event UpdatedHandler Updated;
        public delegate void UpdatedHandler();

        public DownloadsStatus(int NumDownloads)
        {
            this.NumUrlsLeftToDownload = NumDownloads;
            this.NumToDownload = NumDownloads;
        }
        public int NumUrlsDownloadedButNotShown
        {
            get
            {
                return this.numUrlsDownloadedButNotShown;
            }
            set
            {
                this.numUrlsDownloadedButNotShown = value;
                if (this.Updated != null)
                    this.Updated.Invoke();
            }
        }
        public int NumUrlsLeftToDownload
        {
            get
            {
                return this.numUrlsLeftToDownload;
            }
            set
            {
                this.numUrlsLeftToDownload = value;
                if (this.Updated != null)
                    this.Updated.Invoke();
            }
        }
        public int NumToDownload;
 
        public int NumFailed
        {
            get
            {
                return this.numFailed;
            }
            set
            {
                this.numFailed = value;
                if (this.Updated != null)
                    this.Updated.Invoke();
            }
        }
        
        public string SampleFailure
        {
            get
            {
                return this.sampleFailure;
            }
            set
            {
                this.sampleFailure = value;
                if (this.Updated != null)
                    this.Updated.Invoke();
            }
        }

        private int numUrlsDownloadedButNotShown;
        private int numUrlsLeftToDownload;
        private int numFailed;
        private string sampleFailure;
    }
}

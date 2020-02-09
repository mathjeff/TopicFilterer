using System;
using System.Collections.Generic;
using System.Text;

namespace TopicFilterer
{
    class DownloadsStatus
    {
        public DownloadsStatus(int NumDownloads)
        {
            this.NumUrlsLeftToDownload = NumDownloads;
            this.NumToDownload = NumDownloads;
        }
        public int NumUrlsDownloadedButNotShown;
        public int NumUrlsLeftToDownload;
        public int NumToDownload;
    }
}

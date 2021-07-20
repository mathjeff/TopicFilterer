using System;
using System.Collections.Generic;
using System.Text;
using VisiPlacement;
using Xamarin.Forms;

namespace TopicFilterer.View
{
    class PostsView : ContainerLayout
    {
        public delegate void PostStarred_Handler(PostInteraction post);
        public event PostStarred_Handler PostStarred;

        public delegate void PostClicked_Handler(PostInteraction post);
        public event PostClicked_Handler PostClicked;

        public delegate void RequestUpdate_Handler(PostsView postsView);
        public event RequestUpdate_Handler RequestUpdate;

        public PostsView(DownloadsStatus downloadsStatus)
        {
            this.downloadsStatus = downloadsStatus;
            this.downloadsStatus.Updated += DownloadsStatus_Updated;

            this.setupUpdateLayout();

            this.nextButton = new Button();
            this.nextButton_layout = new ButtonLayout(this.nextButton, "More", 16);
            this.nextButton.Clicked += NextButton_Clicked;
        }

        private void NextButton_Clicked(object sender, EventArgs e)
        {
            this.displayIndex += this.pageSize;
            this.update();
        }

        public List<AnalyzedPost> Posts
        {
            set
            {
                this.posts = value;
                this.displayIndex = 0;
                this.update();
            }
        }
        private void setupUpdateLayout()
        {
            this.cannotUpdate_layout = new TextblockLayout("", 32);
            this.cannotUpdate_layout.setTextColor(Color.White);
            this.cannotUpdate_layout.setBackgroundColor(Color.Black);
            this.updateButton = new Button();
            this.updateButton.Clicked += UpdateButton_Clicked;
            this.updateButton_layout = new ButtonLayout(this.updateButton, "", 32, true, false, false, true);
            this.downloadStatus_container = new ContainerLayout();
            this.update_numCompletedDownloads_status();
            this.Posts = new List<AnalyzedPost>();
        }

        private void UpdateButton_Clicked(object sender, EventArgs e)
        {
            this.RequestUpdate.Invoke(this);
        }

        private void DownloadsStatus_Updated()
        {
            this.update_numCompletedDownloads_status();
        }
        private void update_numCompletedDownloads_status()
        {
            string message;
            if (this.downloadsStatus.NumUrlsLeftToDownload < 1 && this.downloadsStatus.NumUrlsDownloadedButNotShown < 1)
            {
                message = "Downloads Complete";
            }
            else
            {
                if (this.downloadsStatus.NumUrlsLeftToDownload >= this.downloadsStatus.NumToDownload)
                {
                    message = "Downloading 0/" + this.downloadsStatus.NumToDownload;
                }
                else
                {
                    message = "" + this.downloadsStatus.NumUrlsLeftToDownload + " feeds left to download; " + this.downloadsStatus.NumUrlsDownloadedButNotShown + " new feeds to show";
                }
            }
            if (this.downloadsStatus.NumFailed > 0)
                message += " (" + this.downloadsStatus.NumFailed + " failed, including " + this.downloadsStatus.SampleFailure + ")";
            System.Diagnostics.Debug.WriteLine("NumCompletedDownloads status message: '" + message + "'");

            if (this.downloadsStatus.NumUrlsDownloadedButNotShown > 0)
            {
                this.downloadStatus_container.SubLayout = this.updateButton_layout;
                this.updateButton.Text = message;
            }
            else
            {
                this.downloadStatus_container.SubLayout = this.cannotUpdate_layout;
                this.cannotUpdate_layout.setText(message);
            }
        }

        private void update()
        {
            Vertical_GridLayout_Builder gridBuilder = new Vertical_GridLayout_Builder();
            gridBuilder.AddLayout(this.downloadStatus_container);

            int minIndex = this.displayIndex;
            int maxIndex = Math.Min(this.posts.Count, minIndex + this.pageSize);
            bool hasMore = this.posts.Count > this.displayIndex + this.pageSize;
            List<AnalyzedPost> posts = this.posts.GetRange(minIndex, maxIndex - minIndex);

            double previousScore = double.NegativeInfinity;
            foreach (AnalyzedPost scoredPost in posts)
            {
                double thisScore = scoredPost.Score;
                if (thisScore != previousScore)
                {
                    string text = "Score: " + thisScore;
                    TextblockLayout textBlockLayout = new TextblockLayout(text, 30);
                    textBlockLayout.setBackgroundColor(Color.Black);
                    textBlockLayout.setTextColor(Color.White);
                    gridBuilder.AddLayout(textBlockLayout);

                    previousScore = thisScore;
                }
                gridBuilder.AddLayout(this.viewForPost(scoredPost));
            }
            if (hasMore)
                gridBuilder.AddLayout(this.nextButton_layout);
            this.gridLayout = gridBuilder.Build();
            this.SubLayout = ScrollLayout.New(this.gridLayout);
        }

        private PostView viewForPost(AnalyzedPost post)
        {
            if (!this.postViewCache.ContainsKey(post))
            {
                PostView postView = new PostView(post);
                postView.PostClicked += PostView_PostClicked;
                postView.PostStarred += PostView_PostStarred;
                postView.Dismissed += PostView_Dismissed;
                this.postViewCache[post] = postView;
            }
            return this.postViewCache[post];
        }

        private void PostView_Dismissed(PostView post)
        {
            int count = this.gridLayout.NumRows; 
            for (int i = 0; i < count; i++)
            {
                if (this.gridLayout.GetLayout(0, i) == post)
                {
                    this.gridLayout.PutLayout(null, 0, i);
                    return;
                }
            }
        }

        private void PostView_PostClicked(PostInteraction post)
        {
            this.PostClicked.Invoke(post);
        }

        private void PostView_PostStarred(PostInteraction post)
        {
            this.PostStarred.Invoke(post);
        }

        private DownloadsStatus downloadsStatus;
        private ContainerLayout downloadStatus_container;
        private Button updateButton;
        private ButtonLayout updateButton_layout;
        private Button nextButton;
        private ButtonLayout nextButton_layout;
        private TextblockLayout cannotUpdate_layout;
        private List<AnalyzedPost> posts;
        private int displayIndex;
        private int pageSize = 30;
        private Dictionary<AnalyzedPost, PostView> postViewCache = new Dictionary<AnalyzedPost, PostView>();
        private GridLayout gridLayout;
    }
}

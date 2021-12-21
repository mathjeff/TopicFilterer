using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TopicFilterer.Scoring;
using TopicFilterer.View;
using VisiPlacement;
using Xamarin.Forms;

namespace TopicFilterer
{
    class TopicFilterer : OnBack_Listener
    {
        public TopicFilterer(ContentView contentView)
        {
            VisualDefaults_Builder defaultsBuilder = new VisualDefaults_Builder();
            defaultsBuilder.UneditableText_Color(Color.White);
            defaultsBuilder.UneditableText_Background(Color.Black);

            ViewManager viewManager = new ViewManager(contentView, null, defaultsBuilder.Build());

            Button customizeButton = new Button();
            customizeButton.Clicked += CustomizeButton_Clicked;

            Button startButton = new Button();
            startButton.Clicked += StartButton_Clicked;

            Button browseStarred_button = new Button();
            browseStarred_button.Clicked += BrowseStarred_button_Clicked;

            viewManager.SetLayout(this.LayoutStack);

            LayoutChoice_Set startLayout = new Vertical_GridLayout_Builder()
                .AddLayout(new ButtonLayout(customizeButton, "Preferences"))
                .AddLayout(new ButtonLayout(startButton, "Browse Latest"))
                .AddLayout(new ButtonLayout(browseStarred_button, "Browse Past Favorites"))
                .Build();

            this.LayoutStack.AddLayout(startLayout, "Welcome", -1);
            this.viewManager = viewManager;

            this.loadPostDatabase();
            this.loadPreferences();
            this.setupRulesScreen();
        }

        private void BrowseStarred_button_Clicked(object sender, EventArgs e)
        {
            this.showStarred();
        }
        private void showStarred()
        {
            this.LayoutStack.AddLayout(this.makeStarredScreen(), "Starred");
        }
        private LayoutChoice_Set makeStarredScreen()
        {
            List<PostInteraction> starredPosts = this.getStarredPosts();
            if (starredPosts.Count < 1)
            {
                return new TextblockLayout("No starred posts! First browse some new posts and star some of them");
            }
            Vertical_GridLayout_Builder builder = new Vertical_GridLayout_Builder();
            foreach (PostInteraction interaction in starredPosts)
            {
                AnalyzedPost analyzed = this.analyzePost(interaction.Post);
                PostView postView = new PostView(analyzed);
                postView.PostClicked += PostView_PostClicked;
                postView.PostStarred += PostView_PostStarred;
                builder.AddLayout(postView);
            }
            return ScrollLayout.New(builder.BuildAnyLayout());
        }
        private List<PostInteraction> getStarredPosts()
        {
            List<PostInteraction> starredPosts = new List<PostInteraction>();
            foreach (PostInteraction post in this.postDatabase.GetPosts())
            {
                if (post.Starred)
                {
                    starredPosts.Add(post);
                }
            }
            starredPosts.Reverse();
            return starredPosts;
        }

        private void setupRulesScreen()
        {
            CustomizePreferences_Layout customizeLayout = new CustomizePreferences_Layout(this.userPreferences_database, this.LayoutStack);
            customizeLayout.RequestImport += CustomizeLayout_RequestImport;
            this.preferencesLayout = customizeLayout;
        }

        private void CustomizeLayout_RequestImport(string text)
        {
            UserPreferences_Database newDb = this.textConverter.ParseUserPreferences(text);
            this.userPreferences_database.CopyFrom(newDb);
            this.savePreferences();
            this.LayoutStack.RemoveLayout();
            this.LayoutStack.RemoveLayout();
            this.setupRulesScreen();
        }

        private void CustomizeButton_Clicked(object sender, EventArgs e)
        {
            this.LayoutStack.AddLayout(this.preferencesLayout, "Customize", this);
        }

        public void OnBack(LayoutChoice_Set layout)
        {
            if (layout == this.preferencesLayout)
            {
                this.savePreferences();
                return;
            }
            throw new Exception("Unrecognized layout " + layout);
        }

        private void savePreferences()
        {
            string serialized = this.textConverter.ConvertToString(this.userPreferences_database);
            this.fileIo.EraseFileAndWriteContent(this.userPreferences_filePath, serialized);
        }

        private void StartButton_Clicked(object sender, EventArgs e)
        {
            this.start();
        }

        private void start()
        {
            this.startGetData();
        }

        private void showPosts(PostsView postsView)
        {
            List<AnalyzedPost> posts = new List<AnalyzedPost>(this.analyzedPosts);
            this.sortPosts(posts);
            posts = this.withoutDuplicateSources(posts);

            this.downloadsStatus.NumUrlsDownloadedButNotShown = 0;
            this.downloadsStatus.NumFailed = 0;

            postsView.Posts = posts;
        }

        private void PostView_PostStarred(PostInteraction post)
        {
            this.savePostDatabase();
        }

        private void PostView_PostClicked(PostInteraction interaction)
        {
            this.savePostDatabase();
            Device.OpenUri(new Uri(interaction.Post.Source));
        }

        private List<AnalyzedPost> analyzePosts(List<Post> posts)
        {
            List<AnalyzedPost> scoredPosts = new List<AnalyzedPost>();
            foreach (Post post in posts)
            {
                scoredPosts.Add(this.analyzePost(post));
            }
            return scoredPosts;
        }
        private void sortPosts(List<AnalyzedPost> posts)
        {
            posts.Sort(new ScoredPost_Sorter());
            posts.Reverse();
        }
        private List<AnalyzedPost> withoutDuplicateSources(List<AnalyzedPost> posts)
        {
            List<AnalyzedPost> results = new List<AnalyzedPost>();
            HashSet<string> sources = new HashSet<string>();
            foreach (AnalyzedPost post in posts)
            {
                string source = post.Interaction.Post.Source;
                if (sources.Contains(source))
                {
                    continue;
                }
                sources.Add(source);
                results.Add(post);
            }
            return results;
        }

        private AnalyzedPost analyzePost(Post post)
        {
            string title = post.Title;
            double score = this.scoreTitle(title);

            List<AnalyzedString> titleComponents = new List<AnalyzedString>();
            List<String> words = new List<String>(title.Split(new char[] { ' ' }));
            for (int i = 0; i < words.Count; i++)
            {
                List<String> hypotheticalWords = new List<String>(words);
                hypotheticalWords.RemoveAt(i);
                double hypotheticalScore = this.scoreTitle(string.Join(" ", hypotheticalWords));
                double difference = score.CompareTo(hypotheticalScore);
                titleComponents.Add(new AnalyzedString(words[i], difference));
            }
            for (int i = titleComponents.Count - 1; i >= 1; i--)
            {
                if (titleComponents[i].Score == titleComponents[i - 1].Score)
                {
                    titleComponents[i - 1] = new AnalyzedString(titleComponents[i - 1].Text + " " + titleComponents[i].Text, titleComponents[i].Score);
                    titleComponents.RemoveAt(i);
                }
            }

            PostInteraction interaction = this.postDatabase.Get(post);

            return new AnalyzedPost(interaction, score, titleComponents);
        }
        private double scoreTitle(string title)
        {
            List<TextRule> rules = this.userPreferences_database.ScoringRules;
            double score = 0;
            foreach (TextRule rule in rules)
            {
                score += rule.computeScore(title);
            }
            return score;
        }

        private void loadPostDatabase()
        {
            string text = this.fileIo.ReadAllText(this.postDatabase_filePath);
            if (text != null && text != "")
            {
                this.postDatabase = PostInteraction_Database.Parse(text);
            }
        }
        private void loadPreferences()
        {
            string text = this.fileIo.ReadAllText(this.userPreferences_filePath);

            if (text != null && text != "")
            {
                this.userPreferences_database = UserPreferences_Database.Parse(text);
            }
        }
        private void savePostDatabase()
        {
            this.postDatabase.ShrinkToSize(10000);
            string text = this.postDatabase.ToString();
            this.fileIo.EraseFileAndWriteContent(this.postDatabase_filePath, text);
        }
        private void startGetData()
        {
            List<UrlDownloader> pendingDownloads = this.startDownloadData();
            this.pendingDownloads = pendingDownloads;
            this.analyzedPosts = new List<AnalyzedPost>();
            this.downloadsStatus = new DownloadsStatus(this.pendingDownloads.Count);

            PostsView resultsLayout = new PostsView(this.downloadsStatus);
            resultsLayout.RequestUpdate += ResultsLayout_RequestUpdate;
            resultsLayout.PostClicked += PostView_PostClicked;
            resultsLayout.PostStarred += PostView_PostStarred;
            this.LayoutStack.AddLayout(resultsLayout, "News");

            this.downloadNextUrl();
        }

        private void ResultsLayout_RequestUpdate(PostsView postsView)
        {
            this.showPosts(postsView);
        }

        private void downloadNextUrl()
        {
            System.Diagnostics.Debug.WriteLine("downloadNextUrl");
            if (this.pendingDownloads.Count > 0)
            {
                UrlDownloader request = this.pendingDownloads[0];
                this.pendingDownloads.RemoveAt(0);

                Task.Run(() =>
                {
                    System.Diagnostics.Debug.WriteLine("downloadNextUrl calling .Get()");
                    String text = request.Get();
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        this.downloadCompleted(text, request.Url);
                    });
                });
            }
        }
        private void downloadCompleted(string text, string url)
        {
            System.Diagnostics.Debug.WriteLine("downloadCompleted");
            this.onReceivedData(text, url);
            this.downloadNextUrl();
        }
        private void onReceivedData(string text, string url)
        {
            System.Diagnostics.Debug.WriteLine("onRecievedData");
            this.downloadsStatus.NumUrlsLeftToDownload--;
            if (text != null)
            {
                try
                {
                    List<Post> posts = this.feedParser.parse(text);
                    List<AnalyzedPost> analyzedPosts = this.analyzePosts(posts);
                    this.analyzedPosts.AddRange(analyzedPosts);
                    this.downloadsStatus.NumUrlsDownloadedButNotShown++;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("Error downloading data: " + e);
                    this.downloadsStatus.NumFailed++;
                    this.downloadsStatus.SampleFailure = url;
                }
            }
        }

        private List<UrlDownloader> startDownloadData()
        {
            WebClient webClient = new WebClient();
            List<UrlDownloader> requests = new List<UrlDownloader>();
            IEnumerable<String> urls = this.userPreferences_database.FeedUrls;
            foreach (string urlText in urls)
            {
                UrlDownloader request = new UrlDownloader(webClient, urlText);
                requests.Add(request);
            }
            return requests;
        }


        public bool GoBack()
        {
            return this.LayoutStack.GoBack();
        }

        List<UrlDownloader> pendingDownloads = null;
        List<AnalyzedPost> analyzedPosts = null;

        private ViewManager viewManager;
        private LayoutStack LayoutStack = new LayoutStack(true);
        private InternalFileIo fileIo = new InternalFileIo();
        private string postDatabase_filePath = "posts.txt";
        private string userPreferences_filePath = "preferences.txt";
        private PostInteraction_Database postDatabase = new PostInteraction_Database();

        private DownloadsStatus downloadsStatus;
        private UserPreferences_Database userPreferences_database = new UserPreferences_Database();
        private LayoutChoice_Set preferencesLayout;
        private TextConverter textConverter = new TextConverter();
        private FeedParser feedParser = new FeedParser();
    }
}

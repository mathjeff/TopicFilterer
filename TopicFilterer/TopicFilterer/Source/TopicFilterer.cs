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
            ViewManager viewManager = new ViewManager(contentView, null);

            Button customizeButton = new Button();
            customizeButton.Clicked += CustomizeButton_Clicked;
            ButtonLayout customizeButtonLayout = new ButtonLayout(customizeButton, "Customize");

            Button startButton = new Button();
            startButton.Clicked += StartButton_Clicked;
            ButtonLayout startButtonLayout = new ButtonLayout(startButton, "Start");

            viewManager.SetLayout(this.LayoutStack);

            LayoutChoice_Set startLayout = new Vertical_GridLayout_Builder().AddLayout(customizeButtonLayout).AddLayout(startButtonLayout).Build();

            this.LayoutStack.AddLayout(startLayout, "Welcome", -1);
            this.viewManager = viewManager;

            this.loadPostDatabase();
            this.loadPreferences();
            this.setupRulesScreen();
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

        private List<Post> parse(string text)
        {
            XmlDocument parse = new XmlDocument();
            parse.LoadXml(text);

            XmlNode feed = parse.FirstChild;
            if (feed.ChildNodes.Count == 1 && feed.FirstChild.Name == "channel")
            {
                feed = feed.FirstChild;
            }
            return this.extractPosts(feed);
        }
        private List<Post> extractPosts(XmlNode feed)
        {
            List<XmlNode> items = new List<XmlNode>();

            foreach (XmlNode node in feed.ChildNodes)
            {
                if (node.Name == "entry" || node.Name == "item")
                {
                    items.Add(node);
                }
            }

            List<Post> posts = new List<Post>();

            foreach (XmlNode node in items)
            {
                Post post = this.postFromData(node);
                if (post.Title != null && post.Title != "")
                    posts.Add(post);
                else
                    System.Diagnostics.Debug.WriteLine("Invalid post: " + post);
            }
            return posts;
        }

        private void showPosts()
        {
            List<AnalyzedPost> posts = new List<AnalyzedPost>(this.analyzedPosts);
            Vertical_GridLayout_Builder gridBuilder = new Vertical_GridLayout_Builder();
            gridBuilder.AddLayout(this.downloadStatus_container);

            this.sortPosts(posts);

            int maxCountToShow = 30;
            if (posts.Count > maxCountToShow)
                posts = posts.GetRange(0, maxCountToShow);
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
                PostInteraction interaction = this.postDatabase.Get(scoredPost);
                PostView postView = new PostView(interaction);
                postView.PostClicked += PostView_PostClicked;
                gridBuilder.AddLayout(postView);
            }

            LayoutChoice_Set scrollLayout = ScrollLayout.New(gridBuilder.BuildAnyLayout());
            this.downloadsStatus.NumUrlsDownloadedButNotShown = 0;
            this.update_numCompletedDownloads_status();

            this.resultsLayout.SubLayout = scrollLayout;
        }

        private void PostView_PostClicked(PostInteraction interaction)
        {
            this.savePostDatabase();
            Device.OpenUri(new Uri(interaction.Post.Post.Source));
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
            return new AnalyzedPost(post, score, titleComponents);
        }
        private List<String> splitIntoWords(string text)
        {
            char[] wordSeparators = new char[] { ' ', ',', '.', '!', '?', '-' };
            List<String> orderedWords = new List<string>(text.Split(wordSeparators));
            return orderedWords;
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
        private double scoreTitle_old(string title)
        {
            double score = 0;
            string lowerTitle = title.ToLower();
            List<String> orderedWords = this.splitIntoWords(lowerTitle);
            HashSet<String> unorderedWords = new HashSet<string>(orderedWords);

            // generic negative filters
            if (lowerTitle.StartsWith("when") && !lowerTitle.Contains("?"))
                score -= 2;
            if (lowerTitle.Contains("rescued"))
                score -= 2;
            if (lowerTitle.Contains("her death") || lowerTitle.Contains("his death"))
                score -= 1;
            if (lowerTitle.StartsWith("aita "))
                score -= 2;
            if (lowerTitle.Contains(" dog ") || lowerTitle.StartsWith("dog "))
                score -= 2;
            if (lowerTitle.Contains("espn ") || lowerTitle.Contains(" espn"))
                score -= 2;
            if (lowerTitle.Contains("nba"))
                score -= 2;
            if (lowerTitle.Contains("not fair") || lowerTitle.Contains("unfair"))
                score -= 1;
            if (lowerTitle.StartsWith("ea "))
                score -= 1;
            if (lowerTitle.EndsWith(".."))
                score -= 1;
            if (lowerTitle.Contains("macbook"))
                score -= 2;
            if (lowerTitle.Contains("apple"))
                score -= 2;
            if (lowerTitle.Contains("steve jobs"))
                score -= 2;
            if (lowerTitle.Contains("stranger things"))
                score -= 2;
            if (lowerTitle.Contains(" died") || lowerTitle.Contains(" dead") || lowerTitle.Contains(" dies"))
                score -= 1;
            if (lowerTitle.Contains(" war "))
                score -= 1;
            if (lowerTitle.Contains(" injured") || lowerTitle.Contains(" injury") || lowerTitle.Contains("surgery") || lowerTitle.Contains("unsafe") || lowerTitle.Contains("police") || lowerTitle.Contains("arrested"))
                score -= 1;
            if (lowerTitle.Contains(" boo "))
                score -= 1;
            if (lowerTitle.Split(' ').Length <= 6)
                score -= 1;

            // generic positive filters
            if (lowerTitle.Contains("nasa ") || lowerTitle.Contains("space ") || lowerTitle.Contains("hubble ") ||
                (lowerTitle.Contains(" moon") && !lowerTitle.Contains("m over the moon")))
                score += 2;
            if (lowerTitle.Contains("research"))
                score += 2;
            if (lowerTitle.Contains("better"))
                score += 2;
            if (lowerTitle.Contains("bank "))
                score += 2;
            if (lowerTitle.StartsWith("til ") || lowerTitle.Contains("learn"))
                score += 2;
            if (lowerTitle.Contains("psychology") || lowerTitle.Contains("math"))
                score += 2;
            if (lowerTitle.Contains("strategy"))
                score += 2;
            if (lowerTitle.Contains("drone"))
                score += 1;
            if (lowerTitle.Contains("npr ") || lowerTitle.Contains(" npr"))
                score += 1;
            if (lowerTitle.Contains("exciting"))
                score += 1;
            if (lowerTitle.Contains("smart"))
                score += 1;

            // Microbiome filters
            if (
                unorderedWords.Contains("microbiome")
                || unorderedWords.Contains("microbiota")
                || unorderedWords.Contains("microbial")
                || unorderedWords.Contains("microbe")
                || unorderedWords.Contains("bacterial")
                )
            {
                if (lowerTitle.Contains("commensal"))
                    score += 3;
                if (lowerTitle.Contains("pathogen"))
                    score += 2;
                if (lowerTitle.Contains("antimicrobial resistance"))
                    score += 2;
                if (lowerTitle.Contains("antibiotic resistance"))
                    score += 2;
                if (unorderedWords.Contains("ahr"))
                    score += 2;
                if (unorderedWords.Contains("lactobacillus"))
                    score += 1.5;
                if (lowerTitle.Contains("horizontal gene transfer"))
                    score += 2;
                if (unorderedWords.Contains("immune"))
                    score += 2;
                if (unorderedWords.Contains("depression"))
                    score += 2;
                if (unorderedWords.Contains("ibd"))
                    score += 2;
                if (unorderedWords.Contains("butyrate"))
                    score += 2;
                if (unorderedWords.Contains("evolution"))
                    score += 2.5;
                if (unorderedWords.Contains("adaptation"))
                    score += 2.5;
                if (unorderedWords.Contains("interaction"))
                    score += 3;
                if (unorderedWords.Contains("virus"))
                    score += 2;
                if (unorderedWords.Contains("phage"))
                    score += 2;
                if (lowerTitle.Contains("population genetics"))
                    score += 1.5;
                if (unorderedWords.Contains("virome"))
                    score += 1.5;
                if (unorderedWords.Contains("bioinformatics"))
                    score += 2;
                if (unorderedWords.Contains("sfb"))
                    score += 1;
                if (lowerTitle.Contains("segmented filamentous bacteria"))
                    score += 1;
                if (lowerTitle.Contains("single cell"))
                    score += 2;
                if (unorderedWords.Contains("nanopore"))
                    score += 2;
                if (unorderedWords.Contains("bgc"))
                    score += 1;
                if (lowerTitle.Contains("biosynthetic gene cluster"))
                    score += 1;
                if (unorderedWords.Contains("cfv"))
                    score += 1;
                if (lowerTitle.Contains("vitamin"))
                    score += 1;
                if (unorderedWords.Contains("geba"))
                    score += 1;
                if (unorderedWords.Contains("pks"))
                    score += 1;
                if (unorderedWords.Contains("rps"))
                    score += 1;
                if (unorderedWords.Contains("eps"))
                    score += 1;
                if (unorderedWords.Contains("fmt"))
                    score += 1;
                if (unorderedWords.Contains("longitudinal"))
                    score += 1;
                if (unorderedWords.Contains("host"))
                    score += 2;
                if (lowerTitle.Contains("isolate"))
                    score += 2;
                if (unorderedWords.Contains("aryl"))
                    score += 1.5;
                if (lowerTitle.Contains("indole"))
                    score += 1;
                if (unorderedWords.Contains("tool"))
                    score += 2;
                if (unorderedWords.Contains("method"))
                    score += 2;
                if (lowerTitle.Contains("plasmid"))
                    score += 1;
                if (unorderedWords.Contains("stool"))
                    score += 2;
                if (lowerTitle.Contains("gut"))
                    score += 2;
                if (lowerTitle.Contains("mobile"))
                    score += 2;
                if (lowerTitle.Contains("human"))
                    score += 3;
                if (lowerTitle.Contains("virulence"))
                    score += 2;
                if (lowerTitle.Contains("diabet"))
                    score += 1;
                if (lowerTitle.Contains("infection"))
                    score += 1;
                if (lowerTitle.Contains("genomic"))
                    score += 1.5;
                if (lowerTitle.Contains("disease"))
                    score += 2;
                if (lowerTitle.Contains("regulatory"))
                    score += 1;
                if (lowerTitle.Contains("genotyp"))
                    score += 1;
                if (lowerTitle.Contains("phenotyp"))
                    score += 1;
                if (lowerTitle.Contains("single-cell"))
                    score += 2;
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
            this.postDatabase.ShrinkToSize(1000);
            string text = this.postDatabase.ToString();
            this.fileIo.EraseFileAndWriteContent(this.postDatabase_filePath, text);
        }
        private void startGetData()
        {
            List<ValueProvider<String>> pendingDownloads = this.startDownloadData();
            this.pendingDownloads = pendingDownloads;
            this.analyzedPosts = new List<AnalyzedPost>();
            this.downloadsStatus = new DownloadsStatus(this.pendingDownloads.Count);

            this.setupUpdateLayout();

            this.resultsLayout = new ContainerLayout();
            this.resultsLayout.SubLayout = this.downloadStatus_container;
            this.LayoutStack.AddLayout(resultsLayout, "News");

            this.downloadNextUrl();
        }
        private void setupUpdateLayout()
        {
            this.cannotUpdate_layout = new TextblockLayout("", 32);
            this.cannotUpdate_layout.setTextColor(Color.White);
            this.cannotUpdate_layout.setBackgroundColor(Color.Black);
            this.updateButton = new Button();
            this.updateButton.Clicked += UpdateButton_Clicked;
            this.updateButton_layout = new ButtonLayout(this.updateButton, "", 32);
            this.downloadStatus_container = new ContainerLayout();
            this.update_numCompletedDownloads_status();
        }

        private void UpdateButton_Clicked(object sender, EventArgs e)
        {
            this.showPosts();
        }

        private void downloadNextUrl()
        {
            System.Diagnostics.Debug.WriteLine("downloadNextUrl");
            if (this.pendingDownloads.Count > 0)
            {
                ValueProvider<String> request = this.pendingDownloads[0];
                this.pendingDownloads.RemoveAt(0);

                Task.Run(() =>
                {
                    System.Diagnostics.Debug.WriteLine("downloadNextUrl calling .Get()");
                    String text = request.Get();
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        this.downloadCompleted(text);
                    });
                });
            }
        }
        private void downloadCompleted(String text)
        {
            System.Diagnostics.Debug.WriteLine("downloadCompleted");
            this.onReceivedData(text);
            this.downloadNextUrl();
        }
        private void onReceivedData(String text)
        {
            System.Diagnostics.Debug.WriteLine("onRecievedData");
            this.downloadsStatus.NumUrlsLeftToDownload--;
            if (text != null)
            {
                this.downloadsStatus.NumUrlsDownloadedButNotShown++;
                List<Post> posts = this.parse(text);
                List<AnalyzedPost> analyzedPosts = this.analyzePosts(posts);
                this.analyzedPosts.AddRange(analyzedPosts);
            }
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
                    message = "" + this.downloadsStatus.NumUrlsLeftToDownload + " urls left to download; " + this.downloadsStatus.NumUrlsDownloadedButNotShown + " new urls to show";
                }
            }
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

        private List<ValueProvider<String>> startDownloadData()
        {
            WebClient webClient = new WebClient();
            List<ValueProvider<String>> requests = new List<ValueProvider<string>>();
            IEnumerable<String> urls = this.userPreferences_database.FeedUrls;
            /*List<String> urls = new List<String>() {
                "https://www.reddit.com/.rss",
                "https://news.google.com/rss",
                "https://hackaday.com/feed/",

                "http://connect.biorxiv.org/biorxiv_xml.php?subject=all",
                "https://www.nature.com/nature.rss",
                "http://www.nature.com/nm/current_issue/rss",
                "http://www.nature.com/nmeth/current_issue/rss",
                "http://www.nature.com/nbt/current_issue/rss",
                "http://www.nature.com/nrmicro/current_issue/rss",
                "https://www.nature.com/ncomms.rss",
                "https://www.cell.com/cell/current.rss",
                "https://science.sciencemag.org/rss/current.xml",
                "http://www.cell.com/cell-host-microbe/current.rss",
                "https://elifesciences.org/rss/recent.xml",
                "https://elifesciences.org/rss/ahead.xml"
            };*/
            foreach (string urlText in urls)
            {
                UrlDownloader request = new UrlDownloader(webClient, urlText);
                requests.Add(request);
            }
            return requests;
        }

        private Post postFromData(XmlNode data)
        {
            Post post = new Post();

            foreach (XmlNode child in data.ChildNodes)
            {
                if (child.Name == "title")
                {
                    if (child.ChildNodes.Count == 1)
                        post.Title = child.FirstChild.Value;
                }
                if (child.Name == "content")
                {
                    if (child.ChildNodes.Count == 1)
                        post.Text = child.FirstChild.Value;
                }
                if (child.Name == "link")
                {
                    String attributeValue = child.getAttribute("href");
                    if (attributeValue != null)
                    {
                        post.Source = attributeValue;
                    }
                    else
                    {
                        if (child.ChildNodes.Count == 1)
                            post.Source = child.FirstChild.Value;
                    }
                }
            }
            return post;
        }

        public bool GoBack()
        {
            return this.LayoutStack.GoBack();
        }

        List<ValueProvider<String>> pendingDownloads = null;
        List<AnalyzedPost> analyzedPosts = null;

        private ViewManager viewManager;
        private LayoutStack LayoutStack = new LayoutStack(true);
        private InternalFileIo fileIo = new InternalFileIo();
        private string postDatabase_filePath = "posts.txt";
        private string userPreferences_filePath = "preferences.txt";
        private PostInteraction_Database postDatabase = new PostInteraction_Database();

        private ContainerLayout resultsLayout;

        private DownloadsStatus downloadsStatus;
        private ContainerLayout downloadStatus_container;
        private Button updateButton;
        private ButtonLayout updateButton_layout;
        private TextblockLayout cannotUpdate_layout;
        private UserPreferences_Database userPreferences_database = new UserPreferences_Database();
        private LayoutChoice_Set preferencesLayout;
        TextConverter textConverter = new TextConverter();
    }
}

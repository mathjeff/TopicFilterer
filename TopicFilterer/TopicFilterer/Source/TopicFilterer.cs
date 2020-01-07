﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TopicFilterer.View;
using VisiPlacement;
using Xamarin.Forms;

namespace TopicFilterer
{
    class TopicFilterer
    {
        public TopicFilterer(ContentView contentView)
        {
            ViewManager viewManager = new ViewManager(contentView, null);

            Button startButton = new Button();

            startButton.Clicked += StartButton_Clicked;

            viewManager.SetLayout(this.LayoutStack);
            this.LayoutStack.AddLayout(new ButtonLayout(startButton, "Start"), "Start");
            this.viewManager = viewManager;
            System.Diagnostics.Debug.WriteLine("Debugging enabled");
        }

        private void StartButton_Clicked(object sender, EventArgs e)
        {
            this.start();
        }

        private TextblockLayout statusMessage(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.TextColor = Color.White;
            label.BackgroundColor = Color.Black;
            return new TextblockLayout(label);
        }
        private void start()
        {
            this.LayoutStack.AddLayout(this.statusLayout, "results");
            Label label = new Label();
            this.statusLayout.SubLayout = this.statusMessage("Downloading data");

            Task.Run(() =>
            {
                List<string> data = this.getData();
                Device.BeginInvokeOnMainThread(() =>
                {
                    this.showDataAsync(data);
                });
            });
        }

        private void showDataAsync(List<String> data)
        {
            this.statusLayout.SubLayout = statusMessage("Analyzing data");
            Task.Run(() =>
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    this.showData(data);
                });
            });
        }

        private void showData(List<String> data)
        {
            List<Post> posts = new List<Post>();
            foreach (string thisData in data)
            {
                posts.AddRange(this.parse(thisData));
            }

            this.showPosts(posts);
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

        private void showPosts(List<Post> posts)
        {
            Vertical_GridLayout_Builder gridBuilder = new Vertical_GridLayout_Builder();

            List<AnalyzedPost> scored = this.rankPosts(posts);
            int maxCountToShow = 20;
            if (scored.Count > maxCountToShow)
                scored = scored.GetRange(0, maxCountToShow);
            double previousScore = double.NegativeInfinity;
            foreach (AnalyzedPost scoredPost in scored)
            {
                double thisScore = scoredPost.Score;
                if (thisScore != previousScore)
                {
                    Label label = new Label();
                    label.Text = "Score: " + thisScore;
                    gridBuilder.AddLayout(new TextblockLayout(label, 30));
                    label.BackgroundColor = Color.Black;
                    label.TextColor = Color.White;
                    previousScore = thisScore;
                }
                Post post = scoredPost.Post;
                gridBuilder.AddLayout(new PostView(scoredPost));
            }

            LayoutChoice_Set scrollLayout = ScrollLayout.New(gridBuilder.BuildAnyLayout());
            this.LayoutStack.AddLayout(scrollLayout, "News");
        }

        private List<AnalyzedPost> rankPosts(List<Post> posts)
        {
            List<AnalyzedPost> scoredPosts = new List<AnalyzedPost>();
            foreach (Post post in posts)
            {
                scoredPosts.Add(this.analyzePost(post));
            }
            scoredPosts.Sort(new ScoredPost_Sorter());
            scoredPosts.Reverse();
            List<AnalyzedPost> results = new List<AnalyzedPost>();
            foreach (AnalyzedPost scored in scoredPosts)
            {
                results.Add(scored);
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


        private List<String> getData()
        {
            if (this.data == null)
            {
                this.data = downloadData();
                //this.data = new List<String>() { getMockData() };
            }

            return this.data;
        }

        private List<String> downloadData()
        {
            WebClient webClient = new WebClient();
            List<String> texts = new List<String>();
            List<String> urls = new List<String>() {
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
            };
            foreach (string urlText in urls)
            {
                try
                {
                    byte[] data = webClient.DownloadData(urlText);
                    string text = System.Text.Encoding.UTF8.GetString(data);
                    System.Diagnostics.Debug.Write("From " + urlText + ", downloaded data of " + text);
                    texts.Add(text);
                }
                catch (WebException e)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to open " + urlText + ", " + e);
                }
            }
            return texts;
        }

        private string getMockData()
        {
            //return @"<?xml version=""1.0"" encoding=""UTF-8""?><feed xmlns=""http://www.w3.org/2005/Atom""><category term="" reddit.com"" label=""r/ reddit.com""/><updated>2019-06-23T21:07:07+00:00</updated><id>/.rss</id><link rel=""self"" href=""https://www.reddit.com/.rss"" type=""application/atom+xml"" /><link rel=""alternate"" href=""https://www.reddit.com/"" type=""text/html"" /><title>reddit: the front page of the internet</title><entry><author><name>/u/FulgencioLanzol</name><uri>https://www.reddit.com/user/FulgencioLanzol</uri></author><category term=""AskReddit"" label=""r/AskReddit""/><content type=""html"">&amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/FulgencioLanzol&quot;&gt; /u/FulgencioLanzol &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/AskReddit/&quot;&gt; r/AskReddit &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/AskReddit/comments/c40baz/people_who_speak_english_as_a_second_language/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/AskReddit/comments/c40baz/people_who_speak_english_as_a_second_language/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_c40baz</id><link href=""https://www.reddit.com/r/AskReddit/comments/c40baz/people_who_speak_english_as_a_second_language/"" /><updated>2019-06-23T06:49:42+00:00</updated><title>People who speak English as a second language, what phrases or concepts from your native tongue you want to use in English but can't because locals wouldn't understand?</title></entry><entry><author><name>/u/metalsirens</name><uri>https://www.reddit.com/user/metalsirens</uri></author><category term=""AmItheAsshole"" label=""r/AmItheAsshole""/><content type=""html"">&lt;!-- SC_OFF --&gt;&lt;div class=&quot;md&quot;&gt;&lt;p&gt;My friend set me up with this guy who’s one of his closest friends. He told me his friend was Muslim but he was really cute and my friend figured we’d hit it off despite slight religious/ cultural differences. We’ve been talking for a week now and last night we met in person. &lt;/p&gt; &lt;p&gt;One of the conversations we had was about halal/ eating pork/ drinking alcohol. Whilst he doesn’t eat pork at all, he drinks alcohol and eats McDonald’s/ shake shack/ whatever. When I asked him why he doesn’t eat pork he just told me it’s personal preference NOT that he avoids it for religious reasons. &lt;/p&gt; &lt;p&gt;I am really into this guy and wanted to make an amazing first impression, so figured I’d make one of my go to all out meals that &lt;em&gt;everyone&lt;/em&gt; loves which was slow cooked bourbon pork butt sliders with a homemade slaw. &lt;/p&gt; &lt;p&gt;When he came over it was amazing, he was cute, funny, and really chill. He drank the beer I offered him, and when we sat down to eat I told him that we’d be having bourbon sliders. I didn’t mention it was pork because in our conversations he told me that he’d never intentionally eaten it before but if he did he didn’t think he’d like it so I didn’t want him to be biased. &lt;/p&gt; &lt;p&gt;After he took a couple bites he looked at me with a really pissed off face and asked if it was pork. I said yes, and he just spat out his mouthful into a napkin and in a really angry voice said that he thought he’d told me he didn’t eat pork. I told him that I thought I’d change his mind and he said that wasnt a cool thing for me to do. &lt;/p&gt; &lt;p&gt;He left very quickly after and I was so upset I called my friend who initially set us up. He was really pissed at me even though I defended myself on the phone and said that a) my date drank alcohol and didn’t eat halal anyway so how I was meant to know that he took not eating pork seriously and b) he didn’t make it clear he avoided pork for religious reasons. &lt;/p&gt; &lt;p&gt;All my guy friends have texted me today saying variants of ‘yo that was mad dumb of you lmao’ etc and I just feel really upset about the whole thing.&lt;/p&gt; &lt;/div&gt;&lt;!-- SC_ON --&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/metalsirens&quot;&gt; /u/metalsirens &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/AmItheAsshole/&quot;&gt; r/AmItheAsshole &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/AmItheAsshole/comments/c43tcx/aita_for_cooking_my_muslim_date_non_halal_meal/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/AmItheAsshole/comments/c43tcx/aita_for_cooking_my_muslim_date_non_halal_meal/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_c43tcx</id><link href=""https://www.reddit.com/r/AmItheAsshole/comments/c43tcx/aita_for_cooking_my_muslim_date_non_halal_meal/"" /><updated>2019-06-23T12:25:31+00:00</updated><title>AITA for cooking my Muslim date non halal meal after he said he doesn’t eat halal? my friends think I’m a major ass.</title></entry><entry><author><name>/u/DmarZX</name><uri>https://www.reddit.com/user/DmarZX</uri></author><category term=""interestingasfuck"" label=""r/interestingasfuck""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/interestingasfuck/comments/c48iwk/this_is_zeus_the_rescued_blind_owl_with_stars_in/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/eDs6Wyss2F3rYJ1PA4rBptzMzVjAi1KSNl0KZrwi4xI.jpg&quot; alt=&quot;This is zeus. The rescued blind owl with stars in his eyes&quot; title=&quot;This is zeus. The rescued blind owl with stars in his eyes&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/DmarZX&quot;&gt; /u/DmarZX &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/interestingasfuck/&quot;&gt; r/interestingasfuck &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://gfycat.com/jaggedmindlessheron&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/interestingasfuck/comments/c48iwk/this_is_zeus_the_rescued_blind_owl_with_stars_in/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c48iwk</id><link href=""https://www.reddit.com/r/interestingasfuck/comments/c48iwk/this_is_zeus_the_rescued_blind_owl_with_stars_in/"" /><updated>2019-06-23T17:53:27+00:00</updated><title>This is zeus. The rescued blind owl with stars in his eyes</title></entry><entry><author><name>/u/hopelesslyunhappy</name><uri>https://www.reddit.com/user/hopelesslyunhappy</uri></author><category term=""BlackPeopleTwitter"" label=""r/BlackPeopleTwitter""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/BlackPeopleTwitter/comments/c45pw7/the_new_regime/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/kCYMuLTXgOle8v2dthaBZIXxdApMvYOGLFvubpbwQUY.jpg&quot; alt=&quot;The new regime&quot; title=&quot;The new regime&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/hopelesslyunhappy&quot;&gt; /u/hopelesslyunhappy &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/BlackPeopleTwitter/&quot;&gt; r/BlackPeopleTwitter &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/mriwfxxzb4631.jpg&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/BlackPeopleTwitter/comments/c45pw7/the_new_regime/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c45pw7</id><link href=""https://www.reddit.com/r/BlackPeopleTwitter/comments/c45pw7/the_new_regime/"" /><updated>2019-06-23T14:43:34+00:00</updated><title>The new regime</title></entry><entry><author><name>/u/hymness1</name><uri>https://www.reddit.com/user/hymness1</uri></author><category term=""tifu"" label=""r/tifu""/><content type=""html"">&lt;!-- SC_OFF --&gt;&lt;div class=&quot;md&quot;&gt;&lt;p&gt;This happened 2 days ago. So last week I found a thong on the floor in my living room. Smelt it like the animal I am, was clean, and put it in my wife&amp;#39;s underwear drawer.&lt;/p&gt; &lt;p&gt;2 days ago in the evening, my wife is getting ready to go take a bath, when she comes down 2 minutes later asking me where the hell does this thong come from.&lt;/p&gt; &lt;p&gt;Huh... I found it on the floor, surely it felt from the laundry basket? It was clean so I put it in your drawer. What it&amp;#39;s not yours?&lt;/p&gt; &lt;p&gt;Angrily, she answers it&amp;#39;s not hers. You just want me to believe it just appeared on the floor?!?&lt;/p&gt; &lt;p&gt;I tell her what the hell am I suppose to say. She goes back upstairs really angry at me.&lt;/p&gt; &lt;p&gt;So last week my wife was out of town for work and a female friend of mine came take a beer with me. I send her a picture of the thong, asking if it hers. It is. We&amp;#39;re both a bit confuse as to how it ended in my living room... We came to the conclusion maybe it got stuck at whatever she was wearing that night by static electricity while it was in the dryer...&lt;/p&gt; &lt;p&gt;Now the real fuck up. My wife is in the bed. I go tell her that my friend was here last week to take a beer. Just texted her and the thong is hers. The only explanation we managed to find it&amp;#39;s that it was stuck by static at her clothes.&lt;/p&gt; &lt;p&gt;YOU WANT ME TO BELIEVE THAT?! THAT MAKES NO SENSE&lt;/p&gt; &lt;p&gt;Jesus Christ, I don&amp;#39;t even believe my explanation myself. I tell her to believe what she wants, I don&amp;#39;t fucking know how the thong appeared in my living room, that I don&amp;#39;t have anything to redeem myself. I take some clothes and go sleep at my parents&amp;#39; home.&lt;/p&gt; &lt;p&gt;The next morning she calls me. She tells me that she decided to believe in my stupid story, because the thong was indeed clean and it&amp;#39;s logical that I&amp;#39;d put it in her drawer. And if I was cheating on her, I&amp;#39;d be fucking stupid to put my mistress panties in her drawer. Asked me to come home and we laughed a lot after that.&lt;/p&gt; &lt;p&gt;TLDR : Found my friend&amp;#39;s thong on the floor in my living room, put it in my wife&amp;#39;s underwear drawer. Almost lost my wife to it.&lt;/p&gt; &lt;p&gt;EDIT : OMG it exploded. Thanks for the silver mystery redditors! I know a bunch of you called me a liar and a cheater, that&amp;#39;s ultimately your right! Had fun with the others that told similar stories :) And yes, I told my friend that I sniffed her panties, asked her what her fabric softener was.&lt;/p&gt; &lt;p&gt;EDIT2 : The &lt;a href=&quot;https://imgur.com/a/KtGzDeU&quot;&gt;thong&lt;/a&gt; in question&lt;/p&gt; &lt;p&gt;EDIT 3 : Wow thanks for the gold! I answered to a bunch of you, but I&amp;#39;ll not be able to keep up! There&amp;#39;s too many comments! I hope you guys have a nice day!&lt;/p&gt; &lt;p&gt;EDIT4 : addressing some comments. No I don&amp;#39;t think my friend did it on purpose to have my wife divorce me so she can have me all for herself. Yes I have the right to see other women and have a drink with them. My wife also has the right to see other men and have a drink with them. Finally, my friend&amp;#39;s fabric softener is Fleecy.&lt;/p&gt; &lt;/div&gt;&lt;!-- SC_ON --&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/hymness1&quot;&gt; /u/hymness1 &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/tifu/&quot;&gt; r/tifu &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/tifu/comments/c43efz/tifu_by_putting_a_thong_that_was_not_my_wifes_in/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/tifu/comments/c43efz/tifu_by_putting_a_thong_that_was_not_my_wifes_in/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_c43efz</id><link href=""https://www.reddit.com/r/tifu/comments/c43efz/tifu_by_putting_a_thong_that_was_not_my_wifes_in/"" /><updated>2019-06-23T11:53:10+00:00</updated><title>TIFU by putting a thong that was not my wife's in her underwear drawer</title></entry><entry><author><name>/u/notoriousdob</name><uri>https://www.reddit.com/user/notoriousdob</uri></author><category term=""RoastMe"" label=""r/RoastMe""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/RoastMe/comments/c436v6/40_year_old_make_up_artist_do_your_worst/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/Fn2h-uK7usFOnQvmM4gq9yTPg3P-CTS-8ttO7HOIIYE.jpg&quot; alt=&quot;40 year old make up artist. Do your worst.&quot; title=&quot;40 year old make up artist. Do your worst.&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/notoriousdob&quot;&gt; /u/notoriousdob &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/RoastMe/&quot;&gt; r/RoastMe &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/3szcqovie3631.jpg&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/RoastMe/comments/c436v6/40_year_old_make_up_artist_do_your_worst/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c436v6</id><link href=""https://www.reddit.com/r/RoastMe/comments/c436v6/40_year_old_make_up_artist_do_your_worst/"" /><updated>2019-06-23T11:36:08+00:00</updated><title>40 year old make up artist. Do your worst.</title></entry><entry><author><name>/u/Furki1907</name><uri>https://www.reddit.com/user/Furki1907</uri></author><category term=""gaming"" label=""r/gaming""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/gaming/comments/c47ggf/drlupo_raised_92034398_in_4h_30min_by_playing_a/&quot;&gt; &lt;img src=&quot;https://a.thumbs.redditmedia.com/PrkypEKQOBp1xUkLkO4ivsLqJCwI2eh3VTpSxmN13M0.jpg&quot; alt=&quot;DrLupo raised $920,343.98 in 4h 30min by playing a Video Game on Twitch for St. Jude.&quot; title=&quot;DrLupo raised $920,343.98 in 4h 30min by playing a Video Game on Twitch for St. Jude.&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/Furki1907&quot;&gt; /u/Furki1907 &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/gaming/&quot;&gt; r/gaming &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/gs1ox6z8x4631.jpg&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/gaming/comments/c47ggf/drlupo_raised_92034398_in_4h_30min_by_playing_a/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c47ggf</id><link href=""https://www.reddit.com/r/gaming/comments/c47ggf/drlupo_raised_92034398_in_4h_30min_by_playing_a/"" /><updated>2019-06-23T16:42:41+00:00</updated><title>DrLupo raised $920,343.98 in 4h 30min by playing a Video Game on Twitch for St. Jude.</title></entry><entry><author><name>/u/Tib3t</name><uri>https://www.reddit.com/user/Tib3t</uri></author><category term=""pics"" label=""r/pics""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/pics/comments/c429dm/the_one_group_you_always_stick_with/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/rx3Wc9YGELgHt_lMEf3PAArFEjTt6erRdgL3L27skEI.jpg&quot; alt=&quot;The one group you always stick with&quot; title=&quot;The one group you always stick with&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/Tib3t&quot;&gt; /u/Tib3t &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/pics/&quot;&gt; r/pics &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/8241qnxe03631.png&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/pics/comments/c429dm/the_one_group_you_always_stick_with/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c429dm</id><link href=""https://www.reddit.com/r/pics/comments/c429dm/the_one_group_you_always_stick_with/"" /><updated>2019-06-23T10:17:44+00:00</updated><title>The one group you always stick with</title></entry><entry><author><name>/u/thesesforty-three</name><uri>https://www.reddit.com/user/thesesforty-three</uri></author><category term=""politics"" label=""r/politics""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/politics/comments/c433m7/thanks_uncle_sam_after_tax_cuts_texas_instruments/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/V00KDtHi99ilfGmAqRK4tB2VEjNF8-Xgqm0sNXncfPQ.jpg&quot; alt=&quot;Thanks, Uncle Sam! After tax cuts, Texas Instruments spent $5 billion on stock — three times more than R&amp;amp;D&quot; title=&quot;Thanks, Uncle Sam! After tax cuts, Texas Instruments spent $5 billion on stock — three times more than R&amp;amp;D&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/thesesforty-three&quot;&gt; /u/thesesforty-three &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/politics/&quot;&gt; r/politics &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.dallasnews.com/opinion/commentary/2019/06/23/thanks-uncle-sam-after-tax-cuts-texas-instruments-spent-5-billion-stock-three-times-rd&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/politics/comments/c433m7/thanks_uncle_sam_after_tax_cuts_texas_instruments/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c433m7</id><link href=""https://www.reddit.com/r/politics/comments/c433m7/thanks_uncle_sam_after_tax_cuts_texas_instruments/"" /><updated>2019-06-23T11:28:55+00:00</updated><title>Thanks, Uncle Sam! After tax cuts, Texas Instruments spent $5 billion on stock — three times more than R&amp;D</title></entry><entry><author><name>/u/eatsleeprepeat101_</name><uri>https://www.reddit.com/user/eatsleeprepeat101_</uri></author><category term=""AskMen"" label=""r/AskMen""/><content type=""html"">&amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/eatsleeprepeat101_&quot;&gt; /u/eatsleeprepeat101_ &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/AskMen/&quot;&gt; r/AskMen &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/AskMen/comments/c42gk6/guys_of_redditwhat_are_some_things_you_are/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/AskMen/comments/c42gk6/guys_of_redditwhat_are_some_things_you_are/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_c42gk6</id><link href=""https://www.reddit.com/r/AskMen/comments/c42gk6/guys_of_redditwhat_are_some_things_you_are/"" /><updated>2019-06-23T10:35:06+00:00</updated><title>Guys of reddit,what are some things you are expected to do in a relationship but want girls to do more often?</title></entry><entry><author><name>/u/yomamascub</name><uri>https://www.reddit.com/user/yomamascub</uri></author><category term=""funny"" label=""r/funny""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/funny/comments/c47tx5/i_think_she_said_yes/&quot;&gt; &lt;img src=&quot;https://a.thumbs.redditmedia.com/rMn07IdIYlNLekm1P6ebg1xoKnNdR9HXho9OwfHg9h4.jpg&quot; alt=&quot;I think she said yes!&quot; title=&quot;I think she said yes!&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/yomamascub&quot;&gt; /u/yomamascub &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/funny/&quot;&gt; r/funny &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://v.redd.it/nnvw94nm15631&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/funny/comments/c47tx5/i_think_she_said_yes/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c47tx5</id><link href=""https://www.reddit.com/r/funny/comments/c47tx5/i_think_she_said_yes/"" /><updated>2019-06-23T17:07:24+00:00</updated><title>I think she said yes!</title></entry><entry><author><name>/u/Quebber</name><uri>https://www.reddit.com/user/Quebber</uri></author><category term=""pics"" label=""r/pics""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/pics/comments/c48623/my_wife_a_few_days_before_her_death_after_21/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/F8Qm02O6UwvbTxPsRAhfuYFqJZeFIEbHJPO6sUVcG6c.jpg&quot; alt=&quot;My Wife a few days before her death after 21 years of fighting Cancer.&quot; title=&quot;My Wife a few days before her death after 21 years of fighting Cancer.&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/Quebber&quot;&gt; /u/Quebber &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/pics/&quot;&gt; r/pics &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/ymu05ned55631.jpg&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/pics/comments/c48623/my_wife_a_few_days_before_her_death_after_21/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c48623</id><link href=""https://www.reddit.com/r/pics/comments/c48623/my_wife_a_few_days_before_her_death_after_21/"" /><updated>2019-06-23T17:29:38+00:00</updated><title>My Wife a few days before her death after 21 years of fighting Cancer.</title></entry><entry><author><name>/u/knobiknows</name><uri>https://www.reddit.com/user/knobiknows</uri></author><category term=""gifs"" label=""r/gifs""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/gifs/comments/c42ufb/a_reference_to_how_strong_chimpanzees_really_are/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/Fjj_wKrHU0vgKF-V8FC6AQUzi-Qyi5TaYDi3d4a_MTw.jpg&quot; alt=&quot;A reference to how strong chimpanzees really are&quot; title=&quot;A reference to how strong chimpanzees really are&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/knobiknows&quot;&gt; /u/knobiknows &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/gifs/&quot;&gt; r/gifs &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.imgur.com/tuVRb9n.gifv&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/gifs/comments/c42ufb/a_reference_to_how_strong_chimpanzees_really_are/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c42ufb</id><link href=""https://www.reddit.com/r/gifs/comments/c42ufb/a_reference_to_how_strong_chimpanzees_really_are/"" /><updated>2019-06-23T11:07:41+00:00</updated><title>A reference to how strong chimpanzees really are</title></entry><entry><author><name>/u/nokia621</name><uri>https://www.reddit.com/user/nokia621</uri></author><category term=""todayilearned"" label=""r/todayilearned""/><content type=""html"">&amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/nokia621&quot;&gt; /u/nokia621 &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/todayilearned/&quot;&gt; r/todayilearned &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://solvingprocrastination.com/why-people-procrastinate/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/todayilearned/comments/c42txr/til_human_procrastination_is_considered_a_complex/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_c42txr</id><link href=""https://www.reddit.com/r/todayilearned/comments/c42txr/til_human_procrastination_is_considered_a_complex/"" /><updated>2019-06-23T11:06:33+00:00</updated><title>TIL human procrastination is considered a complex psychological behavior because of the wide variety of reasons people do it. Although often attributed to &quot;laziness&quot;, research shows it is more likely to be caused by anxiety, depression, a fear of failure, or a reliance on abstract goals.</title></entry><entry><author><name>/u/Lagggging</name><uri>https://www.reddit.com/user/Lagggging</uri></author><category term=""mildlyinteresting"" label=""r/mildlyinteresting""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/mildlyinteresting/comments/c47x28/this_water_leaking_between_the_wall_and_paint/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/AYfTutHYutaQrea-12Zm-l0vi96WEpicpTpLD76k23g.jpg&quot; alt=&quot;This water leaking between the wall and paint&quot; title=&quot;This water leaking between the wall and paint&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/Lagggging&quot;&gt; /u/Lagggging &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/mildlyinteresting/&quot;&gt; r/mildlyinteresting &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/ptc593wm25631.jpg&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/mildlyinteresting/comments/c47x28/this_water_leaking_between_the_wall_and_paint/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c47x28</id><link href=""https://www.reddit.com/r/mildlyinteresting/comments/c47x28/this_water_leaking_between_the_wall_and_paint/"" /><updated>2019-06-23T17:13:04+00:00</updated><title>This water leaking between the wall and paint</title></entry><entry><author><name>/u/SuperPaki</name><uri>https://www.reddit.com/user/SuperPaki</uri></author><category term=""DestinyTheGame"" label=""r/DestinyTheGame""/><content type=""html"">&lt;!-- SC_OFF --&gt;&lt;div class=&quot;md&quot;&gt;&lt;p&gt;I’ve seen numerous amounts of comments saying how awful Titan supers are compared to other classes and honestly I think it’s the other way around. Apparently all titan supers have really bad hit detection, which makes no sense especially on console, were you always see a titan whether void, arc or solar always get 3-4 kills easily. &lt;/p&gt; &lt;p&gt;You have a super with high resilience that can not only shut down other supers, but then have the super energy remaining to pick up a few other kills. If you play comp you’ll quickly realise that the meta is striker Titan. &lt;/p&gt; &lt;p&gt;Then you have hammers which is great as you don’t need to get close to an opponent to get a kill. Shield is the same except you can block incoming damage and no scope three people with a ricocheting shield. Titans easily have the best supers right now across any class. Yet somehow Titans who don’t even what setup to use to get grenade kills in iron banner, think they’re underpowered and need a buff. &lt;/p&gt; &lt;p&gt;The only buff you need is PVE wise. Something to increase the damage on Thundercrash and something to increase the effectiveness of Ward of Dawn. I think we can all agree on that. But pointlessly saying that OEM is the only exotic that makes Titans viable in PvP is stupid, or saying how suppressor grenades are pointless because you are stuck using a void subclass which is somehow weak and further reinstating the fact that striker titan is a easy to counter super and is no where near the best. &lt;/p&gt; &lt;p&gt;TLDR; It is obvious that Titans need PVE buffs but stating that they are weak in PVP is just stupid.&lt;/p&gt; &lt;/div&gt;&lt;!-- SC_ON --&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/SuperPaki&quot;&gt; /u/SuperPaki &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/DestinyTheGame/&quot;&gt; r/DestinyTheGame &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/DestinyTheGame/comments/c41d8i/can_titans_stop_pretending_they_are_weak_in_pvp/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/DestinyTheGame/comments/c41d8i/can_titans_stop_pretending_they_are_weak_in_pvp/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_c41d8i</id><link href=""https://www.reddit.com/r/DestinyTheGame/comments/c41d8i/can_titans_stop_pretending_they_are_weak_in_pvp/"" /><updated>2019-06-23T09:01:05+00:00</updated><title>Can Titans stop pretending they are weak in PvP.</title></entry><entry><author><name>/u/The-Autarkh</name><uri>https://www.reddit.com/user/The-Autarkh</uri></author><category term=""PoliticalHumor"" label=""r/PoliticalHumor""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/PoliticalHumor/comments/c42xb3/well_calibrated_outrageometer/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/2TZW3fRKDuGKUbGvBa0RJ5EFl2TNJS_XVUKPRk9nQEQ.jpg&quot; alt=&quot;Well calibrated Outrage-o-meter&quot; title=&quot;Well calibrated Outrage-o-meter&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/The-Autarkh&quot;&gt; /u/The-Autarkh &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/PoliticalHumor/&quot;&gt; r/PoliticalHumor &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.imgur.com/HCjMRHb.jpg&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/PoliticalHumor/comments/c42xb3/well_calibrated_outrageometer/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c42xb3</id><link href=""https://www.reddit.com/r/PoliticalHumor/comments/c42xb3/well_calibrated_outrageometer/"" /><updated>2019-06-23T11:14:10+00:00</updated><title>Well calibrated Outrage-o-meter</title></entry><entry><author><name>/u/champagnesprn</name><uri>https://www.reddit.com/user/champagnesprn</uri></author><category term=""OldSchoolCool"" label=""r/OldSchoolCool""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/OldSchoolCool/comments/c46xy6/tony_hawk_1988/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/B5PTUvYMs6LodgeCt0iH_UkCmMET9vsEbzvssfOfx0I.jpg&quot; alt=&quot;Tony Hawk, 1988.&quot; title=&quot;Tony Hawk, 1988.&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/champagnesprn&quot;&gt; /u/champagnesprn &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/OldSchoolCool/&quot;&gt; r/OldSchoolCool &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/hfhks1gzq4631.png&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/OldSchoolCool/comments/c46xy6/tony_hawk_1988/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c46xy6</id><link href=""https://www.reddit.com/r/OldSchoolCool/comments/c46xy6/tony_hawk_1988/"" /><updated>2019-06-23T16:08:00+00:00</updated><title>Tony Hawk, 1988.</title></entry><entry><author><name>/u/ManiaforBeatles</name><uri>https://www.reddit.com/user/ManiaforBeatles</uri></author><category term=""worldnews"" label=""r/worldnews""/><content type=""html"">&amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/ManiaforBeatles&quot;&gt; /u/ManiaforBeatles &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/worldnews/&quot;&gt; r/worldnews &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.independent.co.uk/environment/un-climate-talks-in-bonn-saudi-arabia-15c-ipcc-rejects-a8970481.html&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/worldnews/comments/c428ps/a_major_study_on_how_to_limit_global_warming/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_c428ps</id><link href=""https://www.reddit.com/r/worldnews/comments/c428ps/a_major_study_on_how_to_limit_global_warming/"" /><updated>2019-06-23T10:16:07+00:00</updated><title>A major study on how to limit global warming could be dropped from formal UN climate talks in Bonn this week after a “gentlemen’s agreement” was made under pressure from Saudi Arabia.</title></entry><entry><author><name>/u/Jenny86-75-309</name><uri>https://www.reddit.com/user/Jenny86-75-309</uri></author><category term=""legaladvice"" label=""r/legaladvice""/><content type=""html"">&lt;!-- SC_OFF --&gt;&lt;div class=&quot;md&quot;&gt;&lt;p&gt;Just want to say this is going to sound insane but I am not trolling/making it up.&lt;/p&gt; &lt;p&gt;Me and this guy were together for 3 years. We&amp;#39;re 19 now. We never talked about the future until a year in. Before then I&amp;#39;d try to discuss it and he would change the subject.&lt;/p&gt; &lt;p&gt;In short, I wanted kids, he didn&amp;#39;t. I wanted to stay in Ireland, he didn&amp;#39;t. I wanted to get married, he didn&amp;#39;t. When I realised it was a bad match I broke it off but he asked to get back together, saying we were too young to worry about the future, and we got back together. This happened a few times over the next 18 months In this time we probably spent more time apart than together.&lt;/p&gt; &lt;p&gt;We knew early on that we wanted different things, and while marriage and kids are something I would want further down the line, I wasn&amp;#39;t concerned about his open dislike of marriage and kids because 19 is too young to get married anyway. If I were dating him 10 or 15 years from now and he didn&amp;#39;t want to get married, then I might have an issue, but when I&amp;#39;m barely out of my teens, it&amp;#39;s not a huge concern, and just loving him and wanting to be with him is enough.&lt;/p&gt; &lt;p&gt;When I went to see him this time I was again, considering breaking up with him. Aside from the long term issues, he is also inconsiderate and has a mean streak that I really don&amp;#39;t like. So when I arrived at his place, mentally debating breaking up with him for good this time, and he told me he wanted to take me out to dinner, I assumed he wanted to break up too, publicly so I wouldn&amp;#39;t cause a scene. I don&amp;#39;t normally cause a scene but being in public tends to prevent yelling, and we both yell every time we break up.&lt;/p&gt; &lt;p&gt;In the last year he has asked me 2 things that looking back now stand out to me. First he asked about jewellery, what kind of stuff I liked and what my taste was. I assumed it was because my birthday was coming up and he was getting me a bracelet or something so I told him simple and delicate, silver or steel rather than gold, if there was a colour then blue, nothing flashy or expensive as both my job prefers plain jewellery and it&amp;#39;s just my personal preference. I also said &amp;quot;go cheap&amp;quot;.&lt;/p&gt; &lt;p&gt;The other thing he asked me was how I felt about public proposals. I told him immediately that I, personally, disliked them as I felt I wouldn&amp;#39;t be able to say no, even if I wanted to. I feel like public proposals are OK when they&amp;#39;ve been specifically requested and agreed on, but one that&amp;#39;s totally out of the blue is not OK at all. I assumed he was asking about this because his friend had just proposed to his girlfriend of several years, publicly, and she&amp;#39;d accepted but admitted to him after that she would have preferred something private.&lt;/p&gt; &lt;p&gt;I never thought in a million years that he would propose.&lt;/p&gt; &lt;p&gt;So you can imagine my shock when we went to dinner and the first thing he did was propose.&lt;/p&gt; &lt;p&gt;The ring was huge, gold, gaudy, red gems around a diamond and the whole thing was the size and shape of a super bowl ring. He got on one knee, and held it out to me. We were in the middle of this popular restaurant and the place was packed. Everyone there could see what was going on and wasn&amp;#39;t even trying to hide that they were looking at us.&lt;/p&gt; &lt;p&gt;I said no.&lt;/p&gt; &lt;p&gt;Well, I didn&amp;#39;t so much say &amp;quot;no&amp;quot;. I ran out of the restaurant.&lt;/p&gt; &lt;p&gt;He drove me there, so I got a cab back, and drove home that night.&lt;/p&gt; &lt;p&gt;I realise running out wasn&amp;#39;t the best thing to do but I didn&amp;#39;t know what else to do. I could feel everyone&amp;#39;s eyes on me, and all I knew was that I didn&amp;#39;t want to marry him or accept his proposal. I felt like I couldn&amp;#39;t even speak, I was so upset about the whole thing. So I just got up and ran.&lt;/p&gt; &lt;p&gt;I just want to take the opportunity to say here that I really really don&amp;#39;t care about the rings. Honestly, when I want to get married (which is absolutely not when I&amp;#39;m 19 years old) the right person could just turn to me and say &amp;quot;wanna get married?&amp;quot; and I&amp;#39;d say yes. I wouldn&amp;#39;t even need a ring. I know I&amp;#39;m focusing on the ring and the public proposal a lot, but that&amp;#39;s only because of 1) how far away it was from what I&amp;#39;d told him my taste was and 2) what happened next.&lt;/p&gt; &lt;p&gt;I didn&amp;#39;t hear from him until a few weeks later. He said that he thought a proposal was something I&amp;#39;d want, but he saw now that it wasn&amp;#39;t. He said that he was out of pocket for the rings. He&amp;#39;d bought us both the same one and gotten them engraved. He linked me to the jeweller&amp;#39;s website and the ring was up for €1450 (about $1650/£1300). When I asked why he was telling me this he said that he&amp;#39;d hoped I would cover the cost of mine. He said that as they&amp;#39;d been engraved he couldn&amp;#39;t get a refund. He&amp;#39;d hoped that I would say yes to the proposal, in which case he wouldn&amp;#39;t have asked me to pay, but I said no. He also said I&amp;#39;d embarrassed him by saying no in public, and should have said yes, and if I was really against it waited to say no when we were alone.&lt;/p&gt; &lt;p&gt;We both live in small towns where gossip spreads at church. Enough people were at the restaurant that night that both of us got asked about it at church on Sunday.&lt;/p&gt; &lt;p&gt;He has since messaged me saying he&amp;#39;s debating calling in a lawyer to sue me for the cost of my ring (€1450) and he also says that I have caused him &amp;quot;emotional distress&amp;quot; by turning him down in public, and have publicly humiliated him for both rejecting his proposal in public and leaving him to deal with church gossip, which I had no part in spreading. He says he can also get some money off me over those other 2 things. So €1450 for what he spent on my ring, and extra money for emotional distress and public humiliation.&lt;/p&gt; &lt;p&gt;I think his legal claims are all bullshit, and he wouldn&amp;#39;t have a leg to stand on in a court of law, but I am not a law student or a lawyer. He is studying law currently and has an internship at a legal firm.&lt;/p&gt; &lt;p&gt;What do I do about all of this? Do I need to prepare myself for a lawsuit or is it not even worth worrying about?&lt;/p&gt; &lt;p&gt;Thanks in advance :)&lt;/p&gt; &lt;p&gt;&amp;#x200B;&lt;/p&gt; &lt;p&gt;EDIT: He knew I wanted to get married but not to him. He not only knew this but said he had no intention of marrying me, either. He openly despised marriage right up until the time he proposed and he knows that we want different things out of marriage, and I told him that this was why I was breaking up with him the times before this that I have ended the relationship. During the course of our relationship he&amp;#39;s also said stuff like &amp;quot;we&amp;#39;re still too young to think about marriage&amp;quot;, &amp;quot;it&amp;#39;s not like we&amp;#39;re getting married&amp;quot; and (my favourite) &amp;quot;it&amp;#39;s not like I&amp;#39;m gonna propose&amp;quot; (that one was last April)&lt;/p&gt; &lt;/div&gt;&lt;!-- SC_ON --&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/Jenny86-75-309&quot;&gt; /u/Jenny86-75-309 &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/legaladvice/&quot;&gt; r/legaladvice &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/legaladvice/comments/c43tve/my_ex_proposed_i_said_no_he_is_now_talking_about/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/legaladvice/comments/c43tve/my_ex_proposed_i_said_no_he_is_now_talking_about/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_c43tve</id><link href=""https://www.reddit.com/r/legaladvice/comments/c43tve/my_ex_proposed_i_said_no_he_is_now_talking_about/"" /><updated>2019-06-23T12:26:42+00:00</updated><title>My ex proposed. I said no. He is now talking about suing me. Advice?</title></entry><entry><author><name>/u/cosmicnate</name><uri>https://www.reddit.com/user/cosmicnate</uri></author><category term=""TheMonkeysPaw"" label=""r/TheMonkeysPaw""/><content type=""html"">&amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/cosmicnate&quot;&gt; /u/cosmicnate &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/TheMonkeysPaw/&quot;&gt; r/TheMonkeysPaw &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/TheMonkeysPaw/comments/c424cq/i_wish_askouji_and_themonkeyspaw_would_have_a/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/TheMonkeysPaw/comments/c424cq/i_wish_askouji_and_themonkeyspaw_would_have_a/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_c424cq</id><link href=""https://www.reddit.com/r/TheMonkeysPaw/comments/c424cq/i_wish_askouji_and_themonkeyspaw_would_have_a/"" /><updated>2019-06-23T10:05:59+00:00</updated><title>I wish AskOuji and TheMonkeysPaw would have a mash up episode where the answer was only one word at a time.</title></entry><entry><author><name>/u/MrCrash2U</name><uri>https://www.reddit.com/user/MrCrash2U</uri></author><category term=""funny"" label=""r/funny""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/funny/comments/c448el/getting_a_tan/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/462N6lfhSCKgUQ6xckLdx2wh8RpARctlR30AqbYDVtE.jpg&quot; alt=&quot;Getting a tan&quot; title=&quot;Getting a tan&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/MrCrash2U&quot;&gt; /u/MrCrash2U &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/funny/&quot;&gt; r/funny &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://v.redd.it/rf62obn1t3631&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/funny/comments/c448el/getting_a_tan/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c448el</id><link href=""https://www.reddit.com/r/funny/comments/c448el/getting_a_tan/"" /><updated>2019-06-23T12:57:39+00:00</updated><title>Getting a tan</title></entry><entry><author><name>/u/OPG609</name><uri>https://www.reddit.com/user/OPG609</uri></author><category term=""gaming"" label=""r/gaming""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/gaming/comments/c45owi/still_cant_believe_this_is_a_current_gen_game/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/Hy8MusbQH1T1e-oGsUzk3vF0ZnKNGF5tPHb735a4e5o.jpg&quot; alt=&quot;Still Can't Believe This Is A Current Gen Game...&quot; title=&quot;Still Can't Believe This Is A Current Gen Game...&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/OPG609&quot;&gt; /u/OPG609 &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/gaming/&quot;&gt; r/gaming &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/97csiz1xa4631.gif&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/gaming/comments/c45owi/still_cant_believe_this_is_a_current_gen_game/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c45owi</id><link href=""https://www.reddit.com/r/gaming/comments/c45owi/still_cant_believe_this_is_a_current_gen_game/"" /><updated>2019-06-23T14:41:39+00:00</updated><title>Still Can't Believe This Is A Current Gen Game...</title></entry><entry><author><name>/u/OrwinBeane</name><uri>https://www.reddit.com/user/OrwinBeane</uri></author><category term=""memes"" label=""r/memes""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/memes/comments/c442tv/classic_germany/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/7CfMwdFXYz2ROLKhT_bwg4mQW_fV3nwDCzoQJbtOFOs.jpg&quot; alt=&quot;Classic Germany&quot; title=&quot;Classic Germany&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/OrwinBeane&quot;&gt; /u/OrwinBeane &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/memes/&quot;&gt; r/memes &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/ufy91pdzq3631.jpg&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/memes/comments/c442tv/classic_germany/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c442tv</id><link href=""https://www.reddit.com/r/memes/comments/c442tv/classic_germany/"" /><updated>2019-06-23T12:45:47+00:00</updated><title>Classic Germany</title></entry><entry><author><name>/u/meatygonzales</name><uri>https://www.reddit.com/user/meatygonzales</uri></author><category term=""nostalgia"" label=""r/nostalgia""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/nostalgia/comments/c46xvo/update_daniel_has_recieved_the_dragon_tales_plate/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/NNoadEsy0ZjWKXST-gUM_jMNvZGzZ4FP4H0drwjpF0Y.jpg&quot; alt=&quot;Update: Daniel has recieved the Dragon Tales plate! Thank you so much for everyones support and help!&quot; title=&quot;Update: Daniel has recieved the Dragon Tales plate! Thank you so much for everyones support and help!&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/meatygonzales&quot;&gt; /u/meatygonzales &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/nostalgia/&quot;&gt; r/nostalgia &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/8u1l9qnoq4631.jpg&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/nostalgia/comments/c46xvo/update_daniel_has_recieved_the_dragon_tales_plate/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_c46xvo</id><link href=""https://www.reddit.com/r/nostalgia/comments/c46xvo/update_daniel_has_recieved_the_dragon_tales_plate/"" /><updated>2019-06-23T16:07:52+00:00</updated><title>Update: Daniel has recieved the Dragon Tales plate! Thank you so much for everyones support and help!</title></entry></feed>";
            //return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><rss version=""2.0"" xmlns:media=""http://search.yahoo.com/mrss/""><channel><generator>NFE/5.0</generator><title>Top stories - Google News</title><link>https://news.google.com/?hl=en-US&amp;gl=US&amp;ceid=US:en</link><language>en-US</language><webMaster>news-webmaster@google.com</webMaster><copyright>2019 Google Inc.</copyright><lastBuildDate>Fri, 05 Jul 2019 01:44:09 GMT</lastBuildDate><description>Google News</description><item><title>'Nothing America cannot do': Donald Trump touts U.S. military strength in 4th of July speech - USA TODAY</title><link>https://www.usatoday.com/story/news/politics/2019/07/04/independence-day-donald-trump-plans-stress-military-strength-holiday-speech/1648314001/</link><guid isPermaLink=""false"">52780326716600</guid><pubDate>Thu, 04 Jul 2019 23:01:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.usatoday.com/story/news/politics/2019/07/04/independence-day-donald-trump-plans-stress-military-strength-holiday-speech/1648314001/"" target=""_blank""&gt;'Nothing America cannot do': Donald Trump touts U.S. military strength in 4th of July speech&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;USA TODAY&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.cnn.com/politics/live-news/trump-washington-dc-july-4-2019/index.html"" target=""_blank""&gt;Live updates: Donald Trump's July 4th celebration&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.foxnews.com/politics/trumps-salute-to-america-brings-military-might-fireworks-and-a-health-dose-of-controversy-to-washington"" target=""_blank""&gt;Trump's 'Salute to America' brings military might, fireworks and dose of controversy to Washington&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Fox News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.washingtonpost.com/opinions/trumps-salute-to-america-was-a-made-for-television-reelection-event/2019/07/04/b58c9b34-9eaa-11e9-b27f-ed2942f73d70_story.html"" target=""_blank""&gt;Trump tried to make Independence Day all about him. He ended up looking small.&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The Washington Post&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=smEFprOMvBk"" target=""_blank""&gt;White House and Pentagon won't disclose cost of Trump's July 4th event&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CBS This Morning&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWk0aVlDRmpvQU1FUnFjQmhFZGVWZTdLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.usatoday.com"">USA TODAY</source><media:content url=""https://lh6.googleusercontent.com/proxy/yoxm4-Cpv0HpFJSCmJKASR6vvfKIHxu85QxPm9pXBdDZ33vopYQZpuuTCCuewaqYglxy0OraVqBI2FO9VDCuyM1OTfZ_IJbGiN1wSXS4_NSKIhWtmHFceQ9P6rHdwyMXm_w2bG2qdATOx-GtIIJDwvBfFWwlpHkJ-w=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>6.4 magnitude earthquake rattles Southern California; no injuries reported - 10TV</title><link>https://www.10tv.com/article/64-magnitude-earthquake-rattles-southern-california-no-injuries-reported-2019-jul</link><guid isPermaLink=""false"">52780326532802</guid><pubDate>Thu, 04 Jul 2019 18:13:11 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.10tv.com/article/64-magnitude-earthquake-rattles-southern-california-no-injuries-reported-2019-jul"" target=""_blank""&gt;6.4 magnitude earthquake rattles Southern California; no injuries reported&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;10TV&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.cbsnews.com/live-news/earthquake-california-today-ridgecrest-strikes-near-los-angeles-2019-07-04-live-updates/"" target=""_blank""&gt;Earthquake in California today: 6.4 magnitude earthquake strikes Southern California town of Ridgecrest near Los Angeles - live updates&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CBS News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.express.co.uk/news/world/1149298/california-earthquake-today-ridgecrest-ca-news-latest"" target=""_blank""&gt;California earthquake: Where is Ridgecrest CA? Are there any casualties?&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Express.co.uk&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.sfgate.com/news/us/article/The-Latest-6-6-quake-rattles-Southern-14071940.php"" target=""_blank""&gt;The Latest: Police, fire officials say they have resources&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;SF Gate&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.latimes.com/local/lanow/la-me-earthquake-california-shake-quake-20190704-story.html"" target=""_blank""&gt;Strongest earthquake in years rattles Southern California; damage reported&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Los Angeles Times&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqfQgKIndDQklTVXpvSmMzUnZjbmt0TXpZd1NrWUtFUWpDN2ZTRWpvQU1FVnBsUUZrS085aXlFakUyTGpRZ2JXRm5ibWwwZFdSbElHVmhjblJvY1hWaGEyVWdhR2wwY3lCVGIzVjBhR1Z5YmlCRFlXeHBabTl5Ym1saEtBQVAB?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.10tv.com"">10TV</source><media:content url=""https://lh5.googleusercontent.com/proxy/bL2iwtlw-eQO_y42-rWxNmml7we2u_lnBVpmB4cZTtjH3eK_cerp7j48EGXpj4xffL3JKdkC91tRi0fmgL0GYRcO3GVrN2eVZjKgqL7jqFSZJdEIIpkw-7tjowWKhRz6ZtpQ053tPNgQx9gQQi_45-nZAADU7IXJ8HHuvYQyrNHfaIPOidwWZGe_HGobgwLHzS46ZI_pjgc=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>Trump at his rainy July 4th event: Americans 'part of one of the greatest stories ever told' - CNN</title><link>https://www.cnn.com/2019/07/04/politics/salute-to-america-july-4th-event/index.html</link><guid isPermaLink=""false"">CBMiU2h0dHBzOi8vd3d3LmNubi5jb20vMjAxOS8wNy8wNC9wb2xpdGljcy9zYWx1dGUtdG8tYW1lcmljYS1qdWx5LTR0aC1ldmVudC9pbmRleC5odG1s0gEA</guid><pubDate>Fri, 05 Jul 2019 00:38:00 GMT</pubDate><description>&lt;a href=""https://www.cnn.com/2019/07/04/politics/salute-to-america-july-4th-event/index.html"" target=""_blank""&gt;Trump at his rainy July 4th event: Americans 'part of one of the greatest stories ever told'&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNN&lt;/font&gt;&lt;p&gt;With jets roaring overhead and a poncho-clad crowd before him, President Donald Trump implored Americans to come together Thursday in a show of national ...&lt;/p&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWprM1lDRWpvQU1FWmp3OGRwQW05b1VLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;</description><source url=""https://www.cnn.com"">CNN</source><media:content url=""https://lh4.googleusercontent.com/proxy/ev84KsKCb9tRLsQyhjaNquLACkoWIHEjso8Oibp5Zve-0PL4baENveLARgTiiz4kNVvcvLCtBXrqC1w8BQU3pCgIzQvKD1ZC5SuQ7GuxQ2S9rIhAWOl5p4qXgwjXn8IJz2GregcDZgYEFiZlAL9OikQ=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>White House exploring executive order to deal with census citizenship question - ABC News</title><link>https://abcnews.go.com/Politics/white-house-exploring-executive-order-deal-census-citizenship/story?id=64137382</link><guid isPermaLink=""false"">52780325711763</guid><pubDate>Thu, 04 Jul 2019 22:46:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://abcnews.go.com/Politics/white-house-exploring-executive-order-deal-census-citizenship/story?id=64137382"" target=""_blank""&gt;White House exploring executive order to deal with census citizenship question&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;ABC News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.foxnews.com/politics/trump-citizenship-2020-census"" target=""_blank""&gt;Trump doubles down on citizenship question for 2020 census, reportedly mulls executive order&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Fox News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.wsj.com/articles/white-house-to-look-at-every-option-to-get-citizenship-question-on-census-11562272647"" target=""_blank""&gt;White House to Look at ‘Every Option’ to Get Citizenship Question on Census&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Wall Street Journal&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.washingtonpost.com/opinions/2019/07/04/worst-part-about-dojs-reversal-census-is-lack-deliberation/"" target=""_blank""&gt;The worst part about DOJ’s reversal on the census is the lack of deliberation&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The Washington Post&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.cnn.com/2019/07/04/opinions/trump-census-delay-disrespect-reyes/index.html"" target=""_blank""&gt;The real problem of Trump's citizenship question&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWlUMzhLRWpvQU1FVjMzVllSS3B3XzlLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://abcnews.go.com"">ABC News</source><media:content url=""https://lh5.googleusercontent.com/proxy/JlAW-B-Ws2CVk4-gbrJhLkF5ZDBjPfLXk89KLzCTS2D94N9PaS5DL8piR3UR3x6C4r5CQzeEpJX2IxURnYCjP_L6BMYYUfdtt9abEhoaRZ4CTri26eLKcr_hebFrZOj_SkoqGh-L7KDRyapSIlCXDxel1nXXRw=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>Tanker full of Iranian oil intercepted en route to Syria - New York Post </title><link>https://nypost.com/2019/07/04/tanker-full-of-iranian-oil-intercepted-en-route-to-syria/</link><guid isPermaLink=""false"">52780326163946</guid><pubDate>Thu, 04 Jul 2019 18:39:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://nypost.com/2019/07/04/tanker-full-of-iranian-oil-intercepted-en-route-to-syria/"" target=""_blank""&gt;Tanker full of Iranian oil intercepted en route to Syria&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;New York Post &lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.bbc.com/news/uk-48871462"" target=""_blank""&gt;Iran summons UK ambassador in tanker seizure row&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;BBC News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=HNiQy11PV4Y"" target=""_blank""&gt;Royal Marines detain tanker in Gibraltar suspected of carrying Iranian oil to Syria&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The Telegraph&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.reuters.com/article/us-mideast-iran-tanker/tehran-fumes-as-britain-seizes-iranian-oil-tanker-over-syria-sanctions-idUSKCN1TZ0GN"" target=""_blank""&gt;Tehran fumes as Britain seizes Iranian oil tanker over Syria sanctions&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Reuters&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.bbc.com/news/uk-48865030"" target=""_blank""&gt;Oil tanker bound for Syria detained in Gibraltar&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;BBC News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWpxcTk2RWpvQU1FZENHd09mbEp3MjFLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://nypost.com"">New York Post </source></item><item><title>Libya migrants recount horrors of Tajoura detention centre attack - Al Jazeera English</title><link>https://www.aljazeera.com/news/2019/07/libya-migrants-recount-horrors-tajoura-detention-centre-attack-190704053421671.html</link><guid isPermaLink=""false"">52780324675965</guid><pubDate>Thu, 04 Jul 2019 08:05:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.aljazeera.com/news/2019/07/libya-migrants-recount-horrors-tajoura-detention-centre-attack-190704053421671.html"" target=""_blank""&gt;Libya migrants recount horrors of Tajoura detention centre attack&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Al Jazeera English&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.bbc.com/news/world-africa-48868992"" target=""_blank""&gt;Many missing as migrant boat sinks off Tunisia&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;BBC News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.aljazeera.com/news/2019/07/reports-libyan-guards-shot-migrants-fleeing-air-strikes-190704131751142.html"" target=""_blank""&gt;UN reports Libyan guards shot at migrants fleeing air raids&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Aljazeera.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.nytimes.com/2019/07/03/opinion/libya-airstrike.html"" target=""_blank""&gt;They Hoped to Reach Europe Before They Were Massacred&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The New York Times&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=l7xBdy3MnLk"" target=""_blank""&gt;UN agency condemns airstrike on migrant center in Libya&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWo5d29PRWpvQU1FUXhJNWNlakFINWJLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.aljazeera.com"">Al Jazeera English</source></item><item><title>Alaskan heat wave could rise to record levels for Independence Day - CNN</title><link>https://www.cnn.com/2019/07/04/us/alaska-record-heat-trnd-wxc/index.html</link><guid isPermaLink=""false"">52780325939880</guid><pubDate>Thu, 04 Jul 2019 23:06:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.cnn.com/2019/07/04/us/alaska-record-heat-trnd-wxc/index.html"" target=""_blank""&gt;Alaskan heat wave could rise to record levels for Independence Day&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://thehill.com/policy/energy-environment/451692-anchorage-cancels-fourth-of-july-fireworks-due-to-extreme-heat-wave"" target=""_blank""&gt;Anchorage cancels Fourth of July fireworks due to extreme heat wave | TheHill&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The Hill&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.nytimes.com/2019/07/04/us/alaska-heat-anchorage-fireworks.html"" target=""_blank""&gt;Anchorage Has Never Reached 90 Degrees. That Could Change This Week.&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The New York Times&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.ktva.com/story/40741666/record-heat-brings-concern-for-children-and-pets"" target=""_blank""&gt;Record heat brings concern for children and pets&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;KTVA&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.huffpost.com/entry/record-heat-europe-france-alaska-climate-change_n_5d1cf9bbe4b04c48140e23fa"" target=""_blank""&gt;Scorching Temperatures Smash Records In U.S., Europe&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;HuffPost&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWlvMWRDRWpvQU1FYUpxakNBY2lfNlRLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.cnn.com"">CNN</source><media:content url=""https://lh6.googleusercontent.com/proxy/inLKaZE8XtfMNvKR14FYypclpaBOhMRUTsDsK6H43nFViUV3VqkuNWgTnuQ3VtKbSKvbDqdcXslvtCehrIu3eYluTXdrircfvmV2-Xq_fTnkJHa8AyMiOGm8uhyT5zT4nie68u6m-FXHcOGU9DsPfj8Zx7HVDItmGYQrOXnH63wv3IAUW4jbZzG-sES-WQ=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>GCFR responding to possible drowning following boat accident - wpde.com</title><link>http://wpde.com/news/local/gcfr-responding-to-possible-drowning-following-boat-accident</link><guid isPermaLink=""false"">52780326556023</guid><pubDate>Thu, 04 Jul 2019 21:06:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""http://wpde.com/news/local/gcfr-responding-to-possible-drowning-following-boat-accident"" target=""_blank""&gt;GCFR responding to possible drowning following boat accident&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;wpde.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.usatoday.com/story/news/nation/2019/07/04/lightning-strike-along-south-carolina-river-causes-injuries/1652232001/"" target=""_blank""&gt;One dead, others injured in lightning strike near South Carolina river&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;USA TODAY&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.wyff4.com/article/one-killed-as-many-as-12-injured-in-sc-lightning-strike/28294661"" target=""_blank""&gt;One killed, as many as 12 injured in SC lightning strike&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;WYFF4 Greenville&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""http://abcnews4.com/news/local/crews-on-scene-of-boating-accident-in-georgetown-harbor"" target=""_blank""&gt;Crews on scene of boating accident in Georgetown Harbor&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;ABC NEWS 4&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.wral.com/lightning-strike-during-july-fourth-cookout-kills-1-sends-2-to-hospital/18491789/"" target=""_blank""&gt;Lightning strike during July Fourth cookout kills 1, sends 2 to hospital&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;WRAL.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWozb3ZhRWpvQU1FUjVxUjZDT1VESXZLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""http://wpde.com"">wpde.com</source><media:content url=""https://lh3.googleusercontent.com/proxy/llb6rkZdyXsEkBvDdQKs1r-VbKMa2GcxzOi1o6ofLLGki0BNltzc9W4jKspU5Q0evcugmJMIT-siXeqFRqenlL39w37aE68VyHO1eYDIncyI3XP4Ng1ehn1a4dWnOT1gVIaJXfrIocAAUNen6tNmQElZzOBWSdFfMq8ipX6LR_8WXtiVg20ej4Wx4N5LbR1K4cxul4_eGsTFHoxYO8N6OnsZDO14wj3xXqD8X2Fo1VZzGw=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>1 patient died, 5 others infected from mold in operating room of Seattle Children's hospital - NBC News</title><link>https://www.nbcnews.com/news/us-news/1-patient-died-5-others-infected-mold-operating-room-seattle-n1026596</link><guid isPermaLink=""false"">52780326212745</guid><pubDate>Thu, 04 Jul 2019 19:33:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.nbcnews.com/news/us-news/1-patient-died-5-others-infected-mold-operating-room-seattle-n1026596"" target=""_blank""&gt;1 patient died, 5 others infected from mold in operating room of Seattle Children's hospital&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;NBC News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.dailymail.co.uk/health/article-7214383/Seattle-hospital-reopen-operating-rooms-mold-outbreak-killed-1.html"" target=""_blank""&gt;Seattle hospital to reopen all operating rooms after mold outbreak that killed 1&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Daily Mail&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.forbes.com/sites/brucelee/2019/07/04/here-is-the-mold-found-in-seattle-childrens-hospital/"" target=""_blank""&gt;Here Is The Mold Found In Seattle Children's Hospital&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Forbes&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWlKcWVHRWpvQU1FVE82ZmZ2dURFdzRLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.nbcnews.com"">NBC News</source></item><item><title>Several people stabbed at Virginia plasma center - CNN</title><link>https://www.cnn.com/2019/07/04/us/petersburg-virginia-stabbing-plasma-center/index.html</link><guid isPermaLink=""false"">52780326627505</guid><pubDate>Fri, 05 Jul 2019 01:30:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.cnn.com/2019/07/04/us/petersburg-virginia-stabbing-plasma-center/index.html"" target=""_blank""&gt;Several people stabbed at Virginia plasma center&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.wkbn.com/news/reports-several-stabbed-at-blood-plasma-center-in-virginia/"" target=""_blank""&gt;Reports: Several stabbed by “cutting instrument” at blood plasma center in Virginia&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;WKBN.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.wric.com/news/breaking-news/3-injured-in-attack-at-petersburg-plasma-center/"" target=""_blank""&gt;3 injured in stabbing attack at Petersburg plasma center&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;8News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://wtvr.com/2019/07/04/petersburg-plasma-center-shooting-stabbing-3200-block-of-crater-road/"" target=""_blank""&gt;3 stabbed at Petersburg plasma center; suspect in custody&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;WTVR CBS 6 News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.cbsnews.com/news/virginia-stabbing-today-petersburg-octapharma-plasma-plasma-center-several-injured-suspect-custody-2019-07-04/"" target=""_blank""&gt;Virginia stabbing: Suspect in custody after stabbing leaves several injured at Petersburg plasma center&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CBS News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWl4MGZxRWpvQU1FWUNVTXMyTXVqWndLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.cnn.com"">CNN</source></item><item><title>Mexico’s Federal Police Rebel Against New Security Plan - The New York Times</title><link>https://www.nytimes.com/2019/07/04/world/americas/mexico-police-protest.html</link><guid isPermaLink=""false"">52780326463463</guid><pubDate>Thu, 04 Jul 2019 23:48:16 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.nytimes.com/2019/07/04/world/americas/mexico-police-protest.html"" target=""_blank""&gt;Mexico’s Federal Police Rebel Against New Security Plan&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The New York Times&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.washingtonpost.com/world/the_americas/mexicos-amlo-struggles-to-contain-police-uprising/2019/07/04/0cea814a-9e5e-11e9-83e3-45fded8e8d2e_story.html"" target=""_blank""&gt;Mexico’s López Obrador struggles to contain police uprising&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Houston Chronicle &lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWpuel9DRWpvQU1FVU5JSE50MWhVRTRLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.nytimes.com"">The New York Times</source></item><item><title>Squeezed by U.S. Sanctions, Iran Shifts From Patience to Confrontation - Wall Street Journal</title><link>https://www.wsj.com/articles/squeezed-by-u-s-sanctions-iran-shifts-from-patience-to-confrontation-11562270731</link><guid isPermaLink=""false"">52780326276702</guid><pubDate>Thu, 04 Jul 2019 20:05:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.wsj.com/articles/squeezed-by-u-s-sanctions-iran-shifts-from-patience-to-confrontation-11562270731"" target=""_blank""&gt;Squeezed by U.S. Sanctions, Iran Shifts From Patience to Confrontation&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Wall Street Journal&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.nytimes.com/2019/07/04/world/middleeast/iran-us-zarif-nuclear-deal.html"" target=""_blank""&gt;He Enjoys American Coffee and Restaurants. Is He a Credible Negotiator for Iran?&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The New York Times&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=mhfPWqgItgE"" target=""_blank""&gt;Europe says no sanctions for Iran, but calls for cooperation&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Al Jazeera English&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.cnbc.com/2019/07/02/russia-energy-minister-novak-calls-sanctions-against-iran-unlawful.html"" target=""_blank""&gt;Russian energy minister says tension in the Persian Gulf is not the fault of Iran&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNBC&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.nytimes.com/2019/07/04/world/middleeast/iran-zarif-interview.html"" target=""_blank""&gt;In His Own Words: Iran’s Foreign Minister, Mohammad Javad Zarif&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The New York Times&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWplbk9XRWpvQU1FVzQ5akJPTkhTbFJLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.wsj.com"">Wall Street Journal</source></item><item><title>How Hong Kong protesters used hand signs and human chains to storm government - Guardian News</title><link>https://www.youtube.com/watch?v=n9V9EzHrfmE</link><guid isPermaLink=""false"">52780326606017</guid><pubDate>Thu, 04 Jul 2019 12:17:04 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=n9V9EzHrfmE"" target=""_blank""&gt;How Hong Kong protesters used hand signs and human chains to storm government&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Guardian News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.wsj.com/articles/hong-kong-police-make-first-wave-of-arrests-after-protests-11562240165"" target=""_blank""&gt;Hong Kong Police Make First Wave of Protest Arrests&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Wall Street Journal&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.nytimes.com/2019/07/04/world/asia/hong-kong-protests-arrest.html"" target=""_blank""&gt;Hong Kong Police Announce First Arrest in Storming of Legislature&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The New York Times&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.washingtonpost.com/opinions/2019/07/03/hong-kongs-protesters-are-not-radicals-they-just-want-be-heard/"" target=""_blank""&gt;Hong Kong’s protesters are not ‘radicals.' They just want to be heard.&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The Washington Post&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.nytimes.com/2019/07/03/opinion/hong-kong-protest.html"" target=""_blank""&gt;Why China No Longer Needs Hong Kong&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The New York Times&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWpCcWZtRWpvQU1FY1pOZzdldkVMLXRLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.youtube.com"">Guardian News</source></item><item><title>Christie's Goes Ahead with Contested King Tut Statue Auction - Snopes.com</title><link>https://www.snopes.com/ap/2019/07/04/christies-king-tut-auction/</link><guid isPermaLink=""false"">52780325883786</guid><pubDate>Thu, 04 Jul 2019 19:18:01 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.snopes.com/ap/2019/07/04/christies-king-tut-auction/"" target=""_blank""&gt;Christie's Goes Ahead with Contested King Tut Statue Auction&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Snopes.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.cbsnews.com/news/king-tut-sculpture-sells-for-6-million-amid-controversy-2019-07-04/"" target=""_blank""&gt;King Tut sculpture sells for $6 million amid controversy&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CBS News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.cnn.com/style/article/egyptian-head-statue-intl-gbr-scli/index.html"" target=""_blank""&gt;Tutankhamun statue sells for almost $6M despite Egyptian outcry&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.bbc.com/news/world-middle-east-48865336"" target=""_blank""&gt;Tutankhamun: Bust Egypt says was 'stolen' sells for £4.7m&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;BBC News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=FIkbiMoiOPA"" target=""_blank""&gt;Egypt lashes out at Christie's auction house over sale of King Tut sculpture&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CBS This Morning&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWlLbjgyRWpvQU1FVHlKY2NUWkN6a0xLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.snopes.com"">Snopes.com</source><media:content url=""https://lh6.googleusercontent.com/proxy/Kq8uJy8HA8hXs3tqcelvr21HvaArUW0EsloyWk69k968ZcOQbs25-bfCbULtUF50QnKZUjItbPhF06LG0exxaL8bPTvt8LHQyQBXi1sT-wEoR7zLqNHMreJyKQwd=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>AT&amp;T/DIRECTV blacks out local TV stations after rejecting Nexstar offers to extend access - WOODTV.com</title><link>https://www.woodtv.com/about-us/att-directv-blacks-out-local-tv-stations-after-rejecting-nexstar-offers-to-extend-access/</link><guid isPermaLink=""false"">52780326217961</guid><pubDate>Thu, 04 Jul 2019 22:50:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.woodtv.com/about-us/att-directv-blacks-out-local-tv-stations-after-rejecting-nexstar-offers-to-extend-access/"" target=""_blank""&gt;AT&amp;T/DIRECTV blacks out local TV stations after rejecting Nexstar offers to extend access&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;WOODTV.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.usatoday.com/story/money/2019/07/04/at-t-directv-and-nextar-dispute-channels-blacked-out-independence-day/1648807001/"" target=""_blank""&gt;Nexstar stations blacked out on AT&amp;T DirecTV and U-verse amid contract dispute&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;USA TODAY&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.wivb.com/news/bring-back-wivb-news-4-buffalo/"" target=""_blank""&gt;Bring back WIVB News 4 Buffalo and WNLO CW23 on DIRECTV&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;WIVB.com - News 4&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.wric.com/news/bring-back-wric-tv/"" target=""_blank""&gt;Bring back WRIC-TV&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;8News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.wbtw.com/news/att-directv-blacks-out-wbtw-other-nexstar-tv-markets-on-july-4/"" target=""_blank""&gt;AT&amp;T/DirecTV blacks out WBTW, other Nexstar TV markets on July 4&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;WBTW - Myrtle Beach and Florence SC&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWpwMGVHRWpvQU1FYjJnUWlJaTlQRXRLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.woodtv.com"">WOODTV.com</source></item><item><title>Univision Owners Seek Exit After Tumultuous Run - Wall Street Journal</title><link>https://www.wsj.com/articles/univision-owners-seek-exit-after-tumultuous-run-11562269298</link><guid isPermaLink=""false"">52780325987483</guid><pubDate>Thu, 04 Jul 2019 23:11:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.wsj.com/articles/univision-owners-seek-exit-after-tumultuous-run-11562269298"" target=""_blank""&gt;Univision Owners Seek Exit After Tumultuous Run&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Wall Street Journal&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.nytimes.com/2019/07/04/business/media/telemundo-2020-election.html"" target=""_blank""&gt;Telemundo, Presidential Debate Under Its Belt, Moves Into 2020 Spotlight&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The New York Times&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.miamiherald.com/entertainment/tv/article232259552.html"" target=""_blank""&gt;Is Univision exploring a sale? Doral-based company worth billions, 2017 report says&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Miami Herald&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.hollywoodreporter.com/news/univision-explore-sale-1222590"" target=""_blank""&gt;Univision to Explore Sale&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Hollywood Reporter&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.thewrap.com/univision-begins-exploring-possible-sale/"" target=""_blank""&gt;Univision Begins Exploring Possible Sale&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;TheWrap&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWlieWRPRWpvQU1FUnE4NHFMRVZQcjlLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.wsj.com"">Wall Street Journal</source></item><item><title>U.S. asks federal court to throw out Huawei lawsuit - Reuters</title><link>https://www.reuters.com/article/us-usa-china-huawei-tech/u-s-asks-federal-court-to-throw-out-huawei-lawsuit-idUSKCN1TZ224</link><guid isPermaLink=""false"">CAIiEC-ejnA1k0zAeysKSw3LstsqFQgEKg0IACoGCAowt6AMMLAmMJSCDg</guid><pubDate>Thu, 04 Jul 2019 20:43:00 GMT</pubDate><description>&lt;a href=""https://www.reuters.com/article/us-usa-china-huawei-tech/u-s-asks-federal-court-to-throw-out-huawei-lawsuit-idUSKCN1TZ224"" target=""_blank""&gt;U.S. asks federal court to throw out Huawei lawsuit&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Reuters&lt;/font&gt;&lt;p&gt;NEW YORK (Reuters) - The U.S. government filed a motion on Wednesday asking for the dismissal of a lawsuit by Chinese telecommunications giant Huawei ...&lt;/p&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWpQb19HRWpvQU1FVnpGU3QtcC1fcnpLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;</description><source url=""https://www.reuters.com"">Reuters</source></item><item><title>Amazon Is Not FedEx's Biggest Problem - Yahoo Finance</title><link>https://finance.yahoo.com/news/amazon-not-fedex-apos-biggest-021700649.html</link><guid isPermaLink=""false"">52780325818880</guid><pubDate>Thu, 04 Jul 2019 02:27:52 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://finance.yahoo.com/news/amazon-not-fedex-apos-biggest-021700649.html"" target=""_blank""&gt;Amazon Is Not FedEx's Biggest Problem&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Yahoo Finance&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.cnn.com/2019/07/04/business/amazon-25th-birthday-challenges-trnd/index.html"" target=""_blank""&gt;9 challenges Amazon faces on its 25th birthday&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.businessinsider.com/amazon-late-packages-compared-to-fedex-ups-usps-shipping-2019-7"" target=""_blank""&gt;Late Amazon packages more common as it moves from FedEx, UPS, USPS&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Business Insider&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://heavy.com/social/2019/07/fedex-ups-july-4th-2019-delivery-schedule/"" target=""_blank""&gt;FedEx, UPS &amp; Amazon Delivery Schedule on July 4th 2019&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Heavy.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.fool.com/investing/2019/07/03/amazon-is-not-fedexs-biggest-problem.aspx"" target=""_blank""&gt;Amazon Is Not FedEx's Biggest Problem&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The Motley Fool&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWlBcE1tRWpvQU1FVDhZS0lpMUxXTmJLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://finance.yahoo.com"">Yahoo Finance</source><media:content url=""https://lh5.googleusercontent.com/proxy/dUtIo8YgTCmdLdKc6z64QR2ER9yryHVHlOo6wVVmqHwaHoLp-9Q7r1-wssgJRdXXcw9eACuTIGY7VT4F9bMCr2tihXnhqZv1dsZN3x_S5rSuSQBPEg-tOd6qQBffOwy1JfDKH2M7s4qrAsd5tFg7yhxK6w-IBZdI-cvw4VK_XgjMWguInn8yOCnT1Vj95y62HigieOb71k2fJxybBmT4t1rzbbjDzqPyuc8b0jFKX5M6TsFWgUUyHjHOFrpQrCIPkTsXY89H4fY2rSCwG6YLkmfR=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>You can't drive Ford's first million-dollar car on the street - Fox News</title><link>https://www.foxnews.com/auto/you-cant-drive-fords-first-million-dollar-car-on-the-street</link><guid isPermaLink=""false"">52780326296888</guid><pubDate>Thu, 04 Jul 2019 16:43:54 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.foxnews.com/auto/you-cant-drive-fords-first-million-dollar-car-on-the-street"" target=""_blank""&gt;You can't drive Ford's first million-dollar car on the street&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Fox News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=VUPcDhqybeI"" target=""_blank""&gt;Limited Edition Ford GT Mk II | Ford Performance&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Ford Performance&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.motor1.com/news/358011/ford-gt-mk-ii-limited-edition-track-debut/"" target=""_blank""&gt;Ford GT Mk II Is A Rule-Breaking Swan Song&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Motor1.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.autosport.com/wec/news/144586/multimatic-ford-wont-use-radical-gt-for-hypercar"" target=""_blank""&gt;Ford won't use extreme GT version for WEC hypercar - Multimatic&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;autosport.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.cnn.com/2019/07/04/success/ford-gt-track-only-supercar/index.html"" target=""_blank""&gt;Ford will sell a $1.2 million supercar that won't be street-legal&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWk0dXVhRWpvQU1FV0cyS1huYVJCZFlLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.foxnews.com"">Fox News</source><media:content url=""https://lh3.googleusercontent.com/proxy/9vPvQxxpJSQrEiZlnfS3z2yvtaw9Ud4Hn92i1flcM3SOXWYuoCkerBjeq_PqA4hUbPNU72-jsupG8MQCrHmT0vwhwrLSUEnypSRN0vaQEVP44e7fYIUNfhRt5hpRjk3rmmRHAeKCEzLdRfrh44tRzDy1aUaKNH5CWfkrEpIq72cyBZv_57j-dPW_SkzKMrZn79_PS9YaCJUsn3GRYmnQEla--Q=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>Another Note 10 Leak near launch as Google Maps gets smarter - CNET</title><link>https://www.youtube.com/watch?v=gWX3DOT-lWQ</link><guid isPermaLink=""false"">CCAiC2dXWDNET1QtbFdRmAEB</guid><pubDate>Wed, 03 Jul 2019 18:59:33 GMT</pubDate><description>&lt;a href=""https://www.youtube.com/watch?v=gWX3DOT-lWQ"" target=""_blank""&gt;Another Note 10 Leak near launch as Google Maps gets smarter&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNET&lt;/font&gt;&lt;p&gt;&lt;/p&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWlDdjg2RWpvQU1FZWthcjJaU2EwUDZLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;</description><source url=""https://www.youtube.com"">CNET</source></item><item><title>Apple’s iCloud and store systems are down for some users - The Verge</title><link>https://www.theverge.com/2019/7/4/20682491/apple-icloud-down-find-my-iphone-service-disruption-outage</link><guid isPermaLink=""false"">52780326529919</guid><pubDate>Thu, 04 Jul 2019 19:10:07 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.theverge.com/2019/7/4/20682491/apple-icloud-down-find-my-iphone-service-disruption-outage"" target=""_blank""&gt;Apple’s iCloud and store systems are down for some users&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The Verge&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://9to5mac.com/2019/07/04/icloud-services-down/"" target=""_blank""&gt;Almost all iCloud services down for some users including Photos, Mail, Backup, Find My Friends, Contacts, Calendars, more [U]&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;9to5Mac&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://appleinsider.com/articles/19/07/04/apple-seeing-widespread-icloud-and-retail-store-failures"" target=""_blank""&gt;Apple working to resolve widespread iCloud and retail store failures [u]&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;AppleInsider&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://mashable.com/article/apple-icloud-outages-reported/"" target=""_blank""&gt;Apple works to resolve iCloud issues after outages reported&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Mashable&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.macrumors.com/2019/07/04/icloud-retail-store-system-issues/"" target=""_blank""&gt;Apple Experiencing Issues With iCloud Services and Retail Store Systems [Resolved]&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Mac Rumors&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWpfMXZTRWpvQU1FWjMwdC1VQnpmWm1LQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.theverge.com"">The Verge</source></item><item><title>A Very Personal MacBook Pro Disaster - The Mac Observer</title><link>https://www.macobserver.com/columns-opinions/editorial/personal-macbook-pro-disaster/</link><guid isPermaLink=""false"">52780325089343</guid><pubDate>Wed, 03 Jul 2019 21:33:40 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.macobserver.com/columns-opinions/editorial/personal-macbook-pro-disaster/"" target=""_blank""&gt;A Very Personal MacBook Pro Disaster&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The Mac Observer&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.techradar.com/news/apple-may-finally-resolve-macbook-butterfly-keyboard-issues-by-abandoning-the-design-altogether"" target=""_blank""&gt;Apple may finally resolve MacBook butterfly keyboard issues by abandoning the design altogether&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;TechRadar&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.theverge.com/2019/7/4/20682079/apple-butterfly-switch-scissor-switch-2019-macbook-air-2020-macbook-pro"" target=""_blank""&gt;Apple is reportedly giving up on its controversial MacBook keyboard&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The Verge&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://9to5mac.com/2019/07/04/kuo-new-keyboard-macbook-air-pro/"" target=""_blank""&gt;Kuo: Apple to include new scissor switch keyboard in 2019 MacBook Air and 2020 MacBook Pro&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;9to5Mac&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.macworld.co.uk/news/mac/new-macbook-pro-launch-3699060/"" target=""_blank""&gt;New MacBook Pro 'about to launch' - FCC filing&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Macworld UK&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWlfNEp5RWpvQU1FZGVvaEliYnlzWUpLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.macobserver.com"">The Mac Observer</source><media:content url=""https://lh3.googleusercontent.com/proxy/EOmQbktCJ-UT8jWaH8VPogBzNg27ZOnEsCiR1xTV-f_J7lBQmVWWtgJ1BnDYiyd5N562V6Oy6vl9Y2AeEVwVhj8U_FOaaEFLDNjFaGKUAh8d4rFwis9EZPdYCqm8=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>Did 'Spider-Man: Far From Home' Post-Credits Scene Tease Marvel's Next Saga? - Hollywood Reporter</title><link>https://www.hollywoodreporter.com/heat-vision/did-spider-man-far-home-post-credits-tease-marvels-next-saga-1222084</link><guid isPermaLink=""false"">52780325726512</guid><pubDate>Thu, 04 Jul 2019 22:01:06 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.hollywoodreporter.com/heat-vision/did-spider-man-far-home-post-credits-tease-marvels-next-saga-1222084"" target=""_blank""&gt;Did 'Spider-Man: Far From Home' Post-Credits Scene Tease Marvel's Next Saga?&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Hollywood Reporter&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.dailywire.com/news/49178/tom-holland-spider-man-could-be-gay-paul-bois"" target=""_blank""&gt;Tom Holland: 'Spider-Man' Could Be Gay&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The Daily Wire&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.ign.com/articles/2019/07/04/tom-holland-marvel-contract-spider-man-upcoming-movies-phase-4-mcu"" target=""_blank""&gt;Tom Holland's Marvel Contract: How Many Spider-Man Movies Does He Have Left?&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;IGN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.cnet.com/news/spider-man-far-from-home-our-biggest-spoiler-filled-wtf-questions/"" target=""_blank""&gt;Spider-Man: Far From Home -- our biggest spoiler-filled WTF questions&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNET&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.cnbc.com/2019/07/03/spider-man-far-from-home-post-credit-scenes-impact-on-marvel-films.html"" target=""_blank""&gt;How those bombshells in 'Spider-Man: Far From Home' post-credits will impact the Marvel Cinematic Universe going forward&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNBC&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWl3MHNPRWpvQU1FY3NKU050WE9PaGdLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.hollywoodreporter.com"">Hollywood Reporter</source><media:content url=""https://lh3.googleusercontent.com/proxy/bnmWW--Igrpzv3iNIla--eCDsplNRNEFN5g834JM3lQzT8Qd5KlJXc1_bD3NPkG_fz4VO0-UPMggn6Ru3BKr1RyYcFysQceYXughJ1kLo1GagmoqUiWeJJZ2MDTwZIPgba2ISrsEWSb6HWOHi3VdAAe6oCtbVhafjRU=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>RIP Mad Magazine? Satirical Publication to Cease Original Content, Focus on Reprints - AOL</title><link>https://www.aol.com/article/entertainment/2019/07/04/mad-magazine-cease-original-content/23763244/</link><guid isPermaLink=""false"">52780326136421</guid><pubDate>Thu, 04 Jul 2019 19:25:50 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.aol.com/article/entertainment/2019/07/04/mad-magazine-cease-original-content/23763244/"" target=""_blank""&gt;RIP Mad Magazine? Satirical Publication to Cease Original Content, Focus on Reprints&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;AOL&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://io9.gizmodo.com/mad-magazine-is-basically-dead-1836104170"" target=""_blank""&gt;MAD Magazine Is Basically Dead&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Gizmodo&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://comicbook.com/dc/2019/07/04/mad-magazine-to-cease-publication/"" target=""_blank""&gt;MAD Magazine to Cease Publication&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Comicbook.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.cnn.com/2019/07/04/media/mad-magazine-cease-publication-trnd/index.html"" target=""_blank""&gt;Mad Magazine will vanish from newsstands after 67 years&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.huffpost.com/entry/mad-magazine-winding-down_n_5d1da141e4b04c48140eed2e"" target=""_blank""&gt;MAD Magazine Is Winding Down And Fans Are Devastated&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;HuffPost&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWpsMU55RWpvQU1FVVFJd0F2YXh1T2FLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.aol.com"">AOL</source><media:content url=""https://lh6.googleusercontent.com/proxy/qcKqgnNZWymF3_81kbxL0z3QmZ84gF8ZIAWs99AuWt1jGu5S48NwlOW8eLzCXa26rdNtM-OKqlqAHULPZVMQJKy6RVk7g8H6Xwwz-tVvMDsYubqsELrHG2QpgPajn1QrpikrHb7_qk-vUmmb7KPv2DsY6H35L5eQacu9b8nLBHPYQ0qXSfTOcb5ZyJvZ7NskpiI2VTJuzMqvEQS3vN8f1ZYgPg5aWZFm_8jdiBqhegRocsrutqP6zlIjTxT_wQrp4rqxVgLDOwXnfCbIMhrQm25-g_BpFxo=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>Joy-Anna Duggar Forsyth says she had a miscarriage - CNN</title><link>https://www.cnn.com/2019/07/04/entertainment/joy-anna-duggar-forsyth-miscarriage-trnd/index.html</link><guid isPermaLink=""false"">52780326163970</guid><pubDate>Thu, 04 Jul 2019 18:27:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.cnn.com/2019/07/04/entertainment/joy-anna-duggar-forsyth-miscarriage-trnd/index.html"" target=""_blank""&gt;Joy-Anna Duggar Forsyth says she had a miscarriage&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CNN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://people.com/tv/joy-anna-duggar-suffered-miscarriage/"" target=""_blank""&gt;Joy-Anna Duggar Reveals She's Suffered a Miscarriage at 5 Months: 'We've Cried Countless Tears'&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;PEOPLE.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.foxnews.com/entertainment/joy-anna-duggar-miscarriage-pregnancy-counting-on-tlc-austin-forsyth"" target=""_blank""&gt;Joy-Anna Duggar reveals she suffered miscarriage 5 months into second pregnancy: 'We've cried countless tears'&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Fox News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.eonline.com/news/1054356/joy-anna-duggar-suffers-miscarriage-in-second-trimester-of-pregnancy"" target=""_blank""&gt;Joy-Anna Duggar Suffers Miscarriage in Second Trimester&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;E! NEWS&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://okmagazine.com/photos/joy-anna-duggar-suffers-miscarriage-5-months-pregnancy/"" target=""_blank""&gt;Joy-Anna Duggar Suffers Miscarriage 5 Months Into Her Pregnancy&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;OK Magazine&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWlDck42RWpvQU1FYWRTWHhsTjV4dmpLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.cnn.com"">CNN</source><media:content url=""https://lh5.googleusercontent.com/proxy/zVovuGPf61SnUROCDkW2Oh2GFG3WkZ4Tg59LxHlK7f7VMg3UePTw--1UXnBEJ2MrFMUtZ9iVz8rGIuWnWxN_xFoUrADWPWo-piMYo-cxftT2DS8c-zM_ixQ66szyaCYIpNgWuYyoDqpe_oudOHc=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>Get to know Halle Bailey, the new Little Mermaid, through her YouTube covers - The Verge</title><link>https://www.theverge.com/2019/7/4/20682389/halle-bailey-little-mermaid-ariel-disney-chloe-beyonce-rihanna</link><guid isPermaLink=""false"">52780326294116</guid><pubDate>Thu, 04 Jul 2019 18:30:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.theverge.com/2019/7/4/20682389/halle-bailey-little-mermaid-ariel-disney-chloe-beyonce-rihanna"" target=""_blank""&gt;Get to know Halle Bailey, the new Little Mermaid, through her YouTube covers&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The Verge&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=dF85RR6FgEQ"" target=""_blank""&gt;Zendaya, Lizzo React To Halle Bailey As 'The Little Mermaid'&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;ET Canada&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""http://whatculture.com/film/the-little-mermaid-why-disneys-progressiveness-is-good-for-everyone"" target=""_blank""&gt;The Little Mermaid: Why Disney's Progressiveness Is Good For EVERYONE&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;WhatCulture&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.usatoday.com/story/entertainment/movies/2019/07/04/disneys-little-mermaid-remake-halle-berry-lauds-star-halle-bailey/1651573001/"" target=""_blank""&gt;Halle Berry or Halle Bailey? Live-action 'Little Mermaid' casting has fans confused&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;USA TODAY&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.cheatsheet.com/entertainment/celebrity-reactions-halle-bailey-the-little-mermaid.html/"" target=""_blank""&gt;These Celebrities Had the Best Reactions to Halle Bailey's 'The Little Mermaid' Casting&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Showbiz Cheat Sheet&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqdQgKIm9DQklTVFRvSmMzUnZjbmt0TXpZd1NrQUtFUWprcE9hRWpvQU1FUjFrUWdkN2pxZEtFaXRJWVd4c1pTQkNZV2xzWlhrZ2FYTWdSR2x6Ym1WNUozTWdibVYzSUV4cGRIUnNaU0JOWlhKdFlXbGtLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.theverge.com"">The Verge</source></item><item><title>Lakers need to sign Kawhi before July 6th – Bobby Marks | Golic and Wingo - ESPN</title><link>https://www.youtube.com/watch?v=2cdEG_Zg5uM</link><guid isPermaLink=""false"">52780326379174</guid><pubDate>Fri, 05 Jul 2019 00:26:33 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=2cdEG_Zg5uM"" target=""_blank""&gt;Lakers need to sign Kawhi before July 6th – Bobby Marks | Golic and Wingo&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;ESPN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.houstonchronicle.com/sports/columnists/smith/article/Kawhi-Leonard-is-quietly-running-the-new-NBA-14070750.php"" target=""_blank""&gt;Kawhi Leonard is quietly running the new NBA&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Houston Chronicle &lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=oVx8Z5Ee3wU"" target=""_blank""&gt;Raptors hot takes: Questions that divide fans&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CityNews Toronto&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://raptorsrapture.com/2019/07/04/kawhi-leonard-lakers-toronto-raptors/"" target=""_blank""&gt;Did Kawhi Leonard ruin the Lakers backup plan, if he chooses the Toronto Raptors?&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Raptors Rapture&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://sports.yahoo.com/report-kawhi-leonard-suitors-warned-to-not-leak-info-about-pitches-231739759.html"" target=""_blank""&gt;Anxiety crescendos as Kawhi Leonard suitors reportedly warned to not leak info&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Yahoo Sports&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWltdmV1RWpvQU1FYVBxUjgxWkQ2TF9LQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.youtube.com"">ESPN</source></item><item><title>Wimbledon crowd boo Rafael Nadal during fierce Nick Kyrgios showdown - Express</title><link>https://www.express.co.uk/sport/tennis/1149244/Wimbledon-crowd-boo-Rafael-Nadal-Nick-Kyrgios-tennis-news</link><guid isPermaLink=""false"">52780326084852</guid><pubDate>Fri, 05 Jul 2019 00:01:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.express.co.uk/sport/tennis/1149244/Wimbledon-crowd-boo-Rafael-Nadal-Nick-Kyrgios-tennis-news"" target=""_blank""&gt;Wimbledon crowd boo Rafael Nadal during fierce Nick Kyrgios showdown&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Express&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.espn.com/tennis/story/_/id/27121407/kyrgios-apologize-aiming-ball-nadal"" target=""_blank""&gt;Kyrgios won't apologize for aiming ball at Nadal&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;ESPN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.tennisworldusa.org/tennis/news/Rafael_Nadal/73234/rafael-nadal-my-opinion-on-nick-kyrgios-has-not-changed-/"" target=""_blank""&gt;Rafael Nadal: 'My opinion on Nick Kyrgios has not changed'&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Tennis World USA&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.express.co.uk/sport/tennis/1148933/Rafael-Nadal-Nick-Kyrgios-start-time-Wimbledon-order-of-play-today"" target=""_blank""&gt;What time is Rafael Nadal vs Nick Kyrgios at Wimbledon today? Latest predicted start time&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Express&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=qhhAmKNhMXM"" target=""_blank""&gt;Rafael Nadal outlasts Nick Kyrgios in four sets to advance | 2019 Wimbledon Highlights&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;ESPN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWowd2RtRWpvQU1FVkVkbjhtV2pZX3NLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.express.co.uk"">Express</source></item><item><title>Diego Sanchez Calls Himself A 'Jedi' Ahead Of UFC 239: 'I Am A True Anomaly Of This Sport' - MMAFightingonSBN</title><link>https://www.youtube.com/watch?v=-Jcnm23NytI</link><guid isPermaLink=""false"">52780323196868</guid><pubDate>Thu, 04 Jul 2019 23:05:56 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=-Jcnm23NytI"" target=""_blank""&gt;Diego Sanchez Calls Himself A 'Jedi' Ahead Of UFC 239: 'I Am A True Anomaly Of This Sport'&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;MMAFightingonSBN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.bjpenn.com/mma-news/ufc/opinion-jon-jones-making-up-for-lost-time-by-focusing-on-legacy-of-dominance/"" target=""_blank""&gt;Opinion | Jon Jones making up for lost time by focusing on legacy of dominance&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;BJPenn.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=0zcR-Z5V4uU"" target=""_blank""&gt;UFC 239: Media Day Faceoffs&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;UFC - Ultimate Fighting Championship&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.sherdog.com/news/news/Jan-Blachowicz-Expects-To-Face-Best-Luke-Rockhold-Ever-Despite-Long-Layoff-157765"" target=""_blank""&gt;Jan Blachowicz Expects To Face 'Best Luke Rockhold Ever' Despite Long Layoff&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Sherdog.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=Ds1n17296gY"" target=""_blank""&gt;UFC 239 Media Day Staredowns Live Stream&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;MMAFightingonSBN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWpFbjZtRGpvQU1FWXZGZmtvc1hfM05LQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.youtube.com"">MMAFightingonSBN</source></item><item><title>WNBA star Sue Bird explains why she spoke out against President Donald Trump in viral article - FOX Sports Asia</title><link>https://www.foxsportsasia.com/basketball/1133458/wnba-star-sue-bird-explains-why-she-spoke-out-against-president-donald-trump-in-viral-article/</link><guid isPermaLink=""false"">52780325548531</guid><pubDate>Thu, 04 Jul 2019 17:55:58 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.foxsportsasia.com/basketball/1133458/wnba-star-sue-bird-explains-why-she-spoke-out-against-president-donald-trump-in-viral-article/"" target=""_blank""&gt;WNBA star Sue Bird explains why she spoke out against President Donald Trump in viral article&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;FOX Sports Asia&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.foxnews.com/sports/megan-rapinoe-im-uniquely-and-very-deeply-american"" target=""_blank""&gt;Megan Rapinoe: 'I'm uniquely and very deeply American'&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Fox News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.espn.com/soccer/united-states/story/3893119/morgans-tea-cup-celebration-next-level-rapinoe"" target=""_blank""&gt;Morgan's tea cup celebration 'next level' - Rapinoe&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;ESPN&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.usatoday.com/story/sports/columnist/nancy-armour/2019/07/03/world-cup-2019-megan-rapinoe-more-american-those-criticize-her/1641234001/"" target=""_blank""&gt;Opinion: USWNT's Megan Rapinoe is living her patriotism at World Cup. What's your excuse?&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;USA TODAY&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.latimes.com/opinion/op-ed/la-oe-hirshey-soccer-world-cup-rapinoe-20190703-story.html"" target=""_blank""&gt;I helped spark the Rapinoe-Trump war. Trust me, put your money on the soccer star&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Los Angeles Times&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWp6NDdpRWpvQU1FV2k2bXZ0V1R0TmFLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.foxsportsasia.com"">FOX Sports Asia</source><media:content url=""https://lh4.googleusercontent.com/proxy/rlJTtRCrupaBzPf46A54zbHF1exHzIxsMg7gmxDnz9pACnLeioKbxbRnpgnK92aMhpuYFvL7bXvGNM3zsiOI4uAMp5NkBS5BBRDJ-F_d-bZD0-cBxRjg0zEBcofTcv45Nd-ACA9KsYF1FKIlTHlvvpzHmn8D9Cz4gp_mIRg=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>Omega Unveils Speedmaster First Omega in Space Met Edition Watch - Forbes</title><link>https://www.forbes.com/sites/robertanaas/2019/07/03/omega-unveils-speedmaster-first-omega-in-space-met-edition-watch/</link><guid isPermaLink=""false"">CAIiENbZ0pqxtBdIadLYgOR9YQEqFQgEKg0IACoGCAowrqkBMKBFMLGBAg</guid><pubDate>Wed, 03 Jul 2019 17:03:17 GMT</pubDate><description>&lt;a href=""https://www.forbes.com/sites/robertanaas/2019/07/03/omega-unveils-speedmaster-first-omega-in-space-met-edition-watch/"" target=""_blank""&gt;Omega Unveils Speedmaster First Omega in Space Met Edition Watch&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Forbes&lt;/font&gt;&lt;p&gt;Omega, which proudly lays claim to being the first watch to be worn on the moon, unveils its newest collaboration watch—in partnership with The Metropolitan ...&lt;/p&gt;</description><source url=""https://www.forbes.com"">Forbes</source></item><item><title>NASA launches Orion crew capsule from Cape Canaveral - NBC2 News</title><link>https://www.nbc-2.com/story/40739190/nasa-launches-orion-crew-capsule-from-cape-canaveral</link><guid isPermaLink=""false"">52780324298005</guid><pubDate>Wed, 03 Jul 2019 10:18:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.nbc-2.com/story/40739190/nasa-launches-orion-crew-capsule-from-cape-canaveral"" target=""_blank""&gt;NASA launches Orion crew capsule from Cape Canaveral&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;NBC2 News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://dailytimes.com.pk/423077/nasa-tests-abort-system-on-astronaut-capsule-built-for-moon-missions/"" target=""_blank""&gt;NASA tests abort system on astronaut capsule built for moon missions&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Daily Times&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://news.sky.com/story/nasa-succesfully-tests-new-rocket-launch-abort-system-11755759"" target=""_blank""&gt;NASA succesfully tests new rocket launch abort system&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Sky News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.firstpost.com/tech/science/nasa-test-launch-abort-system-of-orion-capsule-that-will-be-used-in-artemis-mission-6922261.html"" target=""_blank""&gt;NASA tests launch-abort system of Orion capsule that will be used in Artemis mission&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Firstpost&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.news24.com/World/News/watch-nasa-tests-launch-abort-system-for-moon-mission-capsule-20190703"" target=""_blank""&gt;WATCH: NASA tests launch-abort system for moon-mission capsule&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;News24&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqfQgKIndDQklTVXpvSmMzUnZjbmt0TXpZd1NrWUtFUWlWdXV5RGpvQU1FVzBINlhiTWNwY01FakZPUVZOQklITjFZMk5sYzNObWRXeHNlU0IwWlhOMGN5QlBjbWx2YmlCc1lYVnVZMmdnWVdKdmNuUWdjM2x6ZEdWdEtBQVAB?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.nbc-2.com"">NBC2 News</source><media:content url=""https://lh3.googleusercontent.com/proxy/XVZSQ-hbjHGrSY9qObiNSL0r43WuwWBkWDdm2mwG9y95z6-STLkBnPwrE_f1x0sKdy2WQrRQrNVMGVxKoetkEmDOqFyoHL8pwYsTApYuhlt1e5Ia9f3c15StCl7G_nTqqT-6uwjTu9wutPnWeHDfiPx3VtwoWn0Lf6bRm_O9sKai=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>Portable polarization-sensitive camera could be used in machine vision, autonomous vehicles, security and more - Phys.org</title><link>https://phys.org/news/2019-07-portable-polarization-sensitive-camera-machine-vision.html</link><guid isPermaLink=""false"">52780326540680</guid><pubDate>Thu, 04 Jul 2019 18:00:01 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://phys.org/news/2019-07-portable-polarization-sensitive-camera-machine-vision.html"" target=""_blank""&gt;Portable polarization-sensitive camera could be used in machine vision, autonomous vehicles, security and more&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Phys.org&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=tGcimk8yd-Y"" target=""_blank""&gt;Polarization Camera&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Harvard John A. Paulson School of Engineering and Applied Sciences&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWlJcV9XRWpvQU1FWm1VY3hnN0hldWFLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://phys.org"">Phys.org</source></item><item><title>'An exciting time for spaceflight in America': NASA space station astronauts celebrate Independence Day - Fox News</title><link>https://www.foxnews.com/science/nasa-space-station-astronauts-independence-day</link><guid isPermaLink=""false"">52780325821805</guid><pubDate>Thu, 04 Jul 2019 15:06:51 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.foxnews.com/science/nasa-space-station-astronauts-independence-day"" target=""_blank""&gt;'An exciting time for spaceflight in America': NASA space station astronauts celebrate Independence Day&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Fox News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=jX_1equIOpU"" target=""_blank""&gt;Happy 4th of July from the Space Station Crew&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;NASA&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.upi.com/Science_News/2019/07/04/Apollo-space-program-spawned-technologies-products-still-in-use/8451561000261/"" target=""_blank""&gt;Apollo space program spawned technologies, products still in use&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;UPI News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.space.com/space-station-astronauts-fourth-of-july.html"" target=""_blank""&gt;Happy Fourth of July from Space! Astronauts Send Video Message Home&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Space.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.express.co.uk/news/science/1149195/NASA-news-space-picture-Earth-International-Space-Station-ISS-NASA-Instagram"" target=""_blank""&gt;NASA news: The space agency just shared the most breathtaking picture of Earth from space&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Express.co.uk&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWp0dXNtRWpvQU1FU3Nqc2J0Y1hEeU5LQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.foxnews.com"">Fox News</source><media:content url=""https://lh6.googleusercontent.com/proxy/wBpr7eegLviUdPmhnqlMz156KV01HzvQ3a8cYbeKTndMSaFYHgt2eD-K80STU0GD8kka6NLAU3PZd0_NO3PIoj4FK6rg2gIaJ8qaswWpjSf-uyI1dkBMqxLqP5tOc1LCnuZatK-UcxuKsVSzmGvLpMi8KxDklCTNjHaNNggMteSOG-av9CdS6bQRFl-sgBJxUJE7pYAT5D7238hywu2aucR1wB79j7VcYE-17NmpHMkAYqu7RPbONSeY=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>HIV Eliminated From Living Animals For First Time - joemygod.com</title><link>https://www.joemygod.com/2019/07/hiv-eliminated-from-living-animals-for-first-time/</link><guid isPermaLink=""false"">52780326468168</guid><pubDate>Thu, 04 Jul 2019 16:41:15 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.joemygod.com/2019/07/hiv-eliminated-from-living-animals-for-first-time/"" target=""_blank""&gt;HIV Eliminated From Living Animals For First Time&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;joemygod.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.washingtonpost.com/health/2019/07/04/scientists-say-they-found-cure-hiv-mice-humans-could-be-next/"" target=""_blank""&gt;Scientists say they found a cure for HIV in some mice. Humans could be next.&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Washington Post&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://people.com/health/hiv-antiretroviral-drugs-cure-with-gene-editing/"" target=""_blank""&gt;Researchers Successfully Remove HIV from 'Humanized Mice' in 'First Step' Toward Showing It's Curable&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;PEOPLE.com&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.labnews.co.uk/news/hiv-virus-eliminated-new-antiretroviral-therapy-04-07-2019/"" target=""_blank""&gt;HIV virus eliminated with CRISPR and new antiretroviral therapy&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Lab News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.inforum.com/business/healthcare/3127732-Scientists-say-they-found-a-cure-for-HIV-in-some-mice-humans-could-be-next"" target=""_blank""&gt;Scientists say they found a cure for HIV in some mice; humans could be next&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;INFORUM&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWpJOVBDRWpvQU1FVF9RUmQtV0I1TlpLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.joemygod.com"">joemygod.com</source><media:content url=""https://lh4.googleusercontent.com/proxy/VbfUfAgqaOKNxY9suOxIS5m3GlI5Qx_ppmFB6ETaXM9n-mdO1F8MeM32RYLttAZ-siywOtEx71YnJJ7by4KU8wHcFD_LPAv3t6ZwOoBNOrNHHNc=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>Man Who Didn't Go in the Water Contracted Flesh-Eating Bacteria - Newser</title><link>https://www.newser.com/story/277372/man-who-didnt-go-in-the-water-contracted-flesh-eating-bacteria.html</link><guid isPermaLink=""false"">CBMiZ2h0dHBzOi8vd3d3Lm5ld3Nlci5jb20vc3RvcnkvMjc3MzcyL21hbi13aG8tZGlkbnQtZ28taW4tdGhlLXdhdGVyLWNvbnRyYWN0ZWQtZmxlc2gtZWF0aW5nLWJhY3RlcmlhLmh0bWzSAQA</guid><pubDate>Thu, 04 Jul 2019 21:26:00 GMT</pubDate><description>&lt;a href=""https://www.newser.com/story/277372/man-who-didnt-go-in-the-water-contracted-flesh-eating-bacteria.html"" target=""_blank""&gt;Man Who Didn't Go in the Water Contracted Flesh-Eating Bacteria&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Newser&lt;/font&gt;&lt;p&gt;Tyler King did not directly touch water the day he was infected with flesh-eating bacteria. He doesn't know how it happened. But when his left arm began ...&lt;/p&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqeAgKInJDQklTVHpvSmMzUnZjbmt0TXpZd1NrSUtFUWo5bUtpRWpvQU1FWWFIQUZ3YkNFNzFFaTFHYkc5eWFXUmhJSGR2YldGdUlHUnBaWE1nWm5KdmJTQm1iR1Z6YUMxbFlYUnBibWNnWW1GamRHVnlhV0VvQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;</description><source url=""https://www.newser.com"">Newser</source><media:content url=""https://lh6.googleusercontent.com/proxy/-tgb_Mh0kIu05U_xaWFbuoY0s65qO2xUHNzFOVX0yVSGTyQ7vkNhZ4YHJ02Y-4JeE47GrXAClZioWBOBYDiUwkEt-2ZXRBTSikOmugtMLHV3SCI_5Q=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>Dog treats could make humans sick, CDC says - ABC15 Arizona</title><link>https://www.abc15.com/news/national/dog-treats-could-make-humans-sick-cdc-says</link><guid isPermaLink=""false"">52780325978789</guid><pubDate>Thu, 04 Jul 2019 02:54:00 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.abc15.com/news/national/dog-treats-could-make-humans-sick-cdc-says"" target=""_blank""&gt;Dog treats could make humans sick, CDC says&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;ABC15 Arizona&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.kwqc.com/content/news/Health-officials-warn-of-salmonella-outbreak-linked-to-popular-dog-treats-512220812.html"" target=""_blank""&gt;Health officials warn of salmonella outbreak linked to popular dog treats&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;KWQC-TV6&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.foodsafetynews.com/2019/07/antibiotic-resistance-complicates-pig-ear-outbreak-45-people-sick/"" target=""_blank""&gt;Antibiotic resistance complicates treatment of people infected in pig ear outbreak&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;Food Safety News&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.wkyt.com/content/news/Contact-with-some-pet-treats-can-give-people-salmonella-512223151.html"" target=""_blank""&gt;Contact with some pet treats can give people salmonella&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;WKYT&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.youtube.com/watch?v=w9Gp1pbp_aE"" target=""_blank""&gt;Pig Ear Dog Treats Linked To Salmonella&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;CBS New York&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWlsaGRPRWpvQU1FYndWcTh2ekNubXhLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.abc15.com"">ABC15 Arizona</source><media:content url=""https://lh4.googleusercontent.com/proxy/uOMqlude__HIPESeT6FnIcNYicBlc176hCV8vWma1jjK1s34DrmOlORCEnLPfwLH1_nJs02RaSa-optDPj6LpH_38hIzgyikCj6f7qM9SQ7rMlDbLR6YgHM4repf5XbD7P6TA9kp75JQrzIBAcZ2yOiNq32OygzaSmrvrYS_eNsvnPIKhPQkhwVRy_85f8dAIaxn0tAKkCaTLF_ni5vgzuUk8azj_veeFtGCyfVZLbYuqXRrNVl0iKPe0_jQf4zriBteI7fglQQzqdpBJvAtpaOjLbiIpC0OMSIlIDBo0uNvp1MgzmDaTV-QE1MacD4w-xQBQT-m1NuV9PiQoNLyShMzBsJy=-w150-h150-c"" medium=""image"" width=""150"" height=""150""/></item><item><title>Alzheimer’s researchers shift focus after failures - The Philadelphia Inquirer</title><link>https://www.inquirer.com/health/alzheimers-research-shift-focus-after-failures-20190704.html</link><guid isPermaLink=""false"">52780326307379</guid><pubDate>Thu, 04 Jul 2019 10:22:37 GMT</pubDate><description>&lt;ol&gt;&lt;li&gt;&lt;a href=""https://www.inquirer.com/health/alzheimers-research-shift-focus-after-failures-20190704.html"" target=""_blank""&gt;Alzheimer’s researchers shift focus after failures&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;The Philadelphia Inquirer&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;a href=""https://www.npr.org/sections/health-shots/2019/07/04/738478841/new-markers-for-alzheimers-disease-could-aid-diagnosis-and-speed-up-drug-develop"" target=""_blank""&gt;Alzheimer's Biomarkers Move Ahead In Research : Shots - Health News&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color=""#6f6f6f""&gt;NPR&lt;/font&gt;&lt;/li&gt;&lt;li&gt;&lt;strong&gt;&lt;a href=""https://news.google.com/stories/CAAqOQgKIjNDQklTSURvSmMzUnZjbmt0TXpZd1NoTUtFUWl6ak9lRWpvQU1FUmg1dEVJOE5yVHVLQUFQAQ?oc=5"" target=""_blank""&gt;View full coverage on Google News&lt;/a&gt;&lt;/strong&gt;&lt;/li&gt;&lt;/ol&gt;</description><source url=""https://www.inquirer.com"">The Philadelphia Inquirer</source></item></channel></rss>";
            //return @"<?xml version=""1.0"" encoding=""UTF-8""?><feed xmlns=""http://www.w3.org/2005/Atom""><category term="" reddit.com"" label=""r/ reddit.com""/><updated>2019-10-18T14:45:18+00:00</updated><id>/.rss</id><link rel=""self"" href=""https://www.reddit.com/.rss"" type=""application/atom+xml"" /><link rel=""alternate"" href=""https://www.reddit.com/"" type=""text/html"" /><title>reddit: the front page of the internet</title><entry><author><name>/u/Siesonn</name><uri>https://www.reddit.com/user/Siesonn</uri></author><category term=""AskReddit"" label=""r/AskReddit""/><content type=""html"">&amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/Siesonn&quot;&gt; /u/Siesonn &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/AskReddit/&quot;&gt; r/AskReddit &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/AskReddit/comments/djfw1n/what_is_something_people_often_say_but_rarely_mean/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/AskReddit/comments/djfw1n/what_is_something_people_often_say_but_rarely_mean/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_djfw1n</id><link href=""https://www.reddit.com/r/AskReddit/comments/djfw1n/what_is_something_people_often_say_but_rarely_mean/"" /><updated>2019-10-18T01:08:05+00:00</updated><title>What is something people often say but rarely mean?</title></entry><entry><author><name>/u/ThouHastLostAn8th</name><uri>https://www.reddit.com/user/ThouHastLostAn8th</uri></author><category term=""news"" label=""r/news""/><content type=""html"">&amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/ThouHastLostAn8th&quot;&gt; /u/ThouHastLostAn8th &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/news/&quot;&gt; r/news &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.kyma.com/news/national-world/mulvaney-admits-quid-pro-quo/1133169814&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/news/comments/djdy4d/mulvaney_admits_quid_pro_quo/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_djdy4d</id><link href=""https://www.reddit.com/r/news/comments/djdy4d/mulvaney_admits_quid_pro_quo/"" /><updated>2019-10-17T22:34:56+00:00</updated><title>Mulvaney admits quid pro quo</title></entry><entry><author><name>/u/ThrowRAhelp6425</name><uri>https://www.reddit.com/user/ThrowRAhelp6425</uri></author><category term=""relationship_advice"" label=""r/relationship_advice""/><content type=""html"">&lt;!-- SC_OFF --&gt;&lt;div class=&quot;md&quot;&gt;&lt;p&gt;Edit: Guys, I appreciate the advice. But I&amp;#39;m not asking on how to get past the infidelity. I&amp;#39;ve made my decision, and it&amp;#39;s not something I&amp;#39;m willing to forgive. I&amp;#39;m looking for advice on how to handle this with my kids, and how to reassure them we can still be a family even if we aren&amp;#39;t married.&lt;/p&gt; &lt;p&gt;My wife and I have been together for 18 years. We had our ups and downs but overall our marriage has been solid and she was my best friend. We&amp;#39;ve always been very close with our children and have raised them very much as a unit, and in turn they&amp;#39;re very close to us.&lt;/p&gt; &lt;p&gt;About two weeks ago I brought up an advice column in the paper I had read regarding infidelity and talked about it for a bit, and I could see she had clearly started to tear up and was attempting to hide it, so I asked her what was bothering her. She confessed that about 7 years ago she cheated on me with a former coworker on a business trip, and then once with him after that before breaking it off due to guilt. This was around the time she left that job for a new one which she had told me back then was due to feeling like it wasn&amp;#39;t a good fit but now confesses was because she didn&amp;#39;t want to work with him anymore. She says she never had a good reason for it, she knew it was wrong then but gave into the urge and nothing has ever happened since. Claims she confessed now as she can&amp;#39;t live with the guilt of hiding it from me. We talked about for the next three days when the kids were in school while trying our best not to let them know while they were home. I realized I knew I&amp;#39;d never be able to let this go, nor would I be able to forgive her. She&amp;#39;s distraught.&lt;/p&gt; &lt;p&gt;We sat our kids down to tell them about the fact our marriage was ending. Obviously this was extremely tough. They acted as one could expect and blame was thrown at both of us. My wife and I agreed to tell them it was mutual, but I guess guilt got the better of her again because she ends up crying and telling them not to blame me, that she &amp;quot;betrayed my trust&amp;quot; in the past and just told me now, and that it&amp;#39;s all her fault. This had the opposite effect that she intended, and now both our kids absolutely resent me for not &amp;quot;forgiving mom&amp;quot; and ruining our family. My wife has repeatedly tried to put blame back on herself, which only makes the kids double down and defend her more as if she&amp;#39;s the victim. I don&amp;#39;t want to drag their mother through the mud, so I&amp;#39;m being trying to explain as tactfully as I can, but they&amp;#39;re gone as far as saying that when I get my own apartment they don&amp;#39;t want to see me anymore and refuse my suggestion of seeing a therapist together and reject the idea that I still plan to see them on a regular basis and be on good terms with their mother. My wife isn&amp;#39;t helping matters as she&amp;#39;s been an emotional wreck since I&amp;#39;ve told her of my plan to leave.&lt;/p&gt; &lt;p&gt;I love my kids, but I don&amp;#39;t know how to make them understand this without dragging their mother through the mud. I don&amp;#39;t know how to assure them this has nothing to do with them or that I&amp;#39;m not trying to punish or hurt their mom. I just know that I cannot get past this, and for my own happiness I need to leave. Any suggestions on how to get them to stop seeing me as the villain would be helpful.&lt;/p&gt; &lt;p&gt;Tl;dr wife revealed past infidelity. I&amp;#39;m leaving. Kids hate me for it.&lt;/p&gt; &lt;/div&gt;&lt;!-- SC_ON --&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/ThrowRAhelp6425&quot;&gt; /u/ThrowRAhelp6425 &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/relationship_advice/&quot;&gt; r/relationship_advice &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/relationship_advice/comments/dje1ta/my_45m_wife_42f_revealed_she_was_unfaithful_and/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/relationship_advice/comments/dje1ta/my_45m_wife_42f_revealed_she_was_unfaithful_and/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_dje1ta</id><link href=""https://www.reddit.com/r/relationship_advice/comments/dje1ta/my_45m_wife_42f_revealed_she_was_unfaithful_and/"" /><updated>2019-10-17T22:42:36+00:00</updated><title>My [45M] Wife [42F] revealed she was unfaithful and I'm divorcing her. Our children [15F &amp; 17M] hate me for it.</title></entry><entry><author><name>/u/Prometheus1889</name><uri>https://www.reddit.com/user/Prometheus1889</uri></author><category term=""sports"" label=""r/sports""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/sports/comments/djfmoa/lebron_james_pressured_adam_silver_to_punish/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/QFpois7yNjiy-FkaS55rqg8Y0dYs5yX0CYVimigrA4U.jpg&quot; alt=&quot;LeBron James pressured Adam Silver to punish Rockets GM Daryl Morey for controversial tweet&quot; title=&quot;LeBron James pressured Adam Silver to punish Rockets GM Daryl Morey for controversial tweet&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/Prometheus1889&quot;&gt; /u/Prometheus1889 &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/sports/&quot;&gt; r/sports &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.espn.com/nba/story/_/id/27852687/inside-lebron-james-adam-silver-make-break-moments-china&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/sports/comments/djfmoa/lebron_james_pressured_adam_silver_to_punish/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_djfmoa</id><link href=""https://www.reddit.com/r/sports/comments/djfmoa/lebron_james_pressured_adam_silver_to_punish/"" /><updated>2019-10-18T00:47:26+00:00</updated><title>LeBron James pressured Adam Silver to punish Rockets GM Daryl Morey for controversial tweet</title></entry><entry><author><name>/u/JamburaStudio</name><uri>https://www.reddit.com/user/JamburaStudio</uri></author><category term=""worldnews"" label=""r/worldnews""/><content type=""html"">&amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/JamburaStudio&quot;&gt; /u/JamburaStudio &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/worldnews/&quot;&gt; r/worldnews &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.dailymail.co.uk/news/article-7587357/Distressing-footage-emerges-children-chemical-burns-Syria.html&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/worldnews/comments/djltkn/dad_stop_the_burning_i_beg_you_horrifying_footage/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_djltkn</id><link href=""https://www.reddit.com/r/worldnews/comments/djltkn/dad_stop_the_burning_i_beg_you_horrifying_footage/"" /><updated>2019-10-18T10:57:44+00:00</updated><title>'Dad stop the burning, I beg you': Horrifying footage reveals badly-burned Kurdish children in Syria amid claims Turkey is using banned weapons such as napalm and white phosphorus</title></entry><entry><author><name>/u/Dlatrex</name><uri>https://www.reddit.com/user/Dlatrex</uri></author><category term=""NatureIsFuckingLit"" label=""r/NatureIsFuckingLit""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/NatureIsFuckingLit/comments/djmc41/wild_horses_enjoy_the_ocean/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/-xjE6ZPsPqgjfM5TN79_jjHu6V_MfXe9fJ23sDaZ9cE.jpg&quot; alt=&quot;🔥 Wild horses enjoy the ocean&quot; title=&quot;🔥 Wild horses enjoy the ocean&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/Dlatrex&quot;&gt; /u/Dlatrex &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/NatureIsFuckingLit/&quot;&gt; r/NatureIsFuckingLit &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://v.redd.it/orspe416fat31&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/NatureIsFuckingLit/comments/djmc41/wild_horses_enjoy_the_ocean/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_djmc41</id><link href=""https://www.reddit.com/r/NatureIsFuckingLit/comments/djmc41/wild_horses_enjoy_the_ocean/"" /><updated>2019-10-18T11:48:05+00:00</updated><title>🔥 Wild horses enjoy the ocean</title></entry><entry><author><name>/u/ManiaforBeatles</name><uri>https://www.reddit.com/user/ManiaforBeatles</uri></author><category term=""worldnews"" label=""r/worldnews""/><content type=""html"">&amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/ManiaforBeatles&quot;&gt; /u/ManiaforBeatles &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/worldnews/&quot;&gt; r/worldnews &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.independent.co.uk/news/world/middle-east/qatar-air-conditioning-temperature-weather-heat-climate-change-athletics-world-cup-a9160751.html&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/worldnews/comments/djkr04/qatar_now_so_hot_it_has_started_airconditioning/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_djkr04</id><link href=""https://www.reddit.com/r/worldnews/comments/djkr04/qatar_now_so_hot_it_has_started_airconditioning/"" /><updated>2019-10-18T08:57:09+00:00</updated><title>Qatar now so hot it has started air-conditioning the outdoors - Giant coolers in public areas accelerating climate crisis further by using electricity from fossil fuels</title></entry><entry><author><name>/u/weiloh</name><uri>https://www.reddit.com/user/weiloh</uri></author><category term=""BikiniBottomTwitter"" label=""r/BikiniBottomTwitter""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/BikiniBottomTwitter/comments/djm08t/thats_hot/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/qMzXDEkIQXYNzZ6PXdu1VisDWXgdfFxDja6FVNmTJtU.jpg&quot; alt=&quot;Thats hot&quot; title=&quot;Thats hot&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/weiloh&quot;&gt; /u/weiloh &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/BikiniBottomTwitter/&quot;&gt; r/BikiniBottomTwitter &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/cqy1if1l9at31.jpg&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/BikiniBottomTwitter/comments/djm08t/thats_hot/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_djm08t</id><link href=""https://www.reddit.com/r/BikiniBottomTwitter/comments/djm08t/thats_hot/"" /><updated>2019-10-18T11:15:56+00:00</updated><title>Thats hot</title></entry><entry><author><name>/u/SugmaNuts</name><uri>https://www.reddit.com/user/SugmaNuts</uri></author><category term=""insanepeoplefacebook"" label=""r/insanepeoplefacebook""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/insanepeoplefacebook/comments/djm3xe/found_this_gem_recently/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/zM3R3dPUIqMCyAV5wisqJEdNBNYHpyvzFjtIPQAkBos.jpg&quot; alt=&quot;Found this gem recently&quot; title=&quot;Found this gem recently&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/SugmaNuts&quot;&gt; /u/SugmaNuts &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/insanepeoplefacebook/&quot;&gt; r/insanepeoplefacebook &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/mvbdxyzebat31.jpg&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/insanepeoplefacebook/comments/djm3xe/found_this_gem_recently/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_djm3xe</id><link href=""https://www.reddit.com/r/insanepeoplefacebook/comments/djm3xe/found_this_gem_recently/"" /><updated>2019-10-18T11:26:05+00:00</updated><title>Found this gem recently</title></entry><entry><author><name>/u/Kirosh</name><uri>https://www.reddit.com/user/Kirosh</uri></author><category term=""OnePiece"" label=""r/OnePiece""/><content type=""html"">&lt;!-- SC_OFF --&gt;&lt;div class=&quot;md&quot;&gt;&lt;p&gt;&lt;strong&gt;Chapter 959: Samurai&lt;/strong&gt;&lt;/p&gt; &lt;table&gt;&lt;thead&gt; &lt;tr&gt; &lt;th align=&quot;left&quot;&gt;&lt;strong&gt;Source&lt;/strong&gt;&lt;/th&gt; &lt;th align=&quot;left&quot;&gt;&lt;strong&gt;Status&lt;/strong&gt;&lt;/th&gt; &lt;/tr&gt; &lt;/thead&gt;&lt;tbody&gt; &lt;tr&gt; &lt;td align=&quot;left&quot;&gt;&lt;a href=&quot;https://mangaplus.shueisha.co.jp/titles/100020&quot;&gt;Official release&lt;/a&gt;&lt;/td&gt; &lt;td align=&quot;left&quot;&gt;&lt;a href=&quot;/cross&quot;&gt;&lt;/a&gt;&lt;/td&gt; &lt;/tr&gt; &lt;tr&gt; &lt;td align=&quot;left&quot;&gt;&lt;a href=&quot;https://jaiminisbox.com/reader/read/one-piece-2/en/0/959/page/1&quot;&gt;JaiminisBox&lt;/a&gt;&lt;/td&gt; &lt;td align=&quot;left&quot;&gt;&lt;a href=&quot;/tick&quot;&gt;&lt;/a&gt;&lt;/td&gt; &lt;/tr&gt; &lt;/tbody&gt;&lt;/table&gt; &lt;hr/&gt; &lt;p&gt;&lt;strong&gt;&lt;a href=&quot;https://mangaplus.shueisha.co.jp/titles/100020&quot;&gt;Ch. 959 Official Release (Official release)&lt;/a&gt;&lt;/strong&gt; 20/10/2019&lt;/p&gt; &lt;p&gt;&lt;strong&gt;Ch. 960 Scan Release:&lt;/strong&gt; 25/10/2019 *&lt;/p&gt; &lt;hr/&gt; &lt;h2&gt;Please discuss the manga here and in the theory/discussion post. Any other post will be removed during the next 24 hours.&lt;/h2&gt; &lt;hr/&gt; &lt;p&gt;&lt;strong&gt;PS:&lt;/strong&gt; Don&amp;#39;t forget to check out the official Discord: &lt;a href=&quot;https://discord.gg/onepiece&quot;&gt;https://discord.gg/onepiece&lt;/a&gt;&lt;/p&gt; &lt;/div&gt;&lt;!-- SC_ON --&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/Kirosh&quot;&gt; /u/Kirosh &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/OnePiece/&quot;&gt; r/OnePiece &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/OnePiece/comments/djixfr/one_piece_chapter_959/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/OnePiece/comments/djixfr/one_piece_chapter_959/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_djixfr</id><link href=""https://www.reddit.com/r/OnePiece/comments/djixfr/one_piece_chapter_959/"" /><updated>2019-10-18T05:30:41+00:00</updated><title>One Piece: Chapter 959</title></entry><entry><author><name>/u/KMeowRooter</name><uri>https://www.reddit.com/user/KMeowRooter</uri></author><category term=""blursedimages"" label=""r/blursedimages""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/blursedimages/comments/djllqc/blursed_chinese_fries/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/FK9fPfCnLL-jr9c7Q5oKBTTtPya6M4q_Rw5VUVmfyeg.jpg&quot; alt=&quot;blursed chinese fries&quot; title=&quot;blursed chinese fries&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/KMeowRooter&quot;&gt; /u/KMeowRooter &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/blursedimages/&quot;&gt; r/blursedimages &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/8h2bmz8c2at31.jpg&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/blursedimages/comments/djllqc/blursed_chinese_fries/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_djllqc</id><link href=""https://www.reddit.com/r/blursedimages/comments/djllqc/blursed_chinese_fries/"" /><updated>2019-10-18T10:35:13+00:00</updated><title>blursed chinese fries</title></entry><entry><author><name>/u/andrzej1220</name><uri>https://www.reddit.com/user/andrzej1220</uri></author><category term=""europe"" label=""r/europe""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/europe/comments/djj9x5/greta_thunbergs_13yearold_sister_is_struggling/&quot;&gt; &lt;img src=&quot;https://a.thumbs.redditmedia.com/_E4xg0sW-w8OPp03v_X3c0zFE5sOvZSIzU4XERcZty4.jpg&quot; alt=&quot;Greta Thunberg’s 13-year-old sister is struggling with the 'systematic bullying, hatred and harassment' her family is facing&quot; title=&quot;Greta Thunberg’s 13-year-old sister is struggling with the 'systematic bullying, hatred and harassment' her family is facing&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/andrzej1220&quot;&gt; /u/andrzej1220 &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/europe/&quot;&gt; r/europe &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.insider.com/greta-thunberg-sister-suffering-abuse-climate-change-campaign-2019-10&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/europe/comments/djj9x5/greta_thunbergs_13yearold_sister_is_struggling/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_djj9x5</id><link href=""https://www.reddit.com/r/europe/comments/djj9x5/greta_thunbergs_13yearold_sister_is_struggling/"" /><updated>2019-10-18T06:07:04+00:00</updated><title>Greta Thunberg’s 13-year-old sister is struggling with the 'systematic bullying, hatred and harassment' her family is facing</title></entry><entry><author><name>/u/iwearahoodie</name><uri>https://www.reddit.com/user/iwearahoodie</uri></author><category term=""videos"" label=""r/videos""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/videos/comments/dji6wa/dave_chappelle_explains_lebrons_position_perfectly/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/j7YmSWBGqrpgbAxnwSrU8ReTli39btou6rBJNKD6LtY.jpg&quot; alt=&quot;Dave Chappelle explains LeBron’s position perfectly&quot; title=&quot;Dave Chappelle explains LeBron’s position perfectly&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/iwearahoodie&quot;&gt; /u/iwearahoodie &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/videos/&quot;&gt; r/videos &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.youtube.com/watch?v=4Doo9kOTuUk&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/videos/comments/dji6wa/dave_chappelle_explains_lebrons_position_perfectly/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_dji6wa</id><link href=""https://www.reddit.com/r/videos/comments/dji6wa/dave_chappelle_explains_lebrons_position_perfectly/"" /><updated>2019-10-18T04:17:17+00:00</updated><title>Dave Chappelle explains LeBron’s position perfectly</title></entry><entry><author><name>/u/supremegalacticgod</name><uri>https://www.reddit.com/user/supremegalacticgod</uri></author><category term=""oddlysatisfying"" label=""r/oddlysatisfying""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/oddlysatisfying/comments/djessi/professional_onion_cutting_skills/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/Gbl488B39tU9Pu93n8q3--aRKwoRi8mJXsatm7KpIOg.jpg&quot; alt=&quot;Professional onion cutting skills&quot; title=&quot;Professional onion cutting skills&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/supremegalacticgod&quot;&gt; /u/supremegalacticgod &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/oddlysatisfying/&quot;&gt; r/oddlysatisfying &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://v.redd.it/3zzkdpr4t6t31&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/oddlysatisfying/comments/djessi/professional_onion_cutting_skills/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_djessi</id><link href=""https://www.reddit.com/r/oddlysatisfying/comments/djessi/professional_onion_cutting_skills/"" /><updated>2019-10-17T23:39:38+00:00</updated><title>Professional onion cutting skills</title></entry><entry><author><name>/u/SadTension5</name><uri>https://www.reddit.com/user/SadTension5</uri></author><category term=""Wellthatsucks"" label=""r/Wellthatsucks""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/Wellthatsucks/comments/djlkp3/this_is_what_happens_if_you_play_with_a_yoyo_too/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/4Gsft6k-UIB9RrAM1RGCwIhsAV9T1Yl9qHi1vZmzvds.jpg&quot; alt=&quot;This is what happens if you play with a yo-yo too much (I'm not kidding this is the hand of a professional yo-yo player)&quot; title=&quot;This is what happens if you play with a yo-yo too much (I'm not kidding this is the hand of a professional yo-yo player)&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/SadTension5&quot;&gt; /u/SadTension5 &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/Wellthatsucks/&quot;&gt; r/Wellthatsucks &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.imgur.com/ub3NuhT.jpg&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/Wellthatsucks/comments/djlkp3/this_is_what_happens_if_you_play_with_a_yoyo_too/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_djlkp3</id><link href=""https://www.reddit.com/r/Wellthatsucks/comments/djlkp3/this_is_what_happens_if_you_play_with_a_yoyo_too/"" /><updated>2019-10-18T10:32:01+00:00</updated><title>This is what happens if you play with a yo-yo too much (I'm not kidding this is the hand of a professional yo-yo player)</title></entry><entry><author><name>/u/cros13</name><uri>https://www.reddit.com/user/cros13</uri></author><category term=""pics"" label=""r/pics""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/pics/comments/dje0e4/this_is_the_finish_after_5_years_of_blood_sweat/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/UyPS4d4Mg4n6MiHrW-C_tdUIP0gsJF_n6xP-3oNARMc.jpg&quot; alt=&quot;This is the finish after 5 years of blood sweat and tears fixing rust in this old girl. Still ages to go, but I've reached a personal milestone today and I'm over the moon! She has paint!&quot; title=&quot;This is the finish after 5 years of blood sweat and tears fixing rust in this old girl. Still ages to go, but I've reached a personal milestone today and I'm over the moon! She has paint!&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/cros13&quot;&gt; /u/cros13 &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/pics/&quot;&gt; r/pics &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/kmhcmnxni6t31.jpg&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/pics/comments/dje0e4/this_is_the_finish_after_5_years_of_blood_sweat/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_dje0e4</id><link href=""https://www.reddit.com/r/pics/comments/dje0e4/this_is_the_finish_after_5_years_of_blood_sweat/"" /><updated>2019-10-17T22:39:40+00:00</updated><title>This is the finish after 5 years of blood sweat and tears fixing rust in this old girl. Still ages to go, but I've reached a personal milestone today and I'm over the moon! She has paint!</title></entry><entry><author><name>/u/G2Minion</name><uri>https://www.reddit.com/user/G2Minion</uri></author><category term=""leagueoflegends"" label=""r/leagueoflegends""/><content type=""html"">&lt;!-- SC_OFF --&gt;&lt;div class=&quot;md&quot;&gt;&lt;h3&gt;WORLDS 2019 GROUP STAGE&lt;/h3&gt; &lt;p&gt;&lt;a href=&quot;https://eu.lolesports.com/en/league/worlds&quot;&gt;Official page&lt;/a&gt; | &lt;a href=&quot;https://lol.gamepedia.com/2019_Season_World_Championship/Main_Event&quot;&gt;Leaguepedia&lt;/a&gt; | &lt;a href=&quot;https://liquipedia.net/leagueoflegends/World_Championship/2019/Group_Stage&quot;&gt;Liquipedia&lt;/a&gt; | &lt;a href=&quot;https://eventvods.com/featured/lol?utm_source=reddit&amp;amp;utm_medium=subreddit&amp;amp;utm_campaign=post_match_threads&quot;&gt;Eventvods.com&lt;/a&gt; | &lt;a href=&quot;http://lol.gamepedia.com/New_To_League/Welcome&quot;&gt;New to LoL&lt;/a&gt; &lt;/p&gt; &lt;hr/&gt; &lt;h3&gt;&lt;a href=&quot;https://twitter.com/lolesports/status/1185187832463273984&quot;&gt;Griffin 1-0 Cloud9&lt;/a&gt;&lt;/h3&gt; &lt;ul&gt; &lt;li&gt;&lt;p&gt;&lt;a href=&quot;https://rocket3989.github.io/worlds-2019/&quot;&gt;&lt;strong&gt;If G2 Esports win the next game they lock themselves and Griffin into the Quarterfinals, Cloud9 and Hong Kong Attitude eliminated. Check the Group Advancement scenarios to see all the possible outcomes of Group A;&lt;/strong&gt;&lt;/a&gt;&lt;/p&gt;&lt;/li&gt; &lt;li&gt;&lt;p&gt;&lt;a href=&quot;https://lol.gamepedia.com/Special:RunQuery/SpoilerFreeSchedule?SFS%5B1%5D=2019%20Season%20World%20Championship/Main%20Event&amp;amp;pfRunQueryFormName=SpoilerFreeSchedule&quot;&gt;&lt;strong&gt;Spoiler-free Schedule.&lt;/strong&gt;&lt;/a&gt;&lt;/p&gt;&lt;/li&gt; &lt;/ul&gt; &lt;p&gt;&lt;strong&gt;GRF&lt;/strong&gt; | &lt;a href=&quot;https://lol.gamepedia.com/Griffin&quot;&gt;Leaguepedia&lt;/a&gt; | &lt;a href=&quot;http://liquipedia.net/leagueoflegends/Griffin&quot;&gt;Liquipedia&lt;/a&gt; | &lt;a href=&quot;https://twitter.com/TeamGriffinLoL&quot;&gt;Twitter&lt;/a&gt;&lt;br/&gt; &lt;strong&gt;C9&lt;/strong&gt; | &lt;a href=&quot;https://lol.gamepedia.com/Cloud9&quot;&gt;Leaguepedia&lt;/a&gt; | &lt;a href=&quot;http://liquipedia.net/leagueoflegends/Cloud9&quot;&gt;Liquipedia&lt;/a&gt; | &lt;a href=&quot;http://www.cloud9.gg/&quot;&gt;Website&lt;/a&gt; | &lt;a href=&quot;http://twitter.com/Cloud9&quot;&gt;Twitter&lt;/a&gt; | &lt;a href=&quot;https://www.facebook.com/cloud9&quot;&gt;Facebook&lt;/a&gt; | &lt;a href=&quot;http://www.youtube.com/C9ggTV&quot;&gt;YouTube&lt;/a&gt; | &lt;a href=&quot;https://www.reddit.com/r/Cloud9&quot;&gt;Subreddit&lt;/a&gt; &lt;/p&gt; &lt;hr/&gt; &lt;h3&gt;MATCH 1: GRF vs. C9&lt;/h3&gt; &lt;p&gt;&lt;a href=&quot;https://i.imgur.com/Mk5AM1U.jpg&quot;&gt;&lt;strong&gt;Winner: Griffin&lt;/strong&gt; in 24m&lt;/a&gt; &lt;/p&gt; &lt;p&gt;&lt;a href=&quot;https://matchhistory.na.leagueoflegends.com/en/#match-details/ESPORTSTMNT06/1071527?gameHash=ca90e96020bac054&quot;&gt;Match History&lt;/a&gt; | &lt;a href=&quot;https://i.imgur.com/xqfaYDx.jpg&quot;&gt;Game Breakdown&lt;/a&gt; | &lt;a href=&quot;https://twitter.com/LoLEsportsStats/status/1185181946047094787&quot;&gt;Runes&lt;/a&gt; &lt;/p&gt; &lt;table&gt;&lt;thead&gt; &lt;tr&gt; &lt;th align=&quot;left&quot;&gt;&lt;/th&gt; &lt;th align=&quot;center&quot;&gt;Bans 1&lt;/th&gt; &lt;th align=&quot;center&quot;&gt;Bans 2&lt;/th&gt; &lt;th align=&quot;center&quot;&gt;&lt;a href=&quot;#mt-gold&quot;&gt;G&lt;/a&gt;&lt;/th&gt; &lt;th align=&quot;center&quot;&gt;&lt;a href=&quot;#mt-kills&quot;&gt;K&lt;/a&gt;&lt;/th&gt; &lt;th align=&quot;center&quot;&gt;&lt;a href=&quot;#mt-towers&quot;&gt;T&lt;/a&gt;&lt;/th&gt; &lt;th align=&quot;center&quot;&gt;D/B&lt;/th&gt; &lt;/tr&gt; &lt;/thead&gt;&lt;tbody&gt; &lt;tr&gt; &lt;td align=&quot;left&quot;&gt;&lt;strong&gt;GRF&lt;/strong&gt;&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;&lt;a href=&quot;#c-veigar&quot;&gt;veigar&lt;/a&gt; &lt;a href=&quot;#c-syndra&quot;&gt;syndra&lt;/a&gt; &lt;a href=&quot;#c-renekton&quot;&gt;renekton&lt;/a&gt;&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;&lt;a href=&quot;#c-tristana&quot;&gt;tristana&lt;/a&gt; &lt;a href=&quot;#c-fiora&quot;&gt;fiora&lt;/a&gt;&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;53.1k&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;21&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;9&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;&lt;a href=&quot;#mt-infernal&quot;&gt;I&lt;/a&gt;&lt;sup&gt;1&lt;/sup&gt; &lt;a href=&quot;#mt-herald&quot;&gt;H&lt;/a&gt;&lt;sup&gt;2&lt;/sup&gt; &lt;a href=&quot;#mt-mountain&quot;&gt;M&lt;/a&gt;&lt;sup&gt;3&lt;/sup&gt; &lt;a href=&quot;#mt-mountain&quot;&gt;M&lt;/a&gt;&lt;sup&gt;4&lt;/sup&gt; &lt;a href=&quot;#mt-barons&quot;&gt;B&lt;/a&gt;&lt;sup&gt;5&lt;/sup&gt;&lt;/td&gt; &lt;/tr&gt; &lt;tr&gt; &lt;td align=&quot;left&quot;&gt;&lt;strong&gt;C9&lt;/strong&gt;&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;&lt;a href=&quot;#c-pantheon&quot;&gt;pantheon&lt;/a&gt; &lt;a href=&quot;#c-yuumi&quot;&gt;yuumi&lt;/a&gt; &lt;a href=&quot;#c-jayce&quot;&gt;jayce&lt;/a&gt;&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;&lt;a href=&quot;#c-gangplank&quot;&gt;gangplank&lt;/a&gt; &lt;a href=&quot;#c-irelia&quot;&gt;irelia&lt;/a&gt;&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;34.4k&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;1&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;0&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;None&lt;/td&gt; &lt;/tr&gt; &lt;/tbody&gt;&lt;/table&gt; &lt;table&gt;&lt;thead&gt; &lt;tr&gt; &lt;th align=&quot;right&quot;&gt;&lt;strong&gt;GRF&lt;/strong&gt;&lt;/th&gt; &lt;th align=&quot;right&quot;&gt;21-1-43&lt;/th&gt; &lt;th align=&quot;center&quot;&gt;&lt;a href=&quot;#mt-kills&quot;&gt;vs&lt;/a&gt;&lt;/th&gt; &lt;th align=&quot;left&quot;&gt;1-21-4&lt;/th&gt; &lt;th align=&quot;left&quot;&gt;&lt;strong&gt;C9&lt;/strong&gt;&lt;/th&gt; &lt;/tr&gt; &lt;/thead&gt;&lt;tbody&gt; &lt;tr&gt; &lt;td align=&quot;right&quot;&gt;Sword &lt;a href=&quot;#c-akali&quot;&gt;akali&lt;/a&gt; &lt;sup&gt;3&lt;/sup&gt;&lt;/td&gt; &lt;td align=&quot;right&quot;&gt;7-0-2&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;TOP&lt;/td&gt; &lt;td align=&quot;left&quot;&gt;0-5-1&lt;/td&gt; &lt;td align=&quot;left&quot;&gt;&lt;sup&gt;4&lt;/sup&gt; &lt;a href=&quot;#c-shen&quot;&gt;shen&lt;/a&gt; Licorice&lt;/td&gt; &lt;/tr&gt; &lt;tr&gt; &lt;td align=&quot;right&quot;&gt;Tarzan &lt;a href=&quot;#c-elise&quot;&gt;elise&lt;/a&gt; &lt;sup&gt;1&lt;/sup&gt;&lt;/td&gt; &lt;td align=&quot;right&quot;&gt;2-1-9&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;JNG&lt;/td&gt; &lt;td align=&quot;left&quot;&gt;0-4-1&lt;/td&gt; &lt;td align=&quot;left&quot;&gt;&lt;sup&gt;3&lt;/sup&gt; &lt;a href=&quot;#c-nocturne&quot;&gt;nocturne&lt;/a&gt; Svenskeren&lt;/td&gt; &lt;/tr&gt; &lt;tr&gt; &lt;td align=&quot;right&quot;&gt;Chovy &lt;a href=&quot;#c-camille&quot;&gt;camille&lt;/a&gt; &lt;sup&gt;3&lt;/sup&gt;&lt;/td&gt; &lt;td align=&quot;right&quot;&gt;4-0-7&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;MID&lt;/td&gt; &lt;td align=&quot;left&quot;&gt;0-5-1&lt;/td&gt; &lt;td align=&quot;left&quot;&gt;&lt;sup&gt;1&lt;/sup&gt; &lt;a href=&quot;#c-kayle&quot;&gt;kayle&lt;/a&gt; Nisqy&lt;/td&gt; &lt;/tr&gt; &lt;tr&gt; &lt;td align=&quot;right&quot;&gt;Viper &lt;a href=&quot;#c-xayah&quot;&gt;xayah&lt;/a&gt; &lt;sup&gt;2&lt;/sup&gt;&lt;/td&gt; &lt;td align=&quot;right&quot;&gt;7-0-10&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;BOT&lt;/td&gt; &lt;td align=&quot;left&quot;&gt;1-3-0&lt;/td&gt; &lt;td align=&quot;left&quot;&gt;&lt;sup&gt;1&lt;/sup&gt; &lt;a href=&quot;#c-kaisa&quot;&gt;kaisa&lt;/a&gt; Sneaky&lt;/td&gt; &lt;/tr&gt; &lt;tr&gt; &lt;td align=&quot;right&quot;&gt;Lehends &lt;a href=&quot;#c-rakan&quot;&gt;rakan&lt;/a&gt; &lt;sup&gt;2&lt;/sup&gt;&lt;/td&gt; &lt;td align=&quot;right&quot;&gt;1-0-15&lt;/td&gt; &lt;td align=&quot;center&quot;&gt;SUP&lt;/td&gt; &lt;td align=&quot;left&quot;&gt;0-4-1&lt;/td&gt; &lt;td align=&quot;left&quot;&gt;&lt;sup&gt;2&lt;/sup&gt; &lt;a href=&quot;#c-galio&quot;&gt;galio&lt;/a&gt; Zeyzal&lt;/td&gt; &lt;/tr&gt; &lt;/tbody&gt;&lt;/table&gt; &lt;hr/&gt; &lt;p&gt;&lt;strong&gt;*&lt;a href=&quot;https://na.leagueoflegends.com/en/news/game-updates/patch/patch-919-notes&quot;&gt;Patch 9.19 Notes: Worlds 2019 Group Stage&lt;/a&gt;&lt;/strong&gt;&lt;/p&gt; &lt;hr/&gt; &lt;p&gt;&lt;a href=&quot;https://postmatch.team&quot;&gt;This thread was created by the Post-Match Team&lt;/a&gt;.&lt;/p&gt; &lt;/div&gt;&lt;!-- SC_ON --&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/G2Minion&quot;&gt; /u/G2Minion &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/leagueoflegends/&quot;&gt; r/leagueoflegends &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/leagueoflegends/comments/djnlw7/griffin_vs_cloud9_2019_world_championship_group_a/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/leagueoflegends/comments/djnlw7/griffin_vs_cloud9_2019_world_championship_group_a/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_djnlw7</id><link href=""https://www.reddit.com/r/leagueoflegends/comments/djnlw7/griffin_vs_cloud9_2019_world_championship_group_a/"" /><updated>2019-10-18T13:35:44+00:00</updated><title>Griffin vs. Cloud9 / 2019 World Championship - Group A / Post-Match Discussion</title></entry><entry><author><name>/u/Senile_Sapien</name><uri>https://www.reddit.com/user/Senile_Sapien</uri></author><category term=""movies"" label=""r/movies""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/movies/comments/djljod/exclusive_china_cancels_release_of_tarantinos/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/W99NcI-NGV8xLidQkv0hHAe7t6lsLMknhQlvfghnk3s.jpg&quot; alt=&quot;Exclusive: China cancels release of Tarantino's 'Once Upon a Time in Hollywood'&quot; title=&quot;Exclusive: China cancels release of Tarantino's 'Once Upon a Time in Hollywood'&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/Senile_Sapien&quot;&gt; /u/Senile_Sapien &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/movies/&quot;&gt; r/movies &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;http://thr.cm/DAjYIo&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/movies/comments/djljod/exclusive_china_cancels_release_of_tarantinos/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_djljod</id><link href=""https://www.reddit.com/r/movies/comments/djljod/exclusive_china_cancels_release_of_tarantinos/"" /><updated>2019-10-18T10:29:00+00:00</updated><title>Exclusive: China cancels release of Tarantino's 'Once Upon a Time in Hollywood'</title></entry><entry><author><name>/u/BunyipPouch</name><uri>https://www.reddit.com/user/BunyipPouch</uri></author><category term=""movies"" label=""r/movies""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/movies/comments/djd6ry/el_camino_a_breaking_bad_movie_draws_65_million/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/4EGw3CC2dGG4oEzST2kHyfdaWeVRA2ghPnq3Gfx8C_k.jpg&quot; alt=&quot;‘El Camino: A Breaking Bad Movie’ Draws 6.5 Million Viewers During Its First Weekend, Nielsen Says&quot; title=&quot;‘El Camino: A Breaking Bad Movie’ Draws 6.5 Million Viewers During Its First Weekend, Nielsen Says&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/BunyipPouch&quot;&gt; /u/BunyipPouch &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/movies/&quot;&gt; r/movies &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.thewrap.com/el-camino-breaking-bad-ratings-netflix/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/movies/comments/djd6ry/el_camino_a_breaking_bad_movie_draws_65_million/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_djd6ry</id><link href=""https://www.reddit.com/r/movies/comments/djd6ry/el_camino_a_breaking_bad_movie_draws_65_million/"" /><updated>2019-10-17T21:39:02+00:00</updated><title>‘El Camino: A Breaking Bad Movie’ Draws 6.5 Million Viewers During Its First Weekend, Nielsen Says</title></entry><entry><author><name>/u/gottamemethemall</name><uri>https://www.reddit.com/user/gottamemethemall</uri></author><category term=""insanepeoplefacebook"" label=""r/insanepeoplefacebook""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/insanepeoplefacebook/comments/djdmkp/imagine_being_proud_of_posting_this/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/Yd5xGxjg61BCWjtfV8o299GDm5Vfi6ljMlWEO8P2Ous.jpg&quot; alt=&quot;Imagine being proud of posting this...&quot; title=&quot;Imagine being proud of posting this...&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/gottamemethemall&quot;&gt; /u/gottamemethemall &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/insanepeoplefacebook/&quot;&gt; r/insanepeoplefacebook &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/ae2optnhd6t31.png&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/insanepeoplefacebook/comments/djdmkp/imagine_being_proud_of_posting_this/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_djdmkp</id><link href=""https://www.reddit.com/r/insanepeoplefacebook/comments/djdmkp/imagine_being_proud_of_posting_this/"" /><updated>2019-10-17T22:10:51+00:00</updated><title>Imagine being proud of posting this...</title></entry><entry><author><name>/u/AdoptedByThanos</name><uri>https://www.reddit.com/user/AdoptedByThanos</uri></author><category term=""tifu"" label=""r/tifu""/><content type=""html"">&lt;!-- SC_OFF --&gt;&lt;div class=&quot;md&quot;&gt;&lt;p&gt;Trowaway account for privacy, this happened yesterday and I don&amp;#39;t know how to make it up.&lt;/p&gt; &lt;p&gt;My girlfriend and I met at university 4 years ago, and we both graduated some time ago. Since then we&amp;#39;ve both been looking for work, but the research is more difficult than we thought, and until yesterday neither of us had managed to get a job. Yesterday my girlfriend received a positive answer to one of her applications, for a job located 430 kilometers from our hometown. She was very happy and that&amp;#39;s how she told me to come to her house to tell me some good news about the research. So expecting some good news, I took a detour to buy her sushi because we both love it, and I arrived at her house early in the evening, and that&amp;#39;s when she tells me that in two weeks she&amp;#39;s going to go more than 400 kilometers from our city, and that I now have to look in the same area as her. I totally lost my mind when she told me that. I explain that I understand that the research is complicated, but that it was no reason to look at work across the country, and that I had no desire to leave my family and friends because my life is in my hometown. The situation then became very tense, she told me that I am selfish and that I have to learn to move away, that even if there is no urgency, it is important to find a job quickly so as not to remain inactive. I then offered her a long-distance relationship for a few months because I really don&amp;#39;t want to leave, and she could come back as soon as she found a job in our city. And that, guys, she took it really hard, I&amp;#39;ve never seen her so upset in my life. She then gave me an ultimatum: either I follow her or we stop our relationship, and she asks me if I&amp;#39;m really ready to leave her to stay with my family and friends. Maybe some people can see it, but this is the moment I fucked up.&lt;/p&gt; &lt;p&gt;I still don&amp;#39;t know why my brain betrayed me like that, but the only answer I could find was : &amp;quot;Perhaps, maybe it is a small price to pay for salvation&amp;quot;. I could have made long rants to show her that I love her, that I care about her. I could have found arguments so that we could solve the situation, as any person in a relationship for 4 years would do. But no, my fucking stupidity made me quote a purple guy. She looked at me with an empty look for several seconds, I realized how much I fucked up, but nothing came from my mouth. She just asks me to leave her house, and since yesterday she speak to me or answer my calls and messages. I sent her a very long message of apology, adding that I hope we will be able to resolve the situation, but still no answer.&lt;/p&gt; &lt;p&gt;I am now staring my Thanos funko pop thinking how much this guy may have ruined part of my life.&lt;/p&gt; &lt;p&gt;TLDR : GF finds a job far away from our city and I doesn&amp;#39;t want to leave my life to live this far. She asks me if I&amp;#39;m really ready to leave her just to stay in my city and my only answer was &amp;quot;Maybe it is a small price to pay for salvation&amp;quot;.&lt;/p&gt; &lt;/div&gt;&lt;!-- SC_ON --&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/AdoptedByThanos&quot;&gt; /u/AdoptedByThanos &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/tifu/&quot;&gt; r/tifu &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/tifu/comments/djkj2x/tifu_by_quoting_thanos_while_arguing_with_my_gf/&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/tifu/comments/djkj2x/tifu_by_quoting_thanos_while_arguing_with_my_gf/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_djkj2x</id><link href=""https://www.reddit.com/r/tifu/comments/djkj2x/tifu_by_quoting_thanos_while_arguing_with_my_gf/"" /><updated>2019-10-18T08:29:57+00:00</updated><title>TIFU by quoting Thanos while arguing with my GF</title></entry><entry><author><name>/u/Failuresandwich</name><uri>https://www.reddit.com/user/Failuresandwich</uri></author><category term=""aww"" label=""r/aww""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/aww/comments/djl094/portable_snuggle/&quot;&gt; &lt;img src=&quot;https://a.thumbs.redditmedia.com/z5Apos6ISvjtYBd5mZkRhDbiYIl-SZzJKmobawEQvH0.jpg&quot; alt=&quot;Portable snuggle&quot; title=&quot;Portable snuggle&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/Failuresandwich&quot;&gt; /u/Failuresandwich &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/aww/&quot;&gt; r/aww &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.imgur.com/hcLW813.gifv&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/aww/comments/djl094/portable_snuggle/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_djl094</id><link href=""https://www.reddit.com/r/aww/comments/djl094/portable_snuggle/"" /><updated>2019-10-18T09:27:46+00:00</updated><title>Portable snuggle</title></entry><entry><author><name>/u/BufordTeeJustice</name><uri>https://www.reddit.com/user/BufordTeeJustice</uri></author><category term=""todayilearned"" label=""r/todayilearned""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/todayilearned/comments/dje9hj/til_that_banksy_once_sold_originals_of_his/&quot;&gt; &lt;img src=&quot;https://b.thumbs.redditmedia.com/a9d8AliROcdgDfO1xiNb9MHC78WFHzHXKTPBII1T4So.jpg&quot; alt=&quot;TIL that Banksy once sold originals of his artwork for $60 each from a street stall in NYC. Not knowing the truth, the public largely ignored the art. Only a few people bought them, and they’re now worth a fortune.&quot; title=&quot;TIL that Banksy once sold originals of his artwork for $60 each from a street stall in NYC. Not knowing the truth, the public largely ignored the art. Only a few people bought them, and they’re now worth a fortune.&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/BufordTeeJustice&quot;&gt; /u/BufordTeeJustice &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/todayilearned/&quot;&gt; r/todayilearned &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://www.theguardian.com/artanddesign/2014/jun/12/banskey-prints-new-york-stall-fortune-bonhams?CMP=Share_iOSApp_Other&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/todayilearned/comments/dje9hj/til_that_banksy_once_sold_originals_of_his/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_dje9hj</id><link href=""https://www.reddit.com/r/todayilearned/comments/dje9hj/til_that_banksy_once_sold_originals_of_his/"" /><updated>2019-10-17T22:58:55+00:00</updated><title>TIL that Banksy once sold originals of his artwork for $60 each from a street stall in NYC. Not knowing the truth, the public largely ignored the art. Only a few people bought them, and they’re now worth a fortune.</title></entry><entry><author><name>/u/NoKidsItsCruel</name><uri>https://www.reddit.com/user/NoKidsItsCruel</uri></author><category term=""news"" label=""r/news""/><content type=""html"">&amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/NoKidsItsCruel&quot;&gt; /u/NoKidsItsCruel &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/news/&quot;&gt; r/news &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://edition.cnn.com/2019/10/18/world/nasa-all-female-spacewalk-koch-meir-scn-trnd/index.html?utm_content=2019-10-18T11%3A00%3A08&amp;amp;utm_medium=social&amp;amp;utm_term=link&amp;amp;utm_source=twCNN&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/news/comments/djlvcc/the_first_allfemale_spacewalk_happens_today/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt;</content><id>t3_djlvcc</id><link href=""https://www.reddit.com/r/news/comments/djlvcc/the_first_allfemale_spacewalk_happens_today/"" /><updated>2019-10-18T11:02:45+00:00</updated><title>The first all-female spacewalk happens today</title></entry><entry><author><name>/u/Manus89</name><uri>https://www.reddit.com/user/Manus89</uri></author><category term=""therewasanattempt"" label=""r/therewasanattempt""/><content type=""html"">&lt;table&gt; &lt;tr&gt;&lt;td&gt; &lt;a href=&quot;https://www.reddit.com/r/therewasanattempt/comments/dje6gy/to_racism/&quot;&gt; &lt;img src=&quot;https://a.thumbs.redditmedia.com/qe731uuydI8xsdB4fppgj-swPV0OfJKCchoGboR9Sn0.jpg&quot; alt=&quot;To racism&quot; title=&quot;To racism&quot; /&gt; &lt;/a&gt; &lt;/td&gt;&lt;td&gt; &amp;#32; submitted by &amp;#32; &lt;a href=&quot;https://www.reddit.com/user/Manus89&quot;&gt; /u/Manus89 &lt;/a&gt; &amp;#32; to &amp;#32; &lt;a href=&quot;https://www.reddit.com/r/therewasanattempt/&quot;&gt; r/therewasanattempt &lt;/a&gt; &lt;br/&gt; &lt;span&gt;&lt;a href=&quot;https://i.redd.it/9aqp0w2ae5t31.png&quot;&gt;[link]&lt;/a&gt;&lt;/span&gt; &amp;#32; &lt;span&gt;&lt;a href=&quot;https://www.reddit.com/r/therewasanattempt/comments/dje6gy/to_racism/&quot;&gt;[comments]&lt;/a&gt;&lt;/span&gt; &lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;</content><id>t3_dje6gy</id><link href=""https://www.reddit.com/r/therewasanattempt/comments/dje6gy/to_racism/"" /><updated>2019-10-17T22:52:22+00:00</updated><title>To racism</title></entry></feed>";
            return @"<rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"" xmlns:prism=""http://prismstandard.org/namespaces/basic/2.0/"" xmlns:dc=""http://purl.org/dc/elements/1.1/""
         xmlns:content=""http://purl.org/rss/1.0/modules/content/"" xmlns=""http://purl.org/rss/1.0/"" xmlns:admin=""http://webns.net/mvcb/"">
    <channel rdf:about=""http://feeds.nature.com/nature/rss/current"">
        <title>Nature</title>
        <description>Nature is the world’s foremost international weekly scientific journal and is the flagship journal for Nature Research. It publishes the finest peer-reviewed research in all fields of science and technology on the basis of its originality, importance, interdisciplinary interest, timeliness, accessibility, elegance and surprising conclusions. Nature’s landmark papers, award winning news, leading comment and expert opinion on important, topical scientific news and events enable readers to share the latest discoveries in science and evolve the discussion amongst the global scientific community.    </description>
        <link>http://feeds.nature.com/nature/rss/current</link>
        <admin:generatorAgent rdf:resource=""http://www.nature.com/""/>
        <admin:errorReportsTo rdf:resource=""mailto:feedback@nature.com""/>
        <dc:publisher>Nature Publishing Group</dc:publisher>
        <dc:language>en</dc:language>
        <dc:rights>© 2020 Macmillan Publishers Limited, part of Springer Nature. All rights reserved.</dc:rights>
        <prism:publicationName>Nature</prism:publicationName>
        
        
        <prism:copyright>© 2020 Macmillan Publishers Limited, part of Springer Nature. All rights reserved.</prism:copyright>
        <prism:rightsAgent>permissions@nature.com</prism:rightsAgent>
        <image rdf:resource=""https://www.nature.com/uploads/product/nature/rss.gif""/>
        <items>
            <rdf:Seq>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03868-8""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03903-8""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03867-9""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03940-3""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03939-w""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03941-2""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03913-6""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03942-1""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03911-8""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03915-4""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03909-2""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03787-8""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03728-5""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1911-y""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1869-9""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1873-0""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1864-1""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1877-9""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1867-y""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1865-0""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1872-1""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03938-x""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03900-x""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03929-y""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03906-5""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03883-9""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03926-1""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03914-5""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03886-6""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03945-y""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03877-7""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03912-7""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03899-1""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03948-9""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03910-9""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03873-x""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03932-3""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03885-7""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03924-3""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03927-0""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1896-6""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03890-w""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03895-5""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1856-1""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03846-0""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03603-3""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1844-5""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1863-2""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03853-1""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1851-6""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1809-8""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1847-2""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03837-1""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03820-w""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03844-2""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03896-4""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1842-7""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03849-x""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03851-3""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1857-0""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03811-x""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1826-7""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03891-9""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1833-8""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1852-5""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1840-9""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03847-z""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03838-0""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03845-1""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1824-9""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1843-6""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03852-2""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03857-x""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/s41586-019-1827-6""/>
                
                    <rdf:li rdf:resource=""https://www.nature.com/articles/d41586-019-03836-2""/>
                
            </rdf:Seq>
        </items>
    </channel>
    <image rdf:about=""https://www.nature.com/uploads/product/nature/rss.gif"">
        <title>Nature</title>
        <url>https://www.nature.com/uploads/product/nature/rss.gif</url>
        <link>http://feeds.nature.com/nature/rss/current</link>
    </image>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03868-8"">
            <title><![CDATA[Podcast: Our reporters’ top picks of 2019]]></title>
            <link>https://www.nature.com/articles/d41586-019-03868-8</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 01 January 2020; <a href=""https://www.nature.com/articles/d41586-019-03868-8"">doi:10.1038/d41586-019-03868-8</a></p>The podcast team share some of their highlights from the past 12 months.]]></content:encoded>
            <dc:title><![CDATA[Podcast: Our reporters’ top picks of 2019]]></dc:title>
            
            <dc:identifier>doi:10.1038/d41586-019-03868-8</dc:identifier>
            <dc:source>Nature, Published online: 2020-01-01; | doi:10.1038/d41586-019-03868-8</dc:source>
            <dc:date>2020-01-01</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03868-8</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03868-8</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03903-8"">
            <title><![CDATA[The game]]></title>
            <link>https://www.nature.com/articles/d41586-019-03903-8</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 01 January 2020; <a href=""https://www.nature.com/articles/d41586-019-03903-8"">doi:10.1038/d41586-019-03903-8</a></p>Playing by the rules.]]></content:encoded>
            <dc:title><![CDATA[The game]]></dc:title>
            <dc:creator>Michael Adam Robson</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03903-8</dc:identifier>
            <dc:source>Nature, Published online: 2020-01-01; | doi:10.1038/d41586-019-03903-8</dc:source>
            <dc:date>2020-01-01</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03903-8</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03903-8</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03867-9"">
            <title><![CDATA[From the archive]]></title>
            <link>https://www.nature.com/articles/d41586-019-03867-9</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 31 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03867-9"">doi:10.1038/d41586-019-03867-9</a></p>How Nature reported a successful treatment for rattlesnake bites in 1870, and ambitions to modernize the Indian sugar industry in 1869.]]></content:encoded>
            <dc:title><![CDATA[From the archive]]></dc:title>
            
            <dc:identifier>doi:10.1038/d41586-019-03867-9</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-31; | doi:10.1038/d41586-019-03867-9</dc:source>
            <dc:date>2019-12-31</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03867-9</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03867-9</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03940-3"">
            <title><![CDATA[China’s different shades of greening]]></title>
            <link>https://www.nature.com/articles/d41586-019-03940-3</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 31 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03940-3"">doi:10.1038/d41586-019-03940-3</a></p>China’s different shades of greening]]></content:encoded>
            <dc:title><![CDATA[China’s different shades of greening]]></dc:title>
            <dc:creator>Lele Shu</dc:creator><dc:creator>Zexuan Xu</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03940-3</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-31; | doi:10.1038/d41586-019-03940-3</dc:source>
            <dc:date>2019-12-31</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03940-3</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03940-3</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03939-w"">
            <title><![CDATA[The quest for top female academics — a search and destroy mission?]]></title>
            <link>https://www.nature.com/articles/d41586-019-03939-w</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 31 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03939-w"">doi:10.1038/d41586-019-03939-w</a></p>The quest for top female academics — a search and destroy mission?]]></content:encoded>
            <dc:title><![CDATA[The quest for top female academics — a search and destroy mission?]]></dc:title>
            <dc:creator>Susanne Täuber</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03939-w</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-31; | doi:10.1038/d41586-019-03939-w</dc:source>
            <dc:date>2019-12-31</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03939-w</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03939-w</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03941-2"">
            <title><![CDATA[Earthquake prediction: heed the signs and save lives]]></title>
            <link>https://www.nature.com/articles/d41586-019-03941-2</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 31 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03941-2"">doi:10.1038/d41586-019-03941-2</a></p>Earthquake prediction: heed the signs and save lives]]></content:encoded>
            <dc:title><![CDATA[Earthquake prediction: heed the signs and save lives]]></dc:title>
            <dc:creator>Sergio Bertolucci</dc:creator><dc:creator>Francesco Mulargia</dc:creator><dc:creator>Domenico Giardini</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03941-2</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-31; | doi:10.1038/d41586-019-03941-2</dc:source>
            <dc:date>2019-12-31</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03941-2</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03941-2</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03913-6"">
            <title><![CDATA[Drink more recycled wastewater]]></title>
            <link>https://www.nature.com/articles/d41586-019-03913-6</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 31 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03913-6"">doi:10.1038/d41586-019-03913-6</a></p>There is no room for squeamishness in the face of the world’s growing water shortage — three steps could vastly improve the image of reused water for drinking.]]></content:encoded>
            <dc:title><![CDATA[Drink more recycled wastewater]]></dc:title>
            <dc:creator>Cecilia Tortajada</dc:creator><dc:creator>Pierre van Rensburg</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03913-6</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-31; | doi:10.1038/d41586-019-03913-6</dc:source>
            <dc:date>2019-12-31</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03913-6</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03913-6</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03942-1"">
            <title><![CDATA[Testosterone’s role in ovulation]]></title>
            <link>https://www.nature.com/articles/d41586-019-03942-1</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 31 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03942-1"">doi:10.1038/d41586-019-03942-1</a></p>Testosterone’s role in ovulation]]></content:encoded>
            <dc:title><![CDATA[Testosterone’s role in ovulation]]></dc:title>
            <dc:creator>Rebecca Jordan-Young</dc:creator><dc:creator>Katrina Karkazis</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03942-1</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-31; | doi:10.1038/d41586-019-03942-1</dc:source>
            <dc:date>2019-12-31</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03942-1</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03942-1</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03911-8"">
            <title><![CDATA[The super-cool materials that send heat to space]]></title>
            <link>https://www.nature.com/articles/d41586-019-03911-8</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 31 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03911-8"">doi:10.1038/d41586-019-03911-8</a></p>Paints, plastics and even wood can be engineered to stay cool in direct sunlight — but their role in displacing power-hungry air conditioners remains unclear.]]></content:encoded>
            <dc:title><![CDATA[The super-cool materials that send heat to space]]></dc:title>
            <dc:creator>XiaoZhi Lim</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03911-8</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-31; | doi:10.1038/d41586-019-03911-8</dc:source>
            <dc:date>2019-12-31</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03911-8</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03911-8</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03915-4"">
            <title><![CDATA[A space for contemplation]]></title>
            <link>https://www.nature.com/articles/d41586-019-03915-4</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 30 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03915-4"">doi:10.1038/d41586-019-03915-4</a></p>Paul Nurse, director of the Francis Crick Institute in London, likes to think amid the hustle and bustle of the institute’s bright and lively atrium.]]></content:encoded>
            <dc:title><![CDATA[A space for contemplation]]></dc:title>
            <dc:creator> Josie Glausiusz</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03915-4</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-30; | doi:10.1038/d41586-019-03915-4</dc:source>
            <dc:date>2019-12-30</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03915-4</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03915-4</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03909-2"">
            <title><![CDATA[A toast to the error detectors]]></title>
            <link>https://www.nature.com/articles/d41586-019-03909-2</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 30 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03909-2"">doi:10.1038/d41586-019-03909-2</a></p>Let 2020 be the year in which we value those who ensure that science is self-correcting.]]></content:encoded>
            <dc:title><![CDATA[A toast to the error detectors]]></dc:title>
            <dc:creator>Simine Vazire</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03909-2</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-30; | doi:10.1038/d41586-019-03909-2</dc:source>
            <dc:date>2019-12-30</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03909-2</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03909-2</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03787-8"">
            <title><![CDATA[<i>PastCast: The Quantum Theory</i>]]></title>
            <link>https://www.nature.com/articles/d41586-019-03787-8</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 27 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03787-8"">doi:10.1038/d41586-019-03787-8</a></p>In the Nature PastCast series, we delve into the archives to tell the stories behind some of Nature’s biggest papers.]]></content:encoded>
            <dc:title><![CDATA[<i>PastCast: The Quantum Theory</i>]]></dc:title>
            <dc:creator>Kerri Smith</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03787-8</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-27; | doi:10.1038/d41586-019-03787-8</dc:source>
            <dc:date>2019-12-27</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03787-8</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03787-8</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03728-5"">
            <title><![CDATA[Malaria-carrying mosquitoes get a leg up on insecticides]]></title>
            <link>https://www.nature.com/articles/d41586-019-03728-5</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 25 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03728-5"">doi:10.1038/d41586-019-03728-5</a></p>A chemosensory protein enriched in the legs of malaria-carrying mosquitoes gives them resistance to insecticides used to treat bed nets. This discovery points to the challenges of tackling malaria.]]></content:encoded>
            <dc:title><![CDATA[Malaria-carrying mosquitoes get a leg up on insecticides]]></dc:title>
            <dc:creator>Flaminia Catteruccia</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03728-5</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-25; | doi:10.1038/d41586-019-03728-5</dc:source>
            <dc:date>2019-12-25</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03728-5</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03728-5</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1911-y"">
            <title><![CDATA[GDF15 mediates the effects of metformin on body weight and energy balance]]></title>
            <link>https://www.nature.com/articles/s41586-019-1911-y</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 25 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1911-y"">doi:10.1038/s41586-019-1911-y</a></p>GDF15 mediates the effects of metformin on body weight and energy balance]]></content:encoded>
            <dc:title><![CDATA[GDF15 mediates the effects of metformin on body weight and energy balance]]></dc:title>
            <dc:creator>Anthony P. Coll</dc:creator><dc:creator>Michael Chen</dc:creator><dc:creator>Pranali Taskar</dc:creator><dc:creator>Debra Rimmington</dc:creator><dc:creator>Satish Patel</dc:creator><dc:creator>John Tadross</dc:creator><dc:creator>Irene Cimino</dc:creator><dc:creator>Ming Yang</dc:creator><dc:creator>Paul Welsh</dc:creator><dc:creator>Samuel Virtue</dc:creator><dc:creator>Deborah A. Goldspink</dc:creator><dc:creator>Emily L. Miedzybrodzka</dc:creator><dc:creator>Adam R. Konopka</dc:creator><dc:creator>Raul Ruiz Esponda</dc:creator><dc:creator>Jeffrey T-J. Huang</dc:creator><dc:creator>Y. C. Loraine Tung</dc:creator><dc:creator>Sergio Rodriguez-Cuenca</dc:creator><dc:creator>Rute A. Tomaz</dc:creator><dc:creator>Heather P. Harding</dc:creator><dc:creator>Audrey Melvin</dc:creator><dc:creator>Giles S. H. Yeo</dc:creator><dc:creator>David Preiss</dc:creator><dc:creator>Antonio Vidal-Puig</dc:creator><dc:creator>Ludovic Vallier</dc:creator><dc:creator>K. Sreekumaran Nair</dc:creator><dc:creator>Nicholas J. Wareham</dc:creator><dc:creator>David Ron</dc:creator><dc:creator>Fiona M. Gribble</dc:creator><dc:creator>Frank Reimann</dc:creator><dc:creator>Naveed Sattar</dc:creator><dc:creator>David B. Savage</dc:creator><dc:creator>Bernard B. Allan</dc:creator><dc:creator>Stephen O’Rahilly</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1911-y</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-25; | doi:10.1038/s41586-019-1911-y</dc:source>
            <dc:date>2019-12-25</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1911-y</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1911-y</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1869-9"">
            <title><![CDATA[Cortical pattern generation during dexterous movement is input-driven]]></title>
            <link>https://www.nature.com/articles/s41586-019-1869-9</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 25 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1869-9"">doi:10.1038/s41586-019-1869-9</a></p>The complex patterns of activity in motor cortex that control movements such as reach and grasp are dependent on both upstream neuronal activity in the thalamus and the current state of the cortex.]]></content:encoded>
            <dc:title><![CDATA[Cortical pattern generation during dexterous movement is input-driven]]></dc:title>
            <dc:creator>Britton A. Sauerbrei</dc:creator><dc:creator>Jian-Zhong Guo</dc:creator><dc:creator>Jeremy D. Cohen</dc:creator><dc:creator>Matteo Mischiati</dc:creator><dc:creator>Wendy Guo</dc:creator><dc:creator>Mayank Kabra</dc:creator><dc:creator>Nakul Verma</dc:creator><dc:creator>Brett Mensh</dc:creator><dc:creator>Kristin Branson</dc:creator><dc:creator>Adam W. Hantman</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1869-9</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-25; | doi:10.1038/s41586-019-1869-9</dc:source>
            <dc:date>2019-12-25</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1869-9</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1869-9</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1873-0"">
            <title><![CDATA[A GPR174–CCL21 module imparts sexual dimorphism to humoral immunity]]></title>
            <link>https://www.nature.com/articles/s41586-019-1873-0</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 25 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1873-0"">doi:10.1038/s41586-019-1873-0</a></p>Male and female B cells show differing abilities to localize and contribute to germinal centres, in a way that depends on the G-protein-coupled guidance receptor GPR174 and its chemokine ligand CCL21.]]></content:encoded>
            <dc:title><![CDATA[A GPR174–CCL21 module imparts sexual dimorphism to humoral immunity]]></dc:title>
            <dc:creator>Ruozhu Zhao</dc:creator><dc:creator>Xin Chen</dc:creator><dc:creator>Weiwei Ma</dc:creator><dc:creator>Jinyu Zhang</dc:creator><dc:creator>Jie Guo</dc:creator><dc:creator>Xiu Zhong</dc:creator><dc:creator>Jiacheng Yao</dc:creator><dc:creator>Jiahui Sun</dc:creator><dc:creator>Julian Rubinfien</dc:creator><dc:creator>Xuyu Zhou</dc:creator><dc:creator>Jianbin Wang</dc:creator><dc:creator>Hai Qi</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1873-0</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-25; | doi:10.1038/s41586-019-1873-0</dc:source>
            <dc:date>2019-12-25</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1873-0</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1873-0</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1864-1"">
            <title><![CDATA[A sensory appendage protein protects malaria vectors from pyrethroids]]></title>
            <link>https://www.nature.com/articles/s41586-019-1864-1</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 25 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1864-1"">doi:10.1038/s41586-019-1864-1</a></p>The leg-enriched sensory appendage protein, SAP2, confers pyrethroid resistance to Anopheles gambiae, through high-affinity binding of pyrethroid insecticides; an observed selective sweep in field mosquitoes mirrors the increasing resistance reported in Africa.]]></content:encoded>
            <dc:title><![CDATA[A sensory appendage protein protects malaria vectors from pyrethroids]]></dc:title>
            <dc:creator>Victoria A. Ingham</dc:creator><dc:creator>Amalia Anthousi</dc:creator><dc:creator>Vassilis Douris</dc:creator><dc:creator>Nicholas J. Harding</dc:creator><dc:creator>Gareth Lycett</dc:creator><dc:creator>Marion Morris</dc:creator><dc:creator>John Vontas</dc:creator><dc:creator>Hilary Ranson</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1864-1</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-25; | doi:10.1038/s41586-019-1864-1</dc:source>
            <dc:date>2019-12-25</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1864-1</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1864-1</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1877-9"">
            <title><![CDATA[H2A.Z facilitates licensing and activation of early replication origins]]></title>
            <link>https://www.nature.com/articles/s41586-019-1877-9</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 25 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1877-9"">doi:10.1038/s41586-019-1877-9</a></p>DNA replication in eukaryotes requires the histone variant H2A.Z, which binds the enzyme SUV420H1 to promote the dimethylation of histone H4, in turn recruiting the origin-recognition complex to activate early replication origins.]]></content:encoded>
            <dc:title><![CDATA[H2A.Z facilitates licensing and activation of early replication origins]]></dc:title>
            <dc:creator>Haizhen Long</dc:creator><dc:creator>Liwei Zhang</dc:creator><dc:creator>Mengjie Lv</dc:creator><dc:creator>Zengqi Wen</dc:creator><dc:creator>Wenhao Zhang</dc:creator><dc:creator>Xiulan Chen</dc:creator><dc:creator>Peitao Zhang</dc:creator><dc:creator>Tongqing Li</dc:creator><dc:creator>Luyuan Chang</dc:creator><dc:creator>Caiwei Jin</dc:creator><dc:creator>Guozhao Wu</dc:creator><dc:creator>Xi Wang</dc:creator><dc:creator>Fuquan Yang</dc:creator><dc:creator>Jianfeng Pei</dc:creator><dc:creator>Ping Chen</dc:creator><dc:creator>Raphael Margueron</dc:creator><dc:creator>Haiteng Deng</dc:creator><dc:creator>Mingzhao Zhu</dc:creator><dc:creator>Guohong Li</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1877-9</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-25; | doi:10.1038/s41586-019-1877-9</dc:source>
            <dc:date>2019-12-25</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1877-9</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1877-9</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1867-y"">
            <title><![CDATA[Resonant microwave-mediated interactions between distant electron spins]]></title>
            <link>https://www.nature.com/articles/s41586-019-1867-y</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 25 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1867-y"">doi:10.1038/s41586-019-1867-y</a></p>Microwave-mediated coupling of electron spins separated by more than 4 mm is demonstrated, suggesting the possibility of using photons at microwave frequencies to create long-range two-qubit gates between distant spins.]]></content:encoded>
            <dc:title><![CDATA[Resonant microwave-mediated interactions between distant electron spins]]></dc:title>
            <dc:creator>F. Borjans</dc:creator><dc:creator>X. G. Croot</dc:creator><dc:creator>X. Mi</dc:creator><dc:creator>M. J. Gullans</dc:creator><dc:creator>J. R. Petta</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1867-y</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-25; | doi:10.1038/s41586-019-1867-y</dc:source>
            <dc:date>2019-12-25</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1867-y</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1867-y</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1865-0"">
            <title><![CDATA[Microbial bile acid metabolites modulate gut RORγ<sup>+</sup> regulatory T cell homeostasis]]></title>
            <link>https://www.nature.com/articles/s41586-019-1865-0</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 25 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1865-0"">doi:10.1038/s41586-019-1865-0</a></p>Both dietary and microbial factors influence the composition of the gut bile acid pool, which in turn modulates the frequencies and functionalities of RORγ-expressing colonic FOXP3+ regulatory T cells, contributing to protection from inflammatory colitis.]]></content:encoded>
            <dc:title><![CDATA[Microbial bile acid metabolites modulate gut RORγ<sup>+</sup> regulatory T cell homeostasis]]></dc:title>
            <dc:creator>Xinyang Song</dc:creator><dc:creator>Ximei Sun</dc:creator><dc:creator>Sungwhan F. Oh</dc:creator><dc:creator>Meng Wu</dc:creator><dc:creator>Yanbo Zhang</dc:creator><dc:creator>Wen Zheng</dc:creator><dc:creator>Naama Geva-Zatorsky</dc:creator><dc:creator>Ray Jupp</dc:creator><dc:creator>Diane Mathis</dc:creator><dc:creator>Christophe Benoist</dc:creator><dc:creator>Dennis L. Kasper</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1865-0</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-25; | doi:10.1038/s41586-019-1865-0</dc:source>
            <dc:date>2019-12-25</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1865-0</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1865-0</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1872-1"">
            <title><![CDATA[Mapping disparities in education across low- and middle-income countries]]></title>
            <link>https://www.nature.com/articles/s41586-019-1872-1</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 25 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1872-1"">doi:10.1038/s41586-019-1872-1</a></p>Analyses of the proportions of individuals who have completed key levels of schooling across all low- and middle-income countries from 2000 to 2017 reveal inequalities across countries as well as within populations.]]></content:encoded>
            <dc:title><![CDATA[Mapping disparities in education across low- and middle-income countries]]></dc:title>
            <dc:creator>Nicholas Graetz</dc:creator><dc:creator>Lauren Woyczynski</dc:creator><dc:creator>Katherine F. Wilson</dc:creator><dc:creator>Jason B. Hall</dc:creator><dc:creator>Kalkidan Hassen Abate</dc:creator><dc:creator>Foad Abd-Allah</dc:creator><dc:creator>Oladimeji M. Adebayo</dc:creator><dc:creator>Victor Adekanmbi</dc:creator><dc:creator>Mahdi Afshari</dc:creator><dc:creator>Olufemi Ajumobi</dc:creator><dc:creator>Tomi Akinyemiju</dc:creator><dc:creator>Fares Alahdab</dc:creator><dc:creator>Ziyad Al-Aly</dc:creator><dc:creator>Jacqueline Elizabeth Alcalde Rabanal</dc:creator><dc:creator>Mehran Alijanzadeh</dc:creator><dc:creator>Vahid Alipour</dc:creator><dc:creator>Khalid Altirkawi</dc:creator><dc:creator>Mohammadreza Amiresmaili</dc:creator><dc:creator>Nahla Hamed Anber</dc:creator><dc:creator>Catalina Liliana Andrei</dc:creator><dc:creator>Mina Anjomshoa</dc:creator><dc:creator>Carl Abelardo T. Antonio</dc:creator><dc:creator>Jalal Arabloo</dc:creator><dc:creator>Olatunde Aremu</dc:creator><dc:creator>Krishna K. Aryal</dc:creator><dc:creator>Mehran Asadi-Aliabadi</dc:creator><dc:creator>Suleman Atique</dc:creator><dc:creator>Marcel Ausloos</dc:creator><dc:creator>Ashish Awasthi</dc:creator><dc:creator>Beatriz Paulina Ayala Quintanilla</dc:creator><dc:creator>Samad Azari</dc:creator><dc:creator>Alaa Badawi</dc:creator><dc:creator>Joseph Adel Mattar Banoub</dc:creator><dc:creator>Suzanne Lyn Barker-Collo</dc:creator><dc:creator>Anthony Barnett</dc:creator><dc:creator>Neeraj Bedi</dc:creator><dc:creator>Derrick A. Bennett</dc:creator><dc:creator>Natalia V. Bhattacharjee</dc:creator><dc:creator>Krittika Bhattacharyya</dc:creator><dc:creator>Suraj Bhattarai</dc:creator><dc:creator>Zulfiqar A. Bhutta</dc:creator><dc:creator>Ali Bijani</dc:creator><dc:creator>Boris Bikbov</dc:creator><dc:creator>Gabrielle Britton</dc:creator><dc:creator>Roy Burstein</dc:creator><dc:creator>Zahid A. Butt</dc:creator><dc:creator>Rosario Cárdenas</dc:creator><dc:creator>Félix Carvalho</dc:creator><dc:creator>Carlos A. Castañeda-Orjuela</dc:creator><dc:creator>Franz Castro</dc:creator><dc:creator>Ester Cerin</dc:creator><dc:creator>Jung-Chen Chang</dc:creator><dc:creator>Michael L. Collison</dc:creator><dc:creator>Cyrus Cooper</dc:creator><dc:creator>Michael A. Cork</dc:creator><dc:creator>Farah Daoud</dc:creator><dc:creator>Rajat Das Gupta</dc:creator><dc:creator>Nicole Davis Weaver</dc:creator><dc:creator>Jan-Walter De Neve</dc:creator><dc:creator>Kebede Deribe</dc:creator><dc:creator>Beruk Berhanu Desalegn</dc:creator><dc:creator>Aniruddha Deshpande</dc:creator><dc:creator>Melaku Desta</dc:creator><dc:creator>Meghnath Dhimal</dc:creator><dc:creator>Daniel Diaz</dc:creator><dc:creator>Mesfin Tadese Dinberu</dc:creator><dc:creator>Shirin Djalalinia</dc:creator><dc:creator>Manisha Dubey</dc:creator><dc:creator>Eleonora Dubljanin</dc:creator><dc:creator>Andre R. Durães</dc:creator><dc:creator>Laura Dwyer-Lindgren</dc:creator><dc:creator>Lucas Earl</dc:creator><dc:creator>Mohammad Ebrahimi Kalan</dc:creator><dc:creator>Ziad El-Khatib</dc:creator><dc:creator>Babak Eshrati</dc:creator><dc:creator>Mahbobeh Faramarzi</dc:creator><dc:creator>Mohammad Fareed</dc:creator><dc:creator>Andre Faro</dc:creator><dc:creator>Seyed-Mohammad Fereshtehnejad</dc:creator><dc:creator>Eduarda Fernandes</dc:creator><dc:creator>Irina Filip</dc:creator><dc:creator>Florian Fischer</dc:creator><dc:creator>Takeshi Fukumoto</dc:creator><dc:creator>Jose A. García</dc:creator><dc:creator>Paramjit Singh Gill</dc:creator><dc:creator>Tiffany K. Gill</dc:creator><dc:creator>Philimon N. Gona</dc:creator><dc:creator>Sameer Vali Gopalani</dc:creator><dc:creator>Ayman Grada</dc:creator><dc:creator>Yuming Guo</dc:creator><dc:creator>Rajeev Gupta</dc:creator><dc:creator>Vipin Gupta</dc:creator><dc:creator>Arvin Haj-Mirzaian</dc:creator><dc:creator>Arya Haj-Mirzaian</dc:creator><dc:creator>Randah R. Hamadeh</dc:creator><dc:creator>Samer Hamidi</dc:creator><dc:creator>Mehedi Hasan</dc:creator><dc:creator>Hamid Yimam Hassen</dc:creator><dc:creator>Delia Hendrie</dc:creator><dc:creator>Andualem Henok</dc:creator><dc:creator>Nathaniel J. Henry</dc:creator><dc:creator>Bernardo Hernández Prado</dc:creator><dc:creator>Claudiu Herteliu</dc:creator><dc:creator>Michael K. Hole</dc:creator><dc:creator>Naznin Hossain</dc:creator><dc:creator>Mehdi Hosseinzadeh</dc:creator><dc:creator>Guoqing Hu</dc:creator><dc:creator>Olayinka Stephen Ilesanmi</dc:creator><dc:creator>Seyed Sina Naghibi Irvani</dc:creator><dc:creator>Sheikh Mohammed Shariful Islam</dc:creator><dc:creator>Neda Izadi</dc:creator><dc:creator>Mihajlo Jakovljevic</dc:creator><dc:creator>Ravi Prakash Jha</dc:creator><dc:creator>John S. Ji</dc:creator><dc:creator>Jost B. Jonas</dc:creator><dc:creator>Zahra Jorjoran Shushtari</dc:creator><dc:creator>Jacek Jerzy Jozwiak</dc:creator><dc:creator>Tanuj Kanchan</dc:creator><dc:creator>Amir Kasaeian</dc:creator><dc:creator>Ali Kazemi Karyani</dc:creator><dc:creator>Peter Njenga Keiyoro</dc:creator><dc:creator>Chandrasekharan Nair Kesavachandran</dc:creator><dc:creator>Yousef Saleh Khader</dc:creator><dc:creator>Morteza Abdullatif Khafaie</dc:creator><dc:creator>Ejaz Ahmad Khan</dc:creator><dc:creator>Mona M. Khater</dc:creator><dc:creator>Aliasghar A. Kiadaliri</dc:creator><dc:creator>Daniel N. Kiirithio</dc:creator><dc:creator>Yun Jin Kim</dc:creator><dc:creator>Ruth W. Kimokoti</dc:creator><dc:creator>Damaris K. Kinyoki</dc:creator><dc:creator>Adnan Kisa</dc:creator><dc:creator>Soewarta Kosen</dc:creator><dc:creator>Ai Koyanagi</dc:creator><dc:creator>Kewal Krishan</dc:creator><dc:creator>Barthelemy Kuate Defo</dc:creator><dc:creator>Manasi Kumar</dc:creator><dc:creator>Pushpendra Kumar</dc:creator><dc:creator>Faris Hasan Lami</dc:creator><dc:creator>Paul H. Lee</dc:creator><dc:creator>Aubrey J. Levine</dc:creator><dc:creator>Shanshan Li</dc:creator><dc:creator>Yu Liao</dc:creator><dc:creator>Lee-Ling Lim</dc:creator><dc:creator>Stefan Listl</dc:creator><dc:creator>Jaifred Christian F. Lopez</dc:creator><dc:creator>Marek Majdan</dc:creator><dc:creator>Reza Majdzadeh</dc:creator><dc:creator>Azeem Majeed</dc:creator><dc:creator>Reza Malekzadeh</dc:creator><dc:creator>Mohammad Ali Mansournia</dc:creator><dc:creator>Francisco Rogerlândio Martins-Melo</dc:creator><dc:creator>Anthony Masaka</dc:creator><dc:creator>Benjamin Ballard Massenburg</dc:creator><dc:creator>Benjamin K. Mayala</dc:creator><dc:creator>Kala M. Mehta</dc:creator><dc:creator>Walter Mendoza</dc:creator><dc:creator>George A. Mensah</dc:creator><dc:creator>Tuomo J. Meretoja</dc:creator><dc:creator>Tomislav Mestrovic</dc:creator><dc:creator>Ted R. Miller</dc:creator><dc:creator>G. K. Mini</dc:creator><dc:creator>Erkin M. Mirrakhimov</dc:creator><dc:creator>Babak Moazen</dc:creator><dc:creator>Dara K. Mohammad</dc:creator><dc:creator>Aso Mohammad Darwesh</dc:creator><dc:creator>Shafiu Mohammed</dc:creator><dc:creator>Farnam Mohebi</dc:creator><dc:creator>Ali H. Mokdad</dc:creator><dc:creator>Lorenzo Monasta</dc:creator><dc:creator>Yoshan Moodley</dc:creator><dc:creator>Mahmood Moosazadeh</dc:creator><dc:creator>Ghobad Moradi</dc:creator><dc:creator>Maziar Moradi-Lakeh</dc:creator><dc:creator>Paula Moraga</dc:creator><dc:creator>Lidia Morawska</dc:creator><dc:creator>Shane Douglas Morrison</dc:creator><dc:creator>Jonathan F. Mosser</dc:creator><dc:creator>Seyyed Meysam Mousavi</dc:creator><dc:creator>Christopher J. L. Murray</dc:creator><dc:creator>Ghulam Mustafa</dc:creator><dc:creator>Azin Nahvijou</dc:creator><dc:creator>Farid Najafi</dc:creator><dc:creator>Vinay Nangia</dc:creator><dc:creator>Duduzile Edith Ndwandwe</dc:creator><dc:creator>Ionut Negoi</dc:creator><dc:creator>Ruxandra Irina Negoi</dc:creator><dc:creator>Josephine W. Ngunjiri</dc:creator><dc:creator>Cuong Tat Nguyen</dc:creator><dc:creator>Long Hoang Nguyen</dc:creator><dc:creator>Dina Nur Anggraini Ningrum</dc:creator><dc:creator>Jean Jacques Noubiap</dc:creator><dc:creator>Malihe Nourollahpour Shiadeh</dc:creator><dc:creator>Peter S. Nyasulu</dc:creator><dc:creator>Felix Akpojene Ogbo</dc:creator><dc:creator>Andrew T. Olagunju</dc:creator><dc:creator>Bolajoko Olubukunola Olusanya</dc:creator><dc:creator>Jacob Olusegun Olusanya</dc:creator><dc:creator>Obinna E. Onwujekwe</dc:creator><dc:creator>Doris D. V. Ortega-Altamirano</dc:creator><dc:creator>Eduardo Ortiz-Panozo</dc:creator><dc:creator>Simon Øverland</dc:creator><dc:creator>Mahesh P. A.</dc:creator><dc:creator>Adrian Pana</dc:creator><dc:creator>Songhomitra Panda-Jonas</dc:creator><dc:creator>Sanghamitra Pati</dc:creator><dc:creator>George C. Patton</dc:creator><dc:creator>Norberto Perico</dc:creator><dc:creator>David M. Pigott</dc:creator><dc:creator>Meghdad Pirsaheb</dc:creator><dc:creator>Maarten J. Postma</dc:creator><dc:creator>Akram Pourshams</dc:creator><dc:creator>Swayam Prakash</dc:creator><dc:creator>Parul Puri</dc:creator><dc:creator>Mostafa Qorbani</dc:creator><dc:creator>Amir Radfar</dc:creator><dc:creator>Fakher Rahim</dc:creator><dc:creator>Vafa Rahimi-Movaghar</dc:creator><dc:creator>Mohammad Hifz Ur Rahman</dc:creator><dc:creator>Fatemeh Rajati</dc:creator><dc:creator>Chhabi Lal Ranabhat</dc:creator><dc:creator>David Laith Rawaf</dc:creator><dc:creator>Salman Rawaf</dc:creator><dc:creator>Robert C. Reiner Jr</dc:creator><dc:creator>Giuseppe Remuzzi</dc:creator><dc:creator>Andre M. N. Renzaho</dc:creator><dc:creator>Satar Rezaei</dc:creator><dc:creator>Aziz Rezapour</dc:creator><dc:creator>Carlos Rios-González</dc:creator><dc:creator>Leonardo Roever</dc:creator><dc:creator>Luca Ronfani</dc:creator><dc:creator>Gholamreza Roshandel</dc:creator><dc:creator>Ali Rostami</dc:creator><dc:creator>Enrico Rubagotti</dc:creator><dc:creator>Nafis Sadat</dc:creator><dc:creator>Ehsan Sadeghi</dc:creator><dc:creator>Yahya Safari</dc:creator><dc:creator>Rajesh Sagar</dc:creator><dc:creator>Nasir Salam</dc:creator><dc:creator>Payman Salamati</dc:creator><dc:creator>Yahya Salimi</dc:creator><dc:creator>Hamideh Salimzadeh</dc:creator><dc:creator>Abdallah M. Samy</dc:creator><dc:creator>Juan Sanabria</dc:creator><dc:creator>Milena M. Santric Milicevic</dc:creator><dc:creator>Benn Sartorius</dc:creator><dc:creator>Brijesh Sathian</dc:creator><dc:creator>Arundhati R. Sawant</dc:creator><dc:creator>Lauren E. Schaeffer</dc:creator><dc:creator>Megan F. Schipp</dc:creator><dc:creator>David C. Schwebel</dc:creator><dc:creator>Anbissa Muleta Senbeta</dc:creator><dc:creator>Sadaf G. Sepanlou</dc:creator><dc:creator>Masood Ali Shaikh</dc:creator><dc:creator>Mehran Shams-Beyranvand</dc:creator><dc:creator>Morteza Shamsizadeh</dc:creator><dc:creator>Kiomars Sharafi</dc:creator><dc:creator>Rajesh Sharma</dc:creator><dc:creator>Jun She</dc:creator><dc:creator>Aziz Sheikh</dc:creator><dc:creator>Mika Shigematsu</dc:creator><dc:creator>Soraya Siabani</dc:creator><dc:creator>Dayane Gabriele Alves Silveira</dc:creator><dc:creator>Jasvinder A. Singh</dc:creator><dc:creator>Dhirendra Narain Sinha</dc:creator><dc:creator>Vegard Skirbekk</dc:creator><dc:creator>Amber Sligar</dc:creator><dc:creator>Badr Hasan Sobaih</dc:creator><dc:creator>Moslem Soofi</dc:creator><dc:creator>Joan B. Soriano</dc:creator><dc:creator>Ireneous N. Soyiri</dc:creator><dc:creator>Chandrashekhar T. Sreeramareddy</dc:creator><dc:creator>Agus Sudaryanto</dc:creator><dc:creator>Mu’awiyyah Babale Sufiyan</dc:creator><dc:creator>Ipsita Sutradhar</dc:creator><dc:creator>PN Sylaja</dc:creator><dc:creator>Rafael Tabarés-Seisdedos</dc:creator><dc:creator>Birkneh Tilahun Tadesse</dc:creator><dc:creator>Mohamad-Hani Temsah</dc:creator><dc:creator>Abdullah Sulieman Terkawi</dc:creator><dc:creator>Belay Tessema</dc:creator><dc:creator>Zemenu Tadesse Tessema</dc:creator><dc:creator>Kavumpurathu Raman Thankappan</dc:creator><dc:creator>Roman Topor-Madry</dc:creator><dc:creator>Marcos Roberto Tovani-Palone</dc:creator><dc:creator>Bach Xuan Tran</dc:creator><dc:creator>Lorainne Tudor Car</dc:creator><dc:creator>Irfan Ullah</dc:creator><dc:creator>Olalekan A. Uthman</dc:creator><dc:creator>Pascual R. Valdez</dc:creator><dc:creator>Yousef Veisani</dc:creator><dc:creator>Francesco S. Violante</dc:creator><dc:creator>Vasily Vlassov</dc:creator><dc:creator>Sebastian Vollmer</dc:creator><dc:creator>Giang Thu Vu</dc:creator><dc:creator>Yasir Waheed</dc:creator><dc:creator>Yuan-Pang Wang</dc:creator><dc:creator>John C. Wilkinson</dc:creator><dc:creator>Andrea Sylvia Winkler</dc:creator><dc:creator>Charles D. A. Wolfe</dc:creator><dc:creator>Tomohide Yamada</dc:creator><dc:creator>Alex Yeshaneh</dc:creator><dc:creator>Paul Yip</dc:creator><dc:creator>Engida Yisma</dc:creator><dc:creator>Naohiro Yonemoto</dc:creator><dc:creator>Mustafa Z. Younis</dc:creator><dc:creator>Mahmoud Yousefifard</dc:creator><dc:creator>Chuanhua Yu</dc:creator><dc:creator>Sojib Bin Zaman</dc:creator><dc:creator>Jianrong Zhang</dc:creator><dc:creator>Yunquan Zhang</dc:creator><dc:creator>Sanjay Zodpey</dc:creator><dc:creator>Emmanuela Gakidou</dc:creator><dc:creator>Simon I. Hay</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1872-1</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-25; | doi:10.1038/s41586-019-1872-1</dc:source>
            <dc:date>2019-12-25</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1872-1</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1872-1</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03938-x"">
            <title><![CDATA[How one astronomer hears the Universe]]></title>
            <link>https://www.nature.com/articles/d41586-019-03938-x</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 24 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03938-x"">doi:10.1038/d41586-019-03938-x</a></p>Wanda Diaz Merced is a blind astronomer who says that converting astronomical data into sound could bring discoveries that conventional techniques miss.]]></content:encoded>
            <dc:title><![CDATA[How one astronomer hears the Universe]]></dc:title>
            <dc:creator>Elizabeth Gibney</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03938-x</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-24; | doi:10.1038/d41586-019-03938-x</dc:source>
            <dc:date>2019-12-24</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03938-x</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03938-x</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03900-x"">
            <title><![CDATA[Podcast Extra: From climate lawyer to climate activist]]></title>
            <link>https://www.nature.com/articles/d41586-019-03900-x</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 23 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03900-x"">doi:10.1038/d41586-019-03900-x</a></p>After three decades of climate advocacy, renowned IPCC lawyer Farhana Yamin decided to join Extinction Rebellion – she tells us why.]]></content:encoded>
            <dc:title><![CDATA[Podcast Extra: From climate lawyer to climate activist]]></dc:title>
            
            <dc:identifier>doi:10.1038/d41586-019-03900-x</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-23; | doi:10.1038/d41586-019-03900-x</dc:source>
            <dc:date>2019-12-23</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03900-x</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03900-x</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03929-y"">
            <title><![CDATA[When Indian households gain electricity, one gender benefits more]]></title>
            <link>https://www.nature.com/articles/d41586-019-03929-y</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 23 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03929-y"">doi:10.1038/d41586-019-03929-y</a></p>A power imbalance can undercut the advantages that women might reap from access to electricity.]]></content:encoded>
            <dc:title><![CDATA[When Indian households gain electricity, one gender benefits more]]></dc:title>
            
            <dc:identifier>doi:10.1038/d41586-019-03929-y</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-23; | doi:10.1038/d41586-019-03929-y</dc:source>
            <dc:date>2019-12-23</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03929-y</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03929-y</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03906-5"">
            <title><![CDATA[Australian biobank repatriates hundreds of ‘legacy’ Indigenous blood samples]]></title>
            <link>https://www.nature.com/articles/d41586-019-03906-5</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 23 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03906-5"">doi:10.1038/d41586-019-03906-5</a></p>The return is part of a groundbreaking approach that could inspire other institutions grappling with how to use historical samples ethically in research.]]></content:encoded>
            <dc:title><![CDATA[Australian biobank repatriates hundreds of ‘legacy’ Indigenous blood samples]]></dc:title>
            <dc:creator>Dyani Lewis</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03906-5</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-23; | doi:10.1038/d41586-019-03906-5</dc:source>
            <dc:date>2019-12-23</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03906-5</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03906-5</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03883-9"">
            <title><![CDATA[Maya ruins named as ‘missing’ capital of the White Dog kingdom]]></title>
            <link>https://www.nature.com/articles/d41586-019-03883-9</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 20 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03883-9"">doi:10.1038/d41586-019-03883-9</a></p>Archaeologists link a site in southern Mexico to a royal centre long known from Mayan inscriptions.]]></content:encoded>
            <dc:title><![CDATA[Maya ruins named as ‘missing’ capital of the White Dog kingdom]]></dc:title>
            
            <dc:identifier>doi:10.1038/d41586-019-03883-9</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-20; | doi:10.1038/d41586-019-03883-9</dc:source>
            <dc:date>2019-12-20</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03883-9</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03883-9</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03926-1"">
            <title><![CDATA[Rumours fly about changes to US government open-access policy]]></title>
            <link>https://www.nature.com/articles/d41586-019-03926-1</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 20 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03926-1"">doi:10.1038/d41586-019-03926-1</a></p>The White House is said to be preparing a policy that would change how government-funded research is disseminated.]]></content:encoded>
            <dc:title><![CDATA[Rumours fly about changes to US government open-access policy]]></dc:title>
            <dc:creator>Nidhi Subbaraman</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03926-1</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-20; | doi:10.1038/d41586-019-03926-1</dc:source>
            <dc:date>2019-12-20</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03926-1</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03926-1</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03914-5"">
            <title><![CDATA[Secrets to writing a winning grant]]></title>
            <link>https://www.nature.com/articles/d41586-019-03914-5</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 20 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03914-5"">doi:10.1038/d41586-019-03914-5</a></p>Experienced scientists reveal how to avoid application pitfalls to submit successful proposals.]]></content:encoded>
            <dc:title><![CDATA[Secrets to writing a winning grant]]></dc:title>
            <dc:creator>Emily Sohn</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03914-5</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-20; | doi:10.1038/d41586-019-03914-5</dc:source>
            <dc:date>2019-12-20</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03914-5</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03914-5</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03886-6"">
            <title><![CDATA[Mini-factory churns out drug cheaply and at a furious pace]]></title>
            <link>https://www.nature.com/articles/d41586-019-03886-6</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 20 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03886-6"">doi:10.1038/d41586-019-03886-6</a></p>A small, fully automated assembly line can produce 4,800 tablets an hour and could make drugs more widely available.]]></content:encoded>
            <dc:title><![CDATA[Mini-factory churns out drug cheaply and at a furious pace]]></dc:title>
            
            <dc:identifier>doi:10.1038/d41586-019-03886-6</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-20; | doi:10.1038/d41586-019-03886-6</dc:source>
            <dc:date>2019-12-20</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03886-6</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03886-6</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03945-y"">
            <title><![CDATA[Daily briefing: Oldest fossil trees rewrite the history of forests]]></title>
            <link>https://www.nature.com/articles/d41586-019-03945-y</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 20 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03945-y"">doi:10.1038/d41586-019-03945-y</a></p>“Spectacularly extensive root systems” show surprising complexity, Science’s biggest breakthrough of the year and death to ‘data upon request’.]]></content:encoded>
            <dc:title><![CDATA[Daily briefing: Oldest fossil trees rewrite the history of forests]]></dc:title>
            <dc:creator>Flora Graham</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03945-y</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-20; | doi:10.1038/d41586-019-03945-y</dc:source>
            <dc:date>2019-12-20</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03945-y</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03945-y</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03877-7"">
            <title><![CDATA[Podcast Extra: Epigenetics]]></title>
            <link>https://www.nature.com/articles/d41586-019-03877-7</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 20 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03877-7"">doi:10.1038/d41586-019-03877-7</a></p>As part of Nature's 150th anniversary celebrations, Nick Howe dives into the topic of epigenetics.]]></content:encoded>
            <dc:title><![CDATA[Podcast Extra: Epigenetics]]></dc:title>
            
            <dc:identifier>doi:10.1038/d41586-019-03877-7</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-20; | doi:10.1038/d41586-019-03877-7</dc:source>
            <dc:date>2019-12-20</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03877-7</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03877-7</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03912-7"">
            <title><![CDATA[2020 in science and culture: <i>Nature</i>’s pick of the listings]]></title>
            <link>https://www.nature.com/articles/d41586-019-03912-7</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 20 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03912-7"">doi:10.1038/d41586-019-03912-7</a></p>The new uncanny valley, 50 years of Earth Day and the troubled future of elephants. Nicola Jones reports on what’s coming in 2020.]]></content:encoded>
            <dc:title><![CDATA[2020 in science and culture: <i>Nature</i>’s pick of the listings]]></dc:title>
            <dc:creator>Nicola Jones</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03912-7</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-20; | doi:10.1038/d41586-019-03912-7</dc:source>
            <dc:date>2019-12-20</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03912-7</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03912-7</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03899-1"">
            <title><![CDATA[Low-carbon, virtual science conference tries to recreate social buzz]]></title>
            <link>https://www.nature.com/articles/d41586-019-03899-1</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 20 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03899-1"">doi:10.1038/d41586-019-03899-1</a></p>The organizers of an international biology meeting asked psychologists to assess their attempts at retaining the advantages of traditional conferences.]]></content:encoded>
            <dc:title><![CDATA[Low-carbon, virtual science conference tries to recreate social buzz]]></dc:title>
            <dc:creator>Alison Abbott</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03899-1</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-20; | doi:10.1038/d41586-019-03899-1</dc:source>
            <dc:date>2019-12-20</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03899-1</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03899-1</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03948-9"">
            <title><![CDATA[What I learnt from trying out industry before graduate school]]></title>
            <link>https://www.nature.com/articles/d41586-019-03948-9</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 20 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03948-9"">doi:10.1038/d41586-019-03948-9</a></p>Ty Tang was told that any move out of academia was likely to be permanent. But after two years working, he has now returned to research.]]></content:encoded>
            <dc:title><![CDATA[What I learnt from trying out industry before graduate school]]></dc:title>
            <dc:creator>Ty Tang</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03948-9</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-20; | doi:10.1038/d41586-019-03948-9</dc:source>
            <dc:date>2019-12-20</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03948-9</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03948-9</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03910-9"">
            <title><![CDATA[The science events to watch for in 2020]]></title>
            <link>https://www.nature.com/articles/d41586-019-03910-9</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 20 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03910-9"">doi:10.1038/d41586-019-03910-9</a></p>A Mars invasion, a climate meeting and human–animal hybrids are set to shape the research agenda.]]></content:encoded>
            <dc:title><![CDATA[The science events to watch for in 2020]]></dc:title>
            <dc:creator>Davide Castelvecchi</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03910-9</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-20; | doi:10.1038/d41586-019-03910-9</dc:source>
            <dc:date>2019-12-20</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03910-9</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03910-9</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03873-x"">
            <title><![CDATA[Early-career funding sources: you will not find what you do not seek]]></title>
            <link>https://www.nature.com/articles/d41586-019-03873-x</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 20 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03873-x"">doi:10.1038/d41586-019-03873-x</a></p>Colin Evans adopted four principles to secure funding after his first postdoctoral fellowship application was unsuccessful.]]></content:encoded>
            <dc:title><![CDATA[Early-career funding sources: you will not find what you do not seek]]></dc:title>
            <dc:creator>Colin Evans</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03873-x</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-20; | doi:10.1038/d41586-019-03873-x</dc:source>
            <dc:date>2019-12-20</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03873-x</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03873-x</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03932-3"">
            <title><![CDATA[Head of ancient-DNA lab sacked for ‘serious misconduct’]]></title>
            <link>https://www.nature.com/articles/d41586-019-03932-3</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 20 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03932-3"">doi:10.1038/d41586-019-03932-3</a></p>Alan Cooper was dismissed as the leader of a prestigious genomics centre, following an investigation.]]></content:encoded>
            <dc:title><![CDATA[Head of ancient-DNA lab sacked for ‘serious misconduct’]]></dc:title>
            <dc:creator>Dyani Lewis</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03932-3</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-20; | doi:10.1038/d41586-019-03932-3</dc:source>
            <dc:date>2019-12-20</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03932-3</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03932-3</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03885-7"">
            <title><![CDATA[Bridges in the sky help slow lorises keep to the trees]]></title>
            <link>https://www.nature.com/articles/d41586-019-03885-7</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 20 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03885-7"">doi:10.1038/d41586-019-03885-7</a></p>Endangered primate cannot leap between isolated clumps of rainforest but can easily navigate artificial walkways.]]></content:encoded>
            <dc:title><![CDATA[Bridges in the sky help slow lorises keep to the trees]]></dc:title>
            
            <dc:identifier>doi:10.1038/d41586-019-03885-7</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-20; | doi:10.1038/d41586-019-03885-7</dc:source>
            <dc:date>2019-12-20</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03885-7</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03885-7</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03924-3"">
            <title><![CDATA[Trump picks computer scientist to lead National Science Foundation]]></title>
            <link>https://www.nature.com/articles/d41586-019-03924-3</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 19 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03924-3"">doi:10.1038/d41586-019-03924-3</a></p>White House also chooses atmospheric scientist to lead the National Oceanic and Atmospheric Administration.]]></content:encoded>
            <dc:title><![CDATA[Trump picks computer scientist to lead National Science Foundation]]></dc:title>
            <dc:creator>Alexandra Witze</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03924-3</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-19; | doi:10.1038/d41586-019-03924-3</dc:source>
            <dc:date>2019-12-19</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03924-3</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03924-3</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03927-0"">
            <title><![CDATA[Daily briefing: The countries where scientists are most likely to work through the holidays]]></title>
            <link>https://www.nature.com/articles/d41586-019-03927-0</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 19 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03927-0"">doi:10.1038/d41586-019-03927-0</a></p>Scientists in Belgium, Japan and the United States are submitting when the rest of us are kipping. Plus: Finally! A solution (of sorts) to the three-body problem, and the conference that brings all the fun of science Twitter into the real world.]]></content:encoded>
            <dc:title><![CDATA[Daily briefing: The countries where scientists are most likely to work through the holidays]]></dc:title>
            <dc:creator>Flora Graham</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03927-0</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-19; | doi:10.1038/d41586-019-03927-0</dc:source>
            <dc:date>2019-12-19</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03927-0</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03927-0</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1896-6"">
            <title><![CDATA[Insights into the assembly and activation of the microtubule nucleator γ-TuRC]]></title>
            <link>https://www.nature.com/articles/s41586-019-1896-6</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 19 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1896-6"">doi:10.1038/s41586-019-1896-6</a></p>Insights into the assembly and activation of the microtubule nucleator γ-TuRC]]></content:encoded>
            <dc:title><![CDATA[Insights into the assembly and activation of the microtubule nucleator γ-TuRC]]></dc:title>
            <dc:creator>Peng Liu</dc:creator><dc:creator>Erik Zupa</dc:creator><dc:creator>Annett Neuner</dc:creator><dc:creator>Anna Böhler</dc:creator><dc:creator>Justus Loerke</dc:creator><dc:creator>Dirk Flemming</dc:creator><dc:creator>Thomas Ruppert</dc:creator><dc:creator>Till Rudack</dc:creator><dc:creator>Christoph Peter</dc:creator><dc:creator>Christian Spahn</dc:creator><dc:creator>Oliver J. Gruss</dc:creator><dc:creator>Stefan Pfeffer</dc:creator><dc:creator>Elmar Schiebel</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1896-6</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-19; | doi:10.1038/s41586-019-1896-6</dc:source>
            <dc:date>2019-12-19</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1896-6</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1896-6</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03890-w"">
            <title><![CDATA[Go figure: salary drives researchers to move to new countries]]></title>
            <link>https://www.nature.com/articles/d41586-019-03890-w</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 19 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03890-w"">doi:10.1038/d41586-019-03890-w</a></p>More than two-thirds of scientists polled in a survey say that they would move for the ‘right’ academic job — if the pay suits.]]></content:encoded>
            <dc:title><![CDATA[Go figure: salary drives researchers to move to new countries]]></dc:title>
            <dc:creator>Chris Woolston</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03890-w</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-19; | doi:10.1038/d41586-019-03890-w</dc:source>
            <dc:date>2019-12-19</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03890-w</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03890-w</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03895-5"">
            <title><![CDATA[This AI researcher is trying to ward off a reproducibility crisis]]></title>
            <link>https://www.nature.com/articles/d41586-019-03895-5</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 19 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03895-5"">doi:10.1038/d41586-019-03895-5</a></p>Joelle Pineau is leading an effort to encourage artificial-intelligence researchers to open up their code.]]></content:encoded>
            <dc:title><![CDATA[This AI researcher is trying to ward off a reproducibility crisis]]></dc:title>
            <dc:creator>Elizabeth Gibney</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03895-5</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-19; | doi:10.1038/d41586-019-03895-5</dc:source>
            <dc:date>2019-12-19</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03895-5</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03895-5</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1856-1"">
            <title><![CDATA[Frequent mutations that converge on the NFKBIZ pathway in ulcerative colitis]]></title>
            <link>https://www.nature.com/articles/s41586-019-1856-1</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1856-1"">doi:10.1038/s41586-019-1856-1</a></p>In patients with ulcerative colitis, chronic inflammation can lead to remodelling of the colorectal epithelium through positive selection of clones with mutations in genes related to IL-17 signalling, which, however, might be negatively selected during colitis-associated carcinogenesis.]]></content:encoded>
            <dc:title><![CDATA[Frequent mutations that converge on the NFKBIZ pathway in ulcerative colitis]]></dc:title>
            <dc:creator>Nobuyuki Kakiuchi</dc:creator><dc:creator>Kenichi Yoshida</dc:creator><dc:creator>Motoi Uchino</dc:creator><dc:creator>Takako Kihara</dc:creator><dc:creator>Kotaro Akaki</dc:creator><dc:creator>Yoshikage Inoue</dc:creator><dc:creator>Kenji Kawada</dc:creator><dc:creator>Satoshi Nagayama</dc:creator><dc:creator>Akira Yokoyama</dc:creator><dc:creator>Shuji Yamamoto</dc:creator><dc:creator>Minoru Matsuura</dc:creator><dc:creator>Takahiro Horimatsu</dc:creator><dc:creator>Tomonori Hirano</dc:creator><dc:creator>Norihiro Goto</dc:creator><dc:creator>Yasuhide Takeuchi</dc:creator><dc:creator>Yotaro Ochi</dc:creator><dc:creator>Yusuke Shiozawa</dc:creator><dc:creator>Yasunori Kogure</dc:creator><dc:creator>Yosaku Watatani</dc:creator><dc:creator>Yoichi Fujii</dc:creator><dc:creator>Soo Ki Kim</dc:creator><dc:creator>Ayana Kon</dc:creator><dc:creator>Keisuke Kataoka</dc:creator><dc:creator>Tetsuichi Yoshizato</dc:creator><dc:creator>Masahiro M. Nakagawa</dc:creator><dc:creator>Akinori Yoda</dc:creator><dc:creator>Yasuhito Nanya</dc:creator><dc:creator>Hideki Makishima</dc:creator><dc:creator>Yuichi Shiraishi</dc:creator><dc:creator>Kenichi Chiba</dc:creator><dc:creator>Hiroko Tanaka</dc:creator><dc:creator>Masashi Sanada</dc:creator><dc:creator>Eiji Sugihara</dc:creator><dc:creator>Taka-aki Sato</dc:creator><dc:creator>Takashi Maruyama</dc:creator><dc:creator>Hiroyuki Miyoshi</dc:creator><dc:creator>Makoto Mark Taketo</dc:creator><dc:creator>Jun Oishi</dc:creator><dc:creator>Ryosaku Inagaki</dc:creator><dc:creator>Yutaka Ueda</dc:creator><dc:creator>Shinya Okamoto</dc:creator><dc:creator>Hideaki Okajima</dc:creator><dc:creator>Yoshiharu Sakai</dc:creator><dc:creator>Takaki Sakurai</dc:creator><dc:creator>Hironori Haga</dc:creator><dc:creator>Seiichi Hirota</dc:creator><dc:creator>Hiroki Ikeuchi</dc:creator><dc:creator>Hiroshi Nakase</dc:creator><dc:creator>Hiroyuki Marusawa</dc:creator><dc:creator>Tsutomu Chiba</dc:creator><dc:creator>Osamu Takeuchi</dc:creator><dc:creator>Satoru Miyano</dc:creator><dc:creator>Hiroshi Seno</dc:creator><dc:creator>Seishi Ogawa</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1856-1</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1856-1</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1856-1</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1856-1</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03846-0"">
            <title><![CDATA[Hunting for New Drugs with AI]]></title>
            <link>https://www.nature.com/articles/d41586-019-03846-0</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03846-0"">doi:10.1038/d41586-019-03846-0</a></p>The pharmaceutical industry is in a drug-discovery slump. How much can AI help?]]></content:encoded>
            <dc:title><![CDATA[Hunting for New Drugs with AI]]></dc:title>
            <dc:creator>David H. Freedman</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03846-0</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03846-0</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03846-0</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03846-0</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03603-3"">
            <title><![CDATA[Long-term predator–prey cycles finally achieved in the lab]]></title>
            <link>https://www.nature.com/articles/d41586-019-03603-3</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03603-3"">doi:10.1038/d41586-019-03603-3</a></p>A combination of laboratory experiments and mathematical and statistical analysis provides an affirmative answer to a decades-old question — can a predator and its prey coexist indefinitely?]]></content:encoded>
            <dc:title><![CDATA[Long-term predator–prey cycles finally achieved in the lab]]></dc:title>
            <dc:creator>Alan Hastings</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03603-3</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03603-3</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03603-3</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03603-3</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1844-5"">
            <title><![CDATA[Somatic inflammatory gene mutations in human ulcerative colitis epithelium]]></title>
            <link>https://www.nature.com/articles/s41586-019-1844-5</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1844-5"">doi:10.1038/s41586-019-1844-5</a></p>Whole-exome sequencing of colon organoids derived from patients with ulcerative colitis identifies somatic mutations in components of the IL-17 signalling pathway, which may confer a growth advantage to cells under inflammatory conditions.]]></content:encoded>
            <dc:title><![CDATA[Somatic inflammatory gene mutations in human ulcerative colitis epithelium]]></dc:title>
            <dc:creator>Kosaku Nanki</dc:creator><dc:creator>Masayuki Fujii</dc:creator><dc:creator>Mariko Shimokawa</dc:creator><dc:creator>Mami Matano</dc:creator><dc:creator>Shingo Nishikori</dc:creator><dc:creator>Shoichi Date</dc:creator><dc:creator>Ai Takano</dc:creator><dc:creator>Kohta Toshimitsu</dc:creator><dc:creator>Yuki Ohta</dc:creator><dc:creator>Sirirat Takahashi</dc:creator><dc:creator>Shinya Sugimoto</dc:creator><dc:creator>Kazuhiro Ishimaru</dc:creator><dc:creator>Kenta Kawasaki</dc:creator><dc:creator>Yoko Nagai</dc:creator><dc:creator>Ryota Ishii</dc:creator><dc:creator>Kosuke Yoshida</dc:creator><dc:creator>Nobuo Sasaki</dc:creator><dc:creator>Toshifumi Hibi</dc:creator><dc:creator>Soichiro Ishihara</dc:creator><dc:creator>Takanori Kanai</dc:creator><dc:creator>Toshiro Sato</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1844-5</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1844-5</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1844-5</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1844-5</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1863-2"">
            <title><![CDATA[Last appearance of <i>Homo erectus</i> at Ngandong, Java, 117,000–108,000 years ago]]></title>
            <link>https://www.nature.com/articles/s41586-019-1863-2</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1863-2"">doi:10.1038/s41586-019-1863-2</a></p>Bayesian modelling of radiometric age estimates provides a robust chronology for Homo erectus at Ngandong (Java), confirming that this site currently represents the last known occurrence of this species.]]></content:encoded>
            <dc:title><![CDATA[Last appearance of <i>Homo erectus</i> at Ngandong, Java, 117,000–108,000 years ago]]></dc:title>
            <dc:creator>Yan Rizal</dc:creator><dc:creator>Kira E. Westaway</dc:creator><dc:creator>Yahdi Zaim</dc:creator><dc:creator>Gerrit D. van den Bergh</dc:creator><dc:creator>E. Arthur Bettis III</dc:creator><dc:creator>Michael J. Morwood</dc:creator><dc:creator>O. Frank Huffman</dc:creator><dc:creator>Rainer Grün</dc:creator><dc:creator>Renaud Joannes-Boyau</dc:creator><dc:creator>Richard M. Bailey</dc:creator><dc:creator> Sidarto</dc:creator><dc:creator>Michael C. Westaway</dc:creator><dc:creator>Iwan Kurniawan</dc:creator><dc:creator>Mark W. Moore</dc:creator><dc:creator>Michael Storey</dc:creator><dc:creator>Fachroel Aziz</dc:creator><dc:creator> Suminto</dc:creator><dc:creator>Jian-xin Zhao</dc:creator><dc:creator> Aswan</dc:creator><dc:creator>Maija E. Sipola</dc:creator><dc:creator>Roy Larick</dc:creator><dc:creator>John-Paul Zonneveld</dc:creator><dc:creator>Robert Scott</dc:creator><dc:creator>Shelby Putt</dc:creator><dc:creator>Russell L. Ciochon</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1863-2</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1863-2</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1863-2</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1863-2</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03853-1"">
            <title><![CDATA[From social media to conference social]]></title>
            <link>https://www.nature.com/articles/d41586-019-03853-1</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03853-1"">doi:10.1038/d41586-019-03853-1</a></p>Molecular biologist Oded Rechavi explains his idea for bringing the Twitter science community together at ‘the Woodstock of Biology’.]]></content:encoded>
            <dc:title><![CDATA[From social media to conference social]]></dc:title>
            <dc:creator>Josie Glausiusz</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03853-1</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03853-1</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03853-1</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03853-1</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1851-6"">
            <title><![CDATA[Localization and delocalization of light in photonic moiré lattices]]></title>
            <link>https://www.nature.com/articles/s41586-019-1851-6</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1851-6"">doi:10.1038/s41586-019-1851-6</a></p>A superposition of tunable photonic lattices is used to create optical moiré patterns and demonstrate the resulting localization of light waves through a mechanism based on flat-band physics.]]></content:encoded>
            <dc:title><![CDATA[Localization and delocalization of light in photonic moiré lattices]]></dc:title>
            <dc:creator>Peng Wang</dc:creator><dc:creator>Yuanlin Zheng</dc:creator><dc:creator>Xianfeng Chen</dc:creator><dc:creator>Changming Huang</dc:creator><dc:creator>Yaroslav V. Kartashov</dc:creator><dc:creator>Lluis Torner</dc:creator><dc:creator>Vladimir V. Konotop</dc:creator><dc:creator>Fangwei Ye</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1851-6</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1851-6</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1851-6</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1851-6</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1809-8"">
            <title><![CDATA[Molecular heterogeneity drives reconfigurable nematic liquid crystal drops]]></title>
            <link>https://www.nature.com/articles/s41586-019-1809-8</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1809-8"">doi:10.1038/s41586-019-1809-8</a></p>Study of droplets containing nematic liquid crystal oligomers shows that a heterogeneous distribution of chain lengths plays a key part in driving reversible shape transformations with cooling and heating.]]></content:encoded>
            <dc:title><![CDATA[Molecular heterogeneity drives reconfigurable nematic liquid crystal drops]]></dc:title>
            <dc:creator>Wei-Shao Wei</dc:creator><dc:creator>Yu Xia</dc:creator><dc:creator>Sophie Ettinger</dc:creator><dc:creator>Shu Yang</dc:creator><dc:creator>A. G. Yodh</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1809-8</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1809-8</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1809-8</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1809-8</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1847-2"">
            <title><![CDATA[Metabolic heterogeneity confers differences in melanoma metastatic potential]]></title>
            <link>https://www.nature.com/articles/s41586-019-1847-2</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1847-2"">doi:10.1038/s41586-019-1847-2</a></p>Differences in MCT1 function among melanoma cells confer differences in oxidative stress resistance and metastatic potential.]]></content:encoded>
            <dc:title><![CDATA[Metabolic heterogeneity confers differences in melanoma metastatic potential]]></dc:title>
            <dc:creator>Alpaslan Tasdogan</dc:creator><dc:creator>Brandon Faubert</dc:creator><dc:creator>Vijayashree Ramesh</dc:creator><dc:creator>Jessalyn M. Ubellacker</dc:creator><dc:creator>Bo Shen</dc:creator><dc:creator>Ashley Solmonson</dc:creator><dc:creator>Malea M. Murphy</dc:creator><dc:creator>Zhimin Gu</dc:creator><dc:creator>Wen Gu</dc:creator><dc:creator>Misty Martin</dc:creator><dc:creator>Stacy Y. Kasitinon</dc:creator><dc:creator>Travis Vandergriff</dc:creator><dc:creator>Thomas P. Mathews</dc:creator><dc:creator>Zhiyu Zhao</dc:creator><dc:creator>Dirk Schadendorf</dc:creator><dc:creator>Ralph J. DeBerardinis</dc:creator><dc:creator>Sean J. Morrison</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1847-2</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1847-2</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1847-2</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1847-2</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03837-1"">
            <title><![CDATA[Thousand-author papers, lab outbreaks and a prisoner exchange]]></title>
            <link>https://www.nature.com/articles/d41586-019-03837-1</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03837-1"">doi:10.1038/d41586-019-03837-1</a></p>The latest science news, in brief.]]></content:encoded>
            <dc:title><![CDATA[Thousand-author papers, lab outbreaks and a prisoner exchange]]></dc:title>
            
            <dc:identifier>doi:10.1038/d41586-019-03837-1</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03837-1</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03837-1</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03837-1</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03820-w"">
            <title><![CDATA[Sometimes you end up where you are]]></title>
            <link>https://www.nature.com/articles/d41586-019-03820-w</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03820-w"">doi:10.1038/d41586-019-03820-w</a></p>A trip back home for Christmas.]]></content:encoded>
            <dc:title><![CDATA[Sometimes you end up where you are]]></dc:title>
            <dc:creator>Beth Cato</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03820-w</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03820-w</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03820-w</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03820-w</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03844-2"">
            <title><![CDATA[What should Peru do to improve its science?]]></title>
            <link>https://www.nature.com/articles/d41586-019-03844-2</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03844-2"">doi:10.1038/d41586-019-03844-2</a></p>Scientists say the country has many home advantages for good research, but it desperately needs more government interest.]]></content:encoded>
            <dc:title><![CDATA[What should Peru do to improve its science?]]></dc:title>
            <dc:creator>Aleszu Bajak</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03844-2</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03844-2</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03844-2</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03844-2</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03896-4"">
            <title><![CDATA[Daily briefing: Why is ice so slippery?]]></title>
            <link>https://www.nature.com/articles/d41586-019-03896-4</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03896-4"">doi:10.1038/d41586-019-03896-4</a></p>Hint: It’s not all about the water. Plus, Nature’s hotly anticipated annual list of the ten people who mattered in science this year.]]></content:encoded>
            <dc:title><![CDATA[Daily briefing: Why is ice so slippery?]]></dc:title>
            
            <dc:identifier>doi:10.1038/d41586-019-03896-4</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03896-4</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03896-4</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03896-4</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1842-7"">
            <title><![CDATA[Impaired cell fate through gain-of-function mutations in a chromatin reader]]></title>
            <link>https://www.nature.com/articles/s41586-019-1842-7</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1842-7"">doi:10.1038/s41586-019-1842-7</a></p>The histone-acetylation-reader protein ENL is mutated in a paediatric kidney cancer in such a way that it clusters at target genes, increasing the recruitment of the transcriptional machinery, enhancing transcription and deregulating cell fate during development.]]></content:encoded>
            <dc:title><![CDATA[Impaired cell fate through gain-of-function mutations in a chromatin reader]]></dc:title>
            <dc:creator>Liling Wan</dc:creator><dc:creator>Shasha Chong</dc:creator><dc:creator>Fan Xuan</dc:creator><dc:creator>Angela Liang</dc:creator><dc:creator>Xiaodong Cui</dc:creator><dc:creator>Leah Gates</dc:creator><dc:creator>Thomas S. Carroll</dc:creator><dc:creator>Yuanyuan Li</dc:creator><dc:creator>Lijuan Feng</dc:creator><dc:creator>Guochao Chen</dc:creator><dc:creator>Shu-Ping Wang</dc:creator><dc:creator>Michael V. Ortiz</dc:creator><dc:creator>Sara K. Daley</dc:creator><dc:creator>Xiaolu Wang</dc:creator><dc:creator>Hongwen Xuan</dc:creator><dc:creator>Alex Kentsis</dc:creator><dc:creator>Tom W. Muir</dc:creator><dc:creator>Robert G. Roeder</dc:creator><dc:creator>Haitao Li</dc:creator><dc:creator>Wei Li</dc:creator><dc:creator>Robert Tjian</dc:creator><dc:creator>Hong Wen</dc:creator><dc:creator>C. David Allis</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1842-7</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1842-7</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1842-7</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1842-7</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03849-x"">
            <title><![CDATA[Wiring Minds]]></title>
            <link>https://www.nature.com/articles/d41586-019-03849-x</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03849-x"">doi:10.1038/d41586-019-03849-x</a></p>Successfully applying AI to biomedicine requires innovators trained in contrasting cultures.]]></content:encoded>
            <dc:title><![CDATA[Wiring Minds]]></dc:title>
            <dc:creator>Amit Kaushal</dc:creator><dc:creator>Russ B. Altman</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03849-x</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03849-x</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03849-x</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03849-x</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03851-3"">
            <title><![CDATA[How the scientific meeting has changed since <i>Nature</i>’s founding 150 years ago]]></title>
            <link>https://www.nature.com/articles/d41586-019-03851-3</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03851-3"">doi:10.1038/d41586-019-03851-3</a></p>Charting the 1869 conference vibe and the challenges faced by today’s organizers.]]></content:encoded>
            <dc:title><![CDATA[How the scientific meeting has changed since <i>Nature</i>’s founding 150 years ago]]></dc:title>
            <dc:creator>Virginia Gewin</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03851-3</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03851-3</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03851-3</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03851-3</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1857-0"">
            <title><![CDATA[Long-term cyclic persistence in an experimental predator–prey system]]></title>
            <link>https://www.nature.com/articles/s41586-019-1857-0</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1857-0"">doi:10.1038/s41586-019-1857-0</a></p>The potential for infinite persistence of planktonic predator and prey cycles is experimentally demonstrated and these cycles show resilience in the presence of stochastic events.]]></content:encoded>
            <dc:title><![CDATA[Long-term cyclic persistence in an experimental predator–prey system]]></dc:title>
            <dc:creator>Bernd Blasius</dc:creator><dc:creator>Lars Rudolf</dc:creator><dc:creator>Guntram Weithoff</dc:creator><dc:creator>Ursula Gaedke</dc:creator><dc:creator>Gregor F. Fussmann</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1857-0</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1857-0</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1857-0</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1857-0</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03811-x"">
            <title><![CDATA[Brain states behind exploring and hunting revealed]]></title>
            <link>https://www.nature.com/articles/d41586-019-03811-x</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03811-x"">doi:10.1038/d41586-019-03811-x</a></p>The brain fluctuates between different internal states, each of which drives particular behaviours. Brain-wide imaging reveals the internal states that help zebrafish larvae to choose between exploring and hunting.]]></content:encoded>
            <dc:title><![CDATA[Brain states behind exploring and hunting revealed]]></dc:title>
            <dc:creator>Ethan Scott</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03811-x</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03811-x</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03811-x</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03811-x</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1826-7"">
            <title><![CDATA[Large magnetic gap at the Dirac point in Bi<sub>2</sub>Te<sub>3</sub>/MnBi<sub>2</sub>Te<sub>4</sub> heterostructures]]></title>
            <link>https://www.nature.com/articles/s41586-019-1826-7</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1826-7"">doi:10.1038/s41586-019-1826-7</a></p>In theory, the anomalous quantum Hall effect is observed in edge channels of topological insulators when there is a magnetic energy gap at the Dirac point; this gap has now been observed by low-temperature photoelectron spectroscopy in Mn-doped Bi2Te3.]]></content:encoded>
            <dc:title><![CDATA[Large magnetic gap at the Dirac point in Bi<sub>2</sub>Te<sub>3</sub>/MnBi<sub>2</sub>Te<sub>4</sub> heterostructures]]></dc:title>
            <dc:creator>E. D. L. Rienks</dc:creator><dc:creator>S. Wimmer</dc:creator><dc:creator>J. Sánchez-Barriga</dc:creator><dc:creator>O. Caha</dc:creator><dc:creator>P. S. Mandal</dc:creator><dc:creator>J. Růžička</dc:creator><dc:creator>A. Ney</dc:creator><dc:creator>H. Steiner</dc:creator><dc:creator>V. V. Volobuev</dc:creator><dc:creator>H. Groiss</dc:creator><dc:creator>M. Albu</dc:creator><dc:creator>G. Kothleitner</dc:creator><dc:creator>J. Michalička</dc:creator><dc:creator>S. A. Khan</dc:creator><dc:creator>J. Minár</dc:creator><dc:creator>H. Ebert</dc:creator><dc:creator>G. Bauer</dc:creator><dc:creator>F. Freyse</dc:creator><dc:creator>A. Varykhalov</dc:creator><dc:creator>O. Rader</dc:creator><dc:creator>G. Springholz</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1826-7</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1826-7</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1826-7</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1826-7</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03891-9"">
            <title><![CDATA[Podcast: The three-body problem, and festive fun]]></title>
            <link>https://www.nature.com/articles/d41586-019-03891-9</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03891-9"">doi:10.1038/d41586-019-03891-9</a></p>Hear this weeks’ science updates, with Benjamin Thompson and Nick Howe.]]></content:encoded>
            <dc:title><![CDATA[Podcast: The three-body problem, and festive fun]]></dc:title>
            
            <dc:identifier>doi:10.1038/d41586-019-03891-9</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03891-9</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03891-9</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03891-9</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1833-8"">
            <title><![CDATA[A statistical solution to the chaotic, non-hierarchical three-body problem]]></title>
            <link>https://www.nature.com/articles/s41586-019-1833-8</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1833-8"">doi:10.1038/s41586-019-1833-8</a></p>The ergodic hypothesis is used to produce a statistical solution to the chaotic non-hierarchical three-body problem.]]></content:encoded>
            <dc:title><![CDATA[A statistical solution to the chaotic, non-hierarchical three-body problem]]></dc:title>
            <dc:creator>Nicholas C. Stone</dc:creator><dc:creator>Nathan W. C. Leigh</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1833-8</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1833-8</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1833-8</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1833-8</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1852-5"">
            <title><![CDATA[The water lily genome and the early evolution of flowering plants]]></title>
            <link>https://www.nature.com/articles/s41586-019-1852-5</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1852-5"">doi:10.1038/s41586-019-1852-5</a></p>The genome of the tropical blue-petal water lily Nymphaea colorata and the transcriptomes from 19 other Nymphaeales species provide insights into the early evolution of angiosperms.]]></content:encoded>
            <dc:title><![CDATA[The water lily genome and the early evolution of flowering plants]]></dc:title>
            <dc:creator>Liangsheng Zhang</dc:creator><dc:creator>Fei Chen</dc:creator><dc:creator>Xingtan Zhang</dc:creator><dc:creator>Zhen Li</dc:creator><dc:creator>Yiyong Zhao</dc:creator><dc:creator>Rolf Lohaus</dc:creator><dc:creator>Xiaojun Chang</dc:creator><dc:creator>Wei Dong</dc:creator><dc:creator>Simon Y. W. Ho</dc:creator><dc:creator>Xing Liu</dc:creator><dc:creator>Aixia Song</dc:creator><dc:creator>Junhao Chen</dc:creator><dc:creator>Wenlei Guo</dc:creator><dc:creator>Zhengjia Wang</dc:creator><dc:creator>Yingyu Zhuang</dc:creator><dc:creator>Haifeng Wang</dc:creator><dc:creator>Xuequn Chen</dc:creator><dc:creator>Juan Hu</dc:creator><dc:creator>Yanhui Liu</dc:creator><dc:creator>Yuan Qin</dc:creator><dc:creator>Kai Wang</dc:creator><dc:creator>Shanshan Dong</dc:creator><dc:creator>Yang Liu</dc:creator><dc:creator>Shouzhou Zhang</dc:creator><dc:creator>Xianxian Yu</dc:creator><dc:creator>Qian Wu</dc:creator><dc:creator>Liangsheng Wang</dc:creator><dc:creator>Xueqing Yan</dc:creator><dc:creator>Yuannian Jiao</dc:creator><dc:creator>Hongzhi Kong</dc:creator><dc:creator>Xiaofan Zhou</dc:creator><dc:creator>Cuiwei Yu</dc:creator><dc:creator>Yuchu Chen</dc:creator><dc:creator>Fan Li</dc:creator><dc:creator>Jihua Wang</dc:creator><dc:creator>Wei Chen</dc:creator><dc:creator>Xinlu Chen</dc:creator><dc:creator>Qidong Jia</dc:creator><dc:creator>Chi Zhang</dc:creator><dc:creator>Yifan Jiang</dc:creator><dc:creator>Wanbo Zhang</dc:creator><dc:creator>Guanhua Liu</dc:creator><dc:creator>Jianyu Fu</dc:creator><dc:creator>Feng Chen</dc:creator><dc:creator>Hong Ma</dc:creator><dc:creator>Yves Van de Peer</dc:creator><dc:creator>Haibao Tang</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1852-5</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1852-5</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1852-5</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1852-5</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1840-9"">
            <title><![CDATA[Prediction and observation of an antiferromagnetic topological insulator]]></title>
            <link>https://www.nature.com/articles/s41586-019-1840-9</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1840-9"">doi:10.1038/s41586-019-1840-9</a></p>An intrinsic antiferromagnetic topological insulator, MnBi2Te4, is theoretically predicted and then realized experimentally, with implications for the study of exotic quantum phenomena.]]></content:encoded>
            <dc:title><![CDATA[Prediction and observation of an antiferromagnetic topological insulator]]></dc:title>
            <dc:creator>M. M. Otrokov</dc:creator><dc:creator>I. I. Klimovskikh</dc:creator><dc:creator>H. Bentmann</dc:creator><dc:creator>D. Estyunin</dc:creator><dc:creator>A. Zeugner</dc:creator><dc:creator>Z. S. Aliev</dc:creator><dc:creator>S. Gaß</dc:creator><dc:creator>A. U. B. Wolter</dc:creator><dc:creator>A. V. Koroleva</dc:creator><dc:creator>A. M. Shikin</dc:creator><dc:creator>M. Blanco-Rey</dc:creator><dc:creator>M. Hoffmann</dc:creator><dc:creator>I. P. Rusinov</dc:creator><dc:creator>A. Yu. Vyazovskaya</dc:creator><dc:creator>S. V. Eremeev</dc:creator><dc:creator>Yu. M. Koroteev</dc:creator><dc:creator>V. M. Kuznetsov</dc:creator><dc:creator>F. Freyse</dc:creator><dc:creator>J. Sánchez-Barriga</dc:creator><dc:creator>I. R. Amiraslanov</dc:creator><dc:creator>M. B. Babanly</dc:creator><dc:creator>N. T. Mamedov</dc:creator><dc:creator>N. A. Abdullayev</dc:creator><dc:creator>V. N. Zverev</dc:creator><dc:creator>A. Alfonsov</dc:creator><dc:creator>V. Kataev</dc:creator><dc:creator>B. Büchner</dc:creator><dc:creator>E. F. Schwier</dc:creator><dc:creator>S. Kumar</dc:creator><dc:creator>A. Kimura</dc:creator><dc:creator>L. Petaccia</dc:creator><dc:creator>G. Di Santo</dc:creator><dc:creator>R. C. Vidal</dc:creator><dc:creator>S. Schatz</dc:creator><dc:creator>K. Kißner</dc:creator><dc:creator>M. Ünzelmann</dc:creator><dc:creator>C. H. Min</dc:creator><dc:creator>Simon Moser</dc:creator><dc:creator>T. R. F. Peixoto</dc:creator><dc:creator>F. Reinert</dc:creator><dc:creator>A. Ernst</dc:creator><dc:creator>P. M. Echenique</dc:creator><dc:creator>A. Isaeva</dc:creator><dc:creator>E. V. Chulkov</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1840-9</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1840-9</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1840-9</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1840-9</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03847-z"">
            <title><![CDATA[Rise of Robot Radiologists]]></title>
            <link>https://www.nature.com/articles/d41586-019-03847-z</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03847-z"">doi:10.1038/d41586-019-03847-z</a></p>Deep-learning algorithms are peering into MRIs and x-rays with unmatched vision, but who is to blame when they make a mistake?]]></content:encoded>
            <dc:title><![CDATA[Rise of Robot Radiologists]]></dc:title>
            <dc:creator>Sara Reardon</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03847-z</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03847-z</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03847-z</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03847-z</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03838-0"">
            <title><![CDATA[The science news events that shaped 2019]]></title>
            <link>https://www.nature.com/articles/d41586-019-03838-0</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03838-0"">doi:10.1038/d41586-019-03838-0</a></p>Climate strikes, marsquakes and gaming AIs are among the year’s top stories.]]></content:encoded>
            <dc:title><![CDATA[The science news events that shaped 2019]]></dc:title>
            <dc:creator>Davide Castelvecchi</dc:creator><dc:creator>David Cyranoski</dc:creator><dc:creator>Elizabeth Gibney</dc:creator><dc:creator>Heidi Ledford</dc:creator><dc:creator>Amy Maxmen</dc:creator><dc:creator>Lauren Morello</dc:creator><dc:creator>Emma Stoye</dc:creator><dc:creator>Nidhi Subbaraman</dc:creator><dc:creator>Jeff Tollefson</dc:creator><dc:creator>Alexandra Witze</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03838-0</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03838-0</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03838-0</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03838-0</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03845-1"">
            <title><![CDATA[How Artificial Intelligence Will Change Medicine]]></title>
            <link>https://www.nature.com/articles/d41586-019-03845-1</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03845-1"">doi:10.1038/d41586-019-03845-1</a></p>How Artificial Intelligence Will Change Medicine]]></content:encoded>
            <dc:title><![CDATA[How Artificial Intelligence Will Change Medicine]]></dc:title>
            <dc:creator>Claudia Wallis</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03845-1</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03845-1</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03845-1</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03845-1</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1824-9"">
            <title><![CDATA[Cooperative elastic fluctuations provide tuning of the metal–insulator transition]]></title>
            <link>https://www.nature.com/articles/s41586-019-1824-9</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1824-9"">doi:10.1038/s41586-019-1824-9</a></p>Theoretical modelling shows that elastic fluctuations can enable the tuning of metal-to-insulator transitions, potentially also explaining the dependence of the transition temperature on cation radius in perovskite transition-metal oxides.]]></content:encoded>
            <dc:title><![CDATA[Cooperative elastic fluctuations provide tuning of the metal–insulator transition]]></dc:title>
            <dc:creator>G. G. Guzmán-Verri</dc:creator><dc:creator>R. T. Brierley</dc:creator><dc:creator>P. B. Littlewood</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1824-9</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1824-9</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1824-9</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1824-9</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1843-6"">
            <title><![CDATA[IL-17a promotes sociability in mouse models of neurodevelopmental disorders]]></title>
            <link>https://www.nature.com/articles/s41586-019-1843-6</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1843-6"">doi:10.1038/s41586-019-1843-6</a></p>IL-17a induced by immune activation affects cortical neural activity and promotes social interaction in a mouse model of neurodevelopmental disorders.]]></content:encoded>
            <dc:title><![CDATA[IL-17a promotes sociability in mouse models of neurodevelopmental disorders]]></dc:title>
            <dc:creator>Michael Douglas Reed</dc:creator><dc:creator>Yeong Shin Yim</dc:creator><dc:creator>Ralf D. Wimmer</dc:creator><dc:creator>Hyunju Kim</dc:creator><dc:creator>Changhyeon Ryu</dc:creator><dc:creator>Gwyneth Margaret Welch</dc:creator><dc:creator>Matias Andina</dc:creator><dc:creator>Hunter Oren King</dc:creator><dc:creator>Ari Waisman</dc:creator><dc:creator>Michael M. Halassa</dc:creator><dc:creator>Jun R. Huh</dc:creator><dc:creator>Gloria B. Choi</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1843-6</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1843-6</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1843-6</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1843-6</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03852-2"">
            <title><![CDATA[Ways to make meetings accessible]]></title>
            <link>https://www.nature.com/articles/d41586-019-03852-2</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03852-2"">doi:10.1038/d41586-019-03852-2</a></p>Four scientists with disabilities or chronic conditions share their conference conundrums and give advice on improving accessibility.]]></content:encoded>
            <dc:title><![CDATA[Ways to make meetings accessible]]></dc:title>
            <dc:creator>Emily Sohn</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03852-2</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03852-2</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03852-2</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03852-2</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03857-x"">
            <title><![CDATA[The scientific events that shaped the decade]]></title>
            <link>https://www.nature.com/articles/d41586-019-03857-x</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03857-x"">doi:10.1038/d41586-019-03857-x</a></p>The 2010s have seen breakthroughs in frontiers from gene editing to gravitational waves. The coming one must focus on climate change.]]></content:encoded>
            <dc:title><![CDATA[The scientific events that shaped the decade]]></dc:title>
            
            <dc:identifier>doi:10.1038/d41586-019-03857-x</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03857-x</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03857-x</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03857-x</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/s41586-019-1827-6"">
            <title><![CDATA[Probing the critical nucleus size for ice formation with graphene oxide nanosheets]]></title>
            <link>https://www.nature.com/articles/s41586-019-1827-6</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/s41586-019-1827-6"">doi:10.1038/s41586-019-1827-6</a></p>Nucleation experiments with water droplets containing differently sized graphene oxide nanosheets provide an experimental indication of the temperature-dependent size of the critical ice nucleus.]]></content:encoded>
            <dc:title><![CDATA[Probing the critical nucleus size for ice formation with graphene oxide nanosheets]]></dc:title>
            <dc:creator>Guoying Bai</dc:creator><dc:creator>Dong Gao</dc:creator><dc:creator>Zhang Liu</dc:creator><dc:creator>Xin Zhou</dc:creator><dc:creator>Jianjun Wang</dc:creator>
            <dc:identifier>doi:10.1038/s41586-019-1827-6</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/s41586-019-1827-6</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/s41586-019-1827-6</prism:doi>
            <prism:url>https://www.nature.com/articles/s41586-019-1827-6</prism:url>
        </item>
    
        <item rdf:about=""https://www.nature.com/articles/d41586-019-03836-2"">
            <title><![CDATA[We need a science of the night]]></title>
            <link>https://www.nature.com/articles/d41586-019-03836-2</link>
            <content:encoded>
                <![CDATA[<p>Nature, Published online: 18 December 2019; <a href=""https://www.nature.com/articles/d41586-019-03836-2"">doi:10.1038/d41586-019-03836-2</a></p>Understanding what happens in cities after sunset is crucial to global sustainable development, argues Michele Acuto.]]></content:encoded>
            <dc:title><![CDATA[We need a science of the night]]></dc:title>
            <dc:creator>Michele Acuto</dc:creator>
            <dc:identifier>doi:10.1038/d41586-019-03836-2</dc:identifier>
            <dc:source>Nature, Published online: 2019-12-18; | doi:10.1038/d41586-019-03836-2</dc:source>
            <dc:date>2019-12-18</dc:date>
            <prism:publicationName>Nature</prism:publicationName>
            <prism:doi>10.1038/d41586-019-03836-2</prism:doi>
            <prism:url>https://www.nature.com/articles/d41586-019-03836-2</prism:url>
        </item>
    
</rdf:RDF>";
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

        List<String> data = null;

        private ViewManager viewManager;
        private LayoutStack LayoutStack = new LayoutStack(false);
        private ContainerLayout statusLayout = new ContainerLayout();
    }
}

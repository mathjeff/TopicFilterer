using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TopicFilterer.Scoring;
using VisiPlacement;
using Xamarin.Forms;

namespace TopicFilterer.View
{
    class CustomizePreferences_Layout : ContainerLayout
    {
        public event Import RequestImport;
        public delegate void Import(string text);

        public CustomizePreferences_Layout(UserPreferences_Database userPreferencesDatabase, LayoutStack layoutStack)
        {
            ImportPreferences_Layout importLayout = new ImportPreferences_Layout();
            importLayout.RequestImport += ImportLayout_RequestImport;

            this.SubLayout = new MenuLayoutBuilder(layoutStack)
                .AddLayout("Add/Edit Rules", new Customize_ScoringRules_Layout(userPreferencesDatabase, layoutStack))
                .AddLayout("Add/Edit Feeds", new Customize_FeedUrls_Layout(userPreferencesDatabase))
                .AddLayout("Import Preferences", importLayout)
                .AddLayout("Export Preferences", new ExportPreferences_Layout(userPreferencesDatabase, layoutStack))
                .Build();
        }

        private void ImportLayout_RequestImport(string fileContent)
        {
            if (this.RequestImport != null)
                this.RequestImport.Invoke(fileContent);
        }
    }

    class Customize_ScoringRules_Layout : ContainerLayout, OnBack_Listener
    {
        public Customize_ScoringRules_Layout(UserPreferences_Database userPreferencesDatabase, LayoutStack layoutStack)
        {
            this.userPreferencesDatabase = userPreferencesDatabase;
            this.layoutStack = layoutStack;

            this.viewLayout = new View_ScoringRules_Layout(userPreferencesDatabase);
            this.newRule_layout = new New_ScoringRule_Layout(userPreferencesDatabase);

            BoundProperty_List rowHeights = new BoundProperty_List(2);
            rowHeights.BindIndices(0, 1);
            rowHeights.SetPropertyScale(0, 7);
            rowHeights.SetPropertyScale(1, 1);
            GridLayout grid = GridLayout.New(rowHeights, new BoundProperty_List(1), LayoutScore.Zero);
            grid.AddLayout(this.viewLayout);


            Button newRuleButton = new Button();
            newRuleButton.Clicked += NewRuleButton_Clicked;
            grid.AddLayout(new ButtonLayout(newRuleButton, "New Rule"));

            this.SubLayout = ScrollLayout.New(grid);
        }

        public void OnBack(LayoutChoice_Set layout)
        {
            if (layout == this.newRule_layout)
            {
                TextRule rule = this.newRule_layout.GetRule();
                if (rule != null)
                {
                    this.newRule_layout.Clear();
                    this.userPreferencesDatabase.AddScoringRule(rule);
                    this.viewLayout.UpdateLayout();
                }
            }
        }

        private void NewRuleButton_Clicked(object sender, EventArgs e)
        {
            this.layoutStack.AddLayout(this.newRule_layout, "New Rule", this);
        }

        UserPreferences_Database userPreferencesDatabase;
        LayoutStack layoutStack;
        View_ScoringRules_Layout viewLayout;
        New_ScoringRule_Layout newRule_layout;
    }

    class View_ScoringRules_Layout : ContainerLayout
    {
        public View_ScoringRules_Layout(UserPreferences_Database userPreferencesDatabase)
        {
            this.userPreferencesDatabase = userPreferencesDatabase;
            this.UpdateLayout();
        }
        public void UpdateLayout()
        {
            Vertical_GridLayout_Builder builder = new Vertical_GridLayout_Builder();
            List<TextRule> TextPredicates = this.userPreferencesDatabase.ScoringRules;
            foreach (TextRule rule in TextPredicates)
            {
                string text = rule.ToString();
                Button ruleButton = new Button();
                ruleButton.Clicked += RuleButton_Clicked;
                ButtonLayout buttonLayout = new ButtonLayout(ruleButton, text, 16);
                builder.AddLayout(buttonLayout);
            }
            this.SubLayout = ScrollLayout.New(builder.Build());
        }

        private void RuleButton_Clicked(object sender, EventArgs e)
        {
            Button button = sender as Button;
            String text = button.Text;
            this.userPreferencesDatabase.RemoveScoringRule(text);
            this.UpdateLayout();
        }

        UserPreferences_Database userPreferencesDatabase;
    }

    class New_ScoringRule_Layout : ContainerLayout
    {
        public New_ScoringRule_Layout(UserPreferences_Database userPreferencesDatabase)
        {
            this.userPreferencesDatabase = userPreferencesDatabase;

            this.progressLayout = new TextblockLayout();
            this.progressLayout.setBackgroundColor(Color.Black);
            this.progressLayout.setTextColor(Color.White);

            this.feedbackLayout = new TextblockLayout();
            this.feedbackLayout.setBackgroundColor(Color.Black);
            this.feedbackLayout.setTextColor(Color.White);

            LayoutChoice_Set controlsContainer = this.makeControls();
            this.SubLayout = new Vertical_GridLayout_Builder()
                .AddLayout(this.progressLayout)
                .AddLayout(this.feedbackLayout)
                .AddLayout(controlsContainer).Build();
            this.Clear();
        }
        private LayoutChoice_Set makeControls()
        {
            Button openParenButton = new Button();
            openParenButton.Clicked += OpenParenButton_Clicked;
            Button closeParenButton = new Button();
            closeParenButton.Clicked += CloseParenButton_Clicked;

            LayoutChoice_Set parensLayout = new Horizontal_GridLayout_Builder().Uniform()
                .AddLayout(new ButtonLayout(openParenButton, "("))
                .AddLayout(new ButtonLayout(closeParenButton, ")")).BuildAnyLayout();

            Button textButton = new Button();
            textButton.Clicked += TextButton_Clicked;
            this.newWord_box = new Editor();

            LayoutChoice_Set wordLayout = new Horizontal_GridLayout_Builder().Uniform()
                .AddLayout(new ButtonLayout(textButton, "Contains Phrase"))
                .AddLayout(new TextboxLayout(this.newWord_box)).BuildAnyLayout();

            Button notButton = new Button();
            notButton.Clicked += NotButton_Clicked;
            Button andButton = new Button();
            andButton.Clicked += AndButton_Clicked;
            Button orButton = new Button();
            orButton.Clicked += OrButton_Clicked;

            LayoutChoice_Set joinsLayout = new Horizontal_GridLayout_Builder().Uniform()
                .AddLayout(new ButtonLayout(andButton, ") And"))
                .AddLayout(new ButtonLayout(orButton, ") Or"))
                .AddLayout(new ButtonLayout(notButton, "Not (")).BuildAnyLayout();

            this.scoreBox = new Editor();
            this.scoreBox.TextColor = Color.White;
            this.scoreBox.BackgroundColor = Color.Black;
            TextboxLayout scoreBoxLayout = new TextboxLayout(this.scoreBox);
            scoreBoxLayout.SetBackgroundColor(Color.White);
            LayoutChoice_Set scoreLayout = new Horizontal_GridLayout_Builder().Uniform()
                .AddLayout(new TextblockLayout("Score:"))
                .AddLayout(scoreBoxLayout).BuildAnyLayout();

            return new Vertical_GridLayout_Builder().Uniform()
                .AddLayout(parensLayout).AddLayout(wordLayout).AddLayout(joinsLayout).AddLayout(scoreLayout)
                .BuildAnyLayout();
        }

        private void NotButton_Clicked(object sender, EventArgs e)
        {
            NotPredicate not = new NotPredicate();
            this.addChild(not);
            this.updateLayout();
        }

        public TextRule GetRule()
        {
            TextPredicate predicate = this.getPredicate();
            if (predicate == null)
                return null;
            double score = this.getScore();
            if (score == 0)
                return null;
            this.openPredicates.Clear();
            this.newWord_box.Text = "";
            this.scoreBox.Text = "";
            TextRule rule = new TextRule(predicate, score);
            return rule;
        }
        private double getScore()
        {
            double score = 0;
            double.TryParse(this.scoreBox.Text, out score);
            return score;
        }

        private TextPredicate getPredicate()
        {
            while (this.openPredicates.Count > 1)
            {
                this.closeParen();
            }
            if (this.openPredicates.Count == 1)
            {
                return this.openPredicates[0];
            }
            return null;
        }

        private void OpenParenButton_Clicked(object sender, EventArgs e)
        {
            this.openPredicates.Add(null);
            this.updateLayout();
        }

        private void CloseParenButton_Clicked(object sender, EventArgs e)
        {
            this.closeParen();
            this.updateLayout();
        }

        private void closeParen()
        {
            if (this.openPredicates.Count > 1)
            {
                // Remove this pending child and attach it to its parent.
                // This indicates that from now on we won't make any more changes to this child.
                // Instead, any subsequent changes will apply to its parent.
                TextPredicate predicate = this.openPredicates.Last();
                this.openPredicates.RemoveAt(this.openPredicates.Count - 1);
                this.addChild(predicate);
            }
            else
            {
                this.setError("There is no matching \"(\"!");
            }
        }



        private void OrButton_Clicked(object sender, EventArgs e)
        {
            OrPredicate or = new OrPredicate();
            TextPredicate child = this.getPendingChildPredicate();
            if (child != null)
            {
                or.AddChild(child);
                this.openPredicates.Add(or);
            }
            else
            {
                this.addChild(or);
            }
            this.updateLayout();
        }

        private void AndButton_Clicked(object sender, EventArgs e)
        {
            AndPredicate and = new AndPredicate();
            TextPredicate child = this.getPendingChildPredicate();
            if (child != null)
            {
                and.AddChild(child);
                this.openPredicates.Add(and);
                this.clearError();
            }
            else
            {
                this.addChild(and);
            }
            this.updateLayout();
        }

        private void TextButton_Clicked(object sender, EventArgs e)
        {
            string text = this.newWord_box.Text;
            if (text != null && text != "")
            {
                ContainsPhrase_Predicate word = new ContainsPhrase_Predicate(text);
                if (this.addChild(word))
                {
                    this.newWord_box.Text = "";
                }
                this.updateLayout();
            }
            else
            {
                this.setError("Type a word or phrase first");
            }
        }

        private TextPredicate getPendingChildPredicate()
        {
            if (this.openPredicates.Count > 0)
            {
                TextPredicate predicate = this.openPredicates.Last();
                this.openPredicates.RemoveAt(this.openPredicates.Count - 1);
                return predicate;
            }
            return null;
        }

        // add the given predicate as a child of the leafmost existing predicate
        private bool addChild(TextPredicate predicate)
        {
            this.clearError();
            if (this.openPredicates.Count < 1)
            {
                // no predicates already exist, this one becomes the primary one
                this.openPredicates.Add(predicate);
                return true;
            }
            TextPredicate last = this.openPredicates.Last();
            OrPredicate or = last as OrPredicate;
            if (or != null)
            {
                // we have a pending 'or'; add it here
                or.AddChild(predicate);
                return true;
            }
            AndPredicate and = last as AndPredicate;
            if (and != null)
            {
                // we have a pending 'and'; add it here
                and.AddChild(predicate);
                return true;
            }
            NotPredicate not = last as NotPredicate;
            if (not != null)
            {
                // we have a pending 'not'; add it here
                not.Child = predicate;
                return true;
            }
            if (last == null)
            {
                // We have an empty spot to add this predicate to
                // This user is expected to attach this to a parent later
                // If the user doesn't attach this to a parent later, we will for them
                this.openPredicates.RemoveAt(this.openPredicates.Count - 1);
                this.openPredicates.Add(predicate);
                return true;
            }
            this.setError("Already have an expression, try And or Or first");
            return false;
        }

        public void Clear()
        {
            this.openPredicates = new List<TextPredicate>();
            this.updateLayout();
        }
        private string GetText()
        {
            StringBuilder builder = new StringBuilder();
            foreach (TextPredicate predicate in this.openPredicates)
            {
                if (predicate == null)
                {
                    builder.Append("(");
                    continue;
                }
                builder.Append(predicate.builderText());
            }
            return builder.ToString();
        }
        private void setError(string text)
        {
            this.feedbackLayout.setText(text);
            this.feedbackLayout.setTextColor(Color.Red);
        }
        private void clearError()
        {
            this.feedbackLayout.setText(null);
        }
        private void updateLayout()
        {
            this.progressLayout.setText(this.GetText());
        }

        List<TextPredicate> openPredicates;
        Editor newWord_box;
        Editor scoreBox;
        UserPreferences_Database userPreferencesDatabase;
        TextblockLayout progressLayout;
        TextblockLayout feedbackLayout;
    }


    class Customize_FeedUrls_Layout : ContainerLayout
    {
        public Customize_FeedUrls_Layout(UserPreferences_Database userPreferencesDatabase)
        {
            // save some properties
            this.userPreferencesDatabase = userPreferencesDatabase;

            // set up display
            Vertical_GridLayout_Builder newFeedBuilder = new Vertical_GridLayout_Builder();
            TextblockLayout newFeedHelp = new TextblockLayout("Add new feed:");
            newFeedHelp.setBackgroundColor(Color.Black);
            newFeedHelp.setTextColor(Color.White);
            newFeedBuilder.AddLayout(newFeedHelp);
            this.newFeedBox = new Editor();
            newFeedBuilder.AddLayout(new TextboxLayout(this.newFeedBox));
            Button newFeedButton = new Button();
            newFeedButton.Clicked += NewFeedButton_Clicked;
            newFeedBuilder.AddLayout(new ButtonLayout(newFeedButton, "Add"));

            this.newFeedsLayout = newFeedBuilder.Build();

            this.updateLayout();
        }

        private void NewFeedButton_Clicked(object sender, EventArgs e)
        {
            if (this.newFeedBox.Text != null && this.newFeedBox.Text != "")
            {
                this.FeedUrls.Add(this.newFeedBox.Text);
                this.newFeedBox.Text = "";
                this.updateLayout();
                this.validateUrl(this.FeedUrls.Count - 1);
            }
        }

        private void validateUrl(int index)
        {
            string url = this.FeedUrls[index];

            if (this.webClient == null)
                this.webClient = new WebClient();

            Task.Run(() =>
            {
                UrlDownloader request = new UrlDownloader(this.webClient, url);
                System.Diagnostics.Debug.WriteLine("Validating " + url);
                String text = request.Get();
                List<Post> posts = new List<Post>();
                try
                {
                    posts = this.feedParser.parse(text);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("Not valid feed url: " + url);
                }
                bool validUrl = posts.Count > 0;
                Device.BeginInvokeOnMainThread(() =>
                {
                    this.urlValidity[url] = validUrl;
                    this.updateColors();
                });
            });

        }


        private void updateLayout()
        {
            this.feedButtons = new Dictionary<string, List<ButtonLayout>>();

            Vertical_GridLayout_Builder largeFont_builder = new Vertical_GridLayout_Builder();
            Vertical_GridLayout_Builder smallFont_builder = new Vertical_GridLayout_Builder();
            foreach (string url in this.FeedUrls)
            {
                Button feedButton = new Button();
                feedButton.Clicked += FeedButton_Clicked;
                ButtonLayout large = new ButtonLayout(feedButton, url, 24, true, false, false, true);
                ButtonLayout small = new ButtonLayout(feedButton, url, 16, true, false, false, true);

                largeFont_builder.AddLayout(large);
                smallFont_builder.AddLayout(small);

                List<ButtonLayout> buttonsHere = new List<ButtonLayout>();
                buttonsHere.Add(large);
                buttonsHere.Add(small);
                this.feedButtons[url] = buttonsHere;
            }

            LayoutChoice_Set topLayout = ScrollLayout.New(new LayoutUnion(largeFont_builder.Build(), smallFont_builder.Build()));
            this.SubLayout = new Vertical_GridLayout_Builder()
                .AddLayout(topLayout)
                .AddLayout(this.newFeedsLayout)
                .Build();
            this.updateColors();
        }

        private void updateColors()
        {
            foreach (KeyValuePair<string, List<ButtonLayout>> entry in this.feedButtons)
            {
                string url = entry.Key;
                foreach (ButtonLayout buttonLayout in entry.Value)
                {
                    Color textColor;
                    if (this.urlValidity.ContainsKey(url))
                    {
                        if (this.urlValidity[url])
                            textColor = Color.Green;
                        else
                            textColor = Color.Red;
                    }
                    else
                    {
                        textColor = Color.White;
                    }
                    buttonLayout.setTextColor(textColor);
                }
            }
        }

        private void FeedButton_Clicked(object sender, EventArgs e)
        {
            Button button = sender as Button;
            this.FeedUrls.Remove(button.Text);
            this.updateLayout();
        }

        public List<String> FeedUrls
        {
            get
            {
                return this.userPreferencesDatabase.FeedUrls;
            }
            set
            {
                this.userPreferencesDatabase.FeedUrls = value;
            }
        }

        UserPreferences_Database userPreferencesDatabase;
        Editor newFeedBox;
        LayoutChoice_Set newFeedsLayout;
        FeedParser feedParser = new FeedParser();
        WebClient webClient;
        Dictionary<string, bool> urlValidity = new Dictionary<string, bool>();
        Dictionary<string, List<ButtonLayout>> feedButtons = new Dictionary<string, List<ButtonLayout>>();
    }
}

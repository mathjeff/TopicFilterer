using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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
            this.progressContainer = new ContainerLayout();
            LayoutChoice_Set controlsContainer = this.makeControls();
            this.SubLayout = new Vertical_GridLayout_Builder().AddLayout(this.progressContainer).AddLayout(controlsContainer).Build();
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
                .AddLayout(new ButtonLayout(textButton, "Contains Word"))
                .AddLayout(new TextboxLayout(this.newWord_box)).BuildAnyLayout();

            Button andButton = new Button();
            andButton.Clicked += AndButton_Clicked;
            Button orButton = new Button();
            orButton.Clicked += OrButton_Clicked;

            LayoutChoice_Set joinsLayout = new Horizontal_GridLayout_Builder().Uniform()
                .AddLayout(new ButtonLayout(andButton, ") And"))
                .AddLayout(new ButtonLayout(orButton, ") Or")).BuildAnyLayout();

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
            if (this.openPredicates.Count > 0)
            {
                // Remove this pending child and attach it to its parent.
                // This indicates that from now on we won't make any more changes to this child.
                // Instead, any subsequent changes will apply to its parent.
                TextPredicate predicate = this.openPredicates.Last();
                this.openPredicates.RemoveAt(this.openPredicates.Count - 1);
                this.addChild(predicate);
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
                ContainsWord_Predicate word = new ContainsWord_Predicate(text);
                this.newWord_box.Text = "";
                this.addChild(word);
                this.updateLayout();
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
        private void addChild(TextPredicate predicate)
        {
            if (this.openPredicates.Count < 1)
            {
                // no predicates already exist, this one becomes the primary one
                this.openPredicates.Add(predicate);
                return;
            }
            TextPredicate last = this.openPredicates.Last();
            OrPredicate or = last as OrPredicate;
            if (or != null)
            {
                // we have a pending 'or'; add it here
                or.AddChild(predicate);
                return;
            }
            AndPredicate and = last as AndPredicate;
            if (and != null)
            {
                // we have a pending 'and'; add it here
                and.AddChild(predicate);
                return;
            }
            if (last == null)
            {
                // We have an empty spot to add this predicate to
                // This user is expected to attach this to a parent later
                // If the user doesn't attach this to a parent later, we will for them
                this.openPredicates.RemoveAt(this.openPredicates.Count - 1);
                this.openPredicates.Add(predicate);
                return;
            }
            // error; do nothing
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
        private void updateLayout()
        {
            TextblockLayout text = new TextblockLayout(this.GetText());
            text.setBackgroundColor(Color.Black);
            text.setTextColor(Color.White);
            this.progressContainer.SubLayout = text;
        }

        List<TextPredicate> openPredicates;
        Editor newWord_box;
        Editor scoreBox;
        UserPreferences_Database userPreferencesDatabase;
        ContainerLayout progressContainer;
    }


    class Customize_FeedUrls_Layout : ContainerLayout
    {
        public Customize_FeedUrls_Layout(UserPreferences_Database userPreferencesDatabase)
        {
            // save some properties
            this.userPreferencesDatabase = userPreferencesDatabase;

            // setup display
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
            }
        }

        private void updateLayout()
        {
            Vertical_GridLayout_Builder largeFont_builder = new Vertical_GridLayout_Builder();
            Vertical_GridLayout_Builder smallFont_builder = new Vertical_GridLayout_Builder();
            foreach (string url in this.FeedUrls)
            {
                Button feedButton = new Button();
                feedButton.Clicked += FeedButton_Clicked;
                largeFont_builder.AddLayout(new ButtonLayout(feedButton, url, 24, true, false, false, true));
                smallFont_builder.AddLayout(new ButtonLayout(feedButton, url, 16, true, false, false, true));
            }
            largeFont_builder.AddLayout(this.newFeedsLayout);
            smallFont_builder.AddLayout(this.newFeedsLayout);
            this.SubLayout = ScrollLayout.New(new LayoutUnion(largeFont_builder.Build(), smallFont_builder.Build()));
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
    }
}

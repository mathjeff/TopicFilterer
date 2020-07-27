using System;
using System.Collections.Generic;
using System.Text;
using VisiPlacement;
using Xamarin.Forms;

namespace TopicFilterer.View
{
    class CustomizePreferences_Layout : ContainerLayout
    {
        public CustomizePreferences_Layout(UserPreferences_Database userPreferencesDatabase)
        {
            // save some properties
            this.userPreferencesDatabase = userPreferencesDatabase;
            this.FeedUrls = userPreferencesDatabase.FeedUrls;

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
            this.FeedUrls.Add(this.newFeedBox.Text);
            this.newFeedBox.Text = "";
            this.updateLayout();
        }

        private void updateLayout()
        {
            Vertical_GridLayout_Builder builder = new Vertical_GridLayout_Builder();
            foreach (string url in this.FeedUrls)
            {
                Button feedButton = new Button();
                feedButton.Clicked += FeedButton_Clicked;
                ButtonLayout buttonLayout = new ButtonLayout(feedButton, url);
                //TextblockLayout layout = new TextblockLayout(url);
                //layout.setTextColor(Color.White);
                //layout.setBackgroundColor(Color.Black);
                builder.AddLayout(buttonLayout);
            }
            builder.AddLayout(this.newFeedsLayout);
            this.SubLayout = builder.BuildAnyLayout();
        }

        private void FeedButton_Clicked(object sender, EventArgs e)
        {
            Button button = sender as Button;
            this.FeedUrls.Remove(button.Text);
            this.updateLayout();
        }

        public List<string> FeedUrls;
        UserPreferences_Database userPreferencesDatabase;
        Editor newFeedBox;
        LayoutChoice_Set newFeedsLayout;
    }
}

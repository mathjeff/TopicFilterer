using System;
using System.Collections.Generic;
using System.Text;
using VisiPlacement;
using Xamarin.Forms;

namespace TopicFilterer.View
{
    class PostView : ContainerLayout
    {
        public event ClickedHandler PostClicked;
        public delegate void ClickedHandler(PostInteraction post);
        public event StarredHandler PostStarred;
        public delegate void StarredHandler(PostInteraction post);
        public PostView(AnalyzedPost post)
        {
            this.post = post;
            Vertical_GridLayout_Builder mainBuilder = new Vertical_GridLayout_Builder();
            Vertical_GridLayout_Builder titleBuilder = new Vertical_GridLayout_Builder();
            // post title
            foreach (AnalyzedString component in post.TitleComponents)
            {
                Label label = new Label();
                if (component.Score > 0)
                {
                    label.TextColor = Color.Green;
                }
                else
                {
                    if (component.Score < 0)
                    {
                        label.TextColor = Color.Red;
                    }
                    else
                    {
                        label.TextColor = Color.White;
                    }
                }
                label.BackgroundColor = Color.Black;
                TextblockLayout textBlockLayout = new TextblockLayout(label, 16, false, true);
                textBlockLayout.setText(component.Text);
                titleBuilder.AddLayout(textBlockLayout);
            }
            this.starButton = new Button();
            this.starButton.Clicked += SaveButton_Clicked;

            GridLayout topGrid = GridLayout.New(new BoundProperty_List(1), BoundProperty_List.WithRatios(new List<double>() { 5, 1 }), LayoutScore.Zero);
            topGrid.AddLayout(titleBuilder.BuildAnyLayout());
            topGrid.AddLayout(new ButtonLayout(this.starButton, "", 16));
            this.updateStarButton();
            mainBuilder.AddLayout(topGrid);

            this.linkLayout = new TextblockLayout(post.Interaction.Post.Source, 16, false, true);
            this.linkLayout.setBackgroundColor(Color.Black);
            this.updateLinkColor();
            mainBuilder.AddLayout(this.linkLayout);

            Button openButton = new Button();
            ButtonLayout openLayout = new ButtonLayout(openButton, "Open", 16);
            openButton.Clicked += OpenButton_Clicked;

            Button dismissButton = new Button();
            ButtonLayout dismissLayout = new ButtonLayout(dismissButton, "Mark Read", 16);
            dismissButton.Clicked += DismissButton_Clicked;

            mainBuilder.AddLayout(
                new Horizontal_GridLayout_Builder().Uniform()
                    .AddLayout(openLayout)
                    .AddLayout(dismissLayout)
                    .BuildAnyLayout()
            );

            this.SubLayout = mainBuilder.BuildAnyLayout();
        }

        private void SaveButton_Clicked(object sender, EventArgs e)
        {
            this.post.Interaction.Starred = !this.post.Interaction.Starred;
            this.updateStarButton();
            if (this.PostStarred != null)
            {
                this.PostStarred.Invoke(this.post.Interaction);
            }
        }
        private void updateStarButton()
        {
            if (this.post.Interaction.Starred)
                this.starButton.Text = "Unstar";
            else
                this.starButton.Text = "Star";
        }

        private void updateLinkColor()
        {
            if (this.post.Interaction.Visited)
                this.linkLayout.setTextColor(Color.Orange);
            else
                this.linkLayout.setTextColor(Color.White);
        }

        private void OpenButton_Clicked(object sender, EventArgs e)
        {
            this.MarkVisited();
            string source = this.post.Interaction.Post.Source;
            if (source != null && null != "")
            {
                if (this.PostClicked != null)
                {
                    this.PostClicked.Invoke(this.post.Interaction);
                }
            }
        }
        private void DismissButton_Clicked(object sender, EventArgs e)
        {
            this.MarkVisited();
        }

        private void MarkVisited()
        {
            this.post.Interaction.Visited = true;
            this.updateLinkColor();
        }
        AnalyzedPost post;
        TextblockLayout linkLayout;
        Button starButton;
    }
}

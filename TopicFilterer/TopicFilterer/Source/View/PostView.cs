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
        public PostView(PostInteraction interaction)
        {
            this.interaction = interaction;
            AnalyzedPost post = interaction.Post;
            Button button = new Button();

            Vertical_GridLayout_Builder builder = new Vertical_GridLayout_Builder();
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
                builder.AddLayout(textBlockLayout);
            }

            this.linkLayout = new TextblockLayout(post.Post.Source, 16, false, true);
            this.linkLayout.setBackgroundColor(Color.Black);
            this.updateLinkColor();
            
            builder.AddLayout(linkLayout);
            builder.AddLayout(new ButtonLayout(button, "Open", 16));

            button.Clicked += Button_Clicked;

            this.SubLayout = builder.BuildAnyLayout();
        }

        private void updateLinkColor()
        {
            if (this.interaction.Visited)
            {
                this.linkLayout.setTextColor(Color.Orange);
            }
            else
            {
                this.linkLayout.setTextColor(Color.White);
            }
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            this.interaction.Visited = true;
            this.updateLinkColor();
            string source = this.interaction.Post.Post.Source;
            if (source != null && null != "")
            {
                if (this.PostClicked != null)
                {
                    this.PostClicked.Invoke(this.interaction);
                }
            }
        }
        PostInteraction interaction;
        TextblockLayout linkLayout;
    }
}

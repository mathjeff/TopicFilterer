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
                TextblockLayout textBlockLayout = new TextblockLayout(label, 16, false, true);
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
                label.Text = component.Text;
                label.BackgroundColor = Color.Black;
                builder.AddLayout(textBlockLayout);
            }

            this.link = new Label();
            link.BackgroundColor = Color.Black;
            link.TextColor = Color.White;
            link.Text = post.Post.Source;
            TextblockLayout linkLayout = new TextblockLayout(link, 16, false, true);
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
                this.link.TextColor = Color.Orange;
            }
            else
            {
                this.link.TextColor = Color.White;
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
        Label link = new Label();
    }
}

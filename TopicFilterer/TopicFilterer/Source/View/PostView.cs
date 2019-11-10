using System;
using System.Collections.Generic;
using System.Text;
using VisiPlacement;
using Xamarin.Forms;

namespace TopicFilterer.View
{
    class PostView : ContainerLayout
    {
        public PostView(AnalyzedPost post)
        {
            this.post = post.Post;
            Button button = new Button();
            ButtonLayout buttonLayout = new ButtonLayout(button, "Open");

            Vertical_GridLayout_Builder builder = new Vertical_GridLayout_Builder().Uniform();
            // post title
            foreach (AnalyzedString component in post.TitleComponents)
            {
                Label label = new Label();
                TextblockLayout textBlockLayout = new TextblockLayout(label);
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

            builder.AddLayout(new ButtonLayout(button));

            button.Clicked += Button_Clicked;

            this.SubLayout = builder.BuildAnyLayout();
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            string source = this.post.Source;
            if (source != null && null != "")
            {
                Device.OpenUri(new Uri(this.post.Source));
            }
        }
        Post post;
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using VisiPlacement;
using Xamarin.Forms;

namespace TopicFilterer.View
{
    class PostView : ContainerLayout
    {
        public PostView(Post post)
        {
            this.post = post;
            Button button = new Button();
            ButtonLayout buttonLayout = new ButtonLayout(button, "Open");

            Vertical_GridLayout_Builder builder = new Vertical_GridLayout_Builder();
            LayoutChoice_Set titleLayout = new TextblockLayout(post.Title);
            /*if (post.Text.Length > 20)
            {
                TextblockLayout summaryLayout;
                summaryLayout = new TextblockLayout(post.Title.Substring(0, 17) + "...");
                titleLayout = new LayoutUnion(summaryLayout, titleLayout);
            }*/

            builder.AddLayout(titleLayout);
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

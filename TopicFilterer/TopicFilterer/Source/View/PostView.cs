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
            ButtonLayout buttonLayout = new ButtonLayout(button, post.Title);
            Vertical_GridLayout_Builder builder = new Vertical_GridLayout_Builder();
            builder.AddLayout(new TextblockLayout(post.Text, 20));
            if (post.Source != null && post.Source != "")
                builder.AddLayout(new TextblockLayout(post.Text, 20));
            this.detailsLayout = ScrollLayout.New(builder.BuildAnyLayout());

            button.Clicked += Button_Clicked;

            this.SubLayout = buttonLayout;
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            Device.OpenUri(new Uri(this.post.Source));
        }
        LayoutChoice_Set detailsLayout;
        Post post;
    }
}

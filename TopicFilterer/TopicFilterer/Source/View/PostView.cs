using System;
using System.Collections.Generic;
using System.Text;
using VisiPlacement;
using Xamarin.Forms;

namespace TopicFilterer.View
{
    class PostView : ContainerLayout
    {
        public PostView(Post post, LayoutStack layoutStack)
        {
            this.layoutStack = layoutStack;
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
            this.layoutStack.AddLayout(this.detailsLayout);
        }
        LayoutStack layoutStack;
        LayoutChoice_Set detailsLayout;
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using VisiPlacement;
using Xamarin.Forms;

namespace TopicFilterer
{
    class TopicFilterer
    {
        public TopicFilterer(ContentView contentView)
        {
            ViewManager viewManager = new ViewManager(contentView, null);

            Button startButton = new Button();

            startButton.Clicked += StartButton_Clicked;

            viewManager.SetLayout(new ButtonLayout(startButton, "Start"));
            this.viewManager = viewManager;
        }

        private void StartButton_Clicked(object sender, EventArgs e)
        {
            this.viewManager.SetLayout(new TextblockLayout("Clicked!"));
        }

        private ViewManager viewManager;
    }
}

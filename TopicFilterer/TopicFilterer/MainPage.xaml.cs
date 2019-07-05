using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisiPlacement;
using Xamarin.Forms;

namespace TopicFilterer
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            ContentView content = new ContentView();
            this.Content = content;

            this.topicFilterer = new TopicFilterer(content);
        }


        protected override bool OnBackButtonPressed()
        {
            return this.topicFilterer.GoBack();
        }

        private TopicFilterer topicFilterer;
    }
}

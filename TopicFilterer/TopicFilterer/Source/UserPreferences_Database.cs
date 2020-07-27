using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Xamarin.Forms;

namespace TopicFilterer
{
    class UserPreferences_Database
    {
        public static UserPreferences_Database Parse(string text)
        {
            return (new TextConverter().ParseUserPreferences(text));
        }
        public UserPreferences_Database()
        {
        }
        public List<String> FeedUrls
        {
            get
            {
                return this.feedUrls;
            }
            set
            {
                this.feedUrls = value;
            }
        }
        private List<string> feedUrls = new List<string>();
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using TopicFilterer.Scoring;
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
        public List<TextRule> ScoringRules
        {
            get
            {
                return this.rules;
            }
            set
            {
                this.rules = value;
            }
        }
        public void AddScoringRule(TextRule rule)
        {
            this.rules.Add(rule);
        }

        public void RemoveScoringRule(string textRepresentation)
        {
            for (int i = this.rules.Count - 1; i >= 0; i--)
            {
                if (this.rules[i].ToString() == textRepresentation)
                {
                    this.rules.RemoveAt(i);
                }
            }
        }
        private List<string> feedUrls = new List<string>();
        private List<TextRule> rules = new List<TextRule>();
    }
}

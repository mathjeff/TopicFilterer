using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using TopicFilterer.Scoring;

namespace TopicFilterer
{
    class TextConverter
    {
        public string ConvertToString(PostInteraction_Database postDatabase)
        {
            StringBuilder result = new StringBuilder();
            foreach (PostInteraction post in postDatabase.GetPosts())
            {
                result.Append(this.ConvertToString(post));
            }
            return result.ToString();
        }
        public string ConvertToString(UserPreferences_Database preferencesDatabase)
        {
            StringBuilder result = new StringBuilder();
            foreach (string feedUrl in preferencesDatabase.FeedUrls)
            {
                Dictionary<string, string> properties = new Dictionary<string, string>();
                properties[this.FeedUrl_Tag] = feedUrl;
                string text = this.ConvertToString(properties, this.FeedTag);
                result.Append(text);
            }
            foreach (TextRule rule in preferencesDatabase.ScoringRules)
            {
                string text = this.ConvertToString(rule);
                result.Append(text);
            }
            return result.ToString();
        }
        public string ConvertToString(PostInteraction post)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            properties[this.PostUrl_Tag] = this.XmlEscape(post.Post.Source);
            if (post.Visited)
                properties[this.PostVisited_Tag] = this.ConvertToStringBody(true);
            if (post.Starred)
                properties[this.PostStarred_Tag] = this.ConvertToStringBody(true);
            return this.ConvertToString(properties, PostTag);
        }
        public string ConvertToString(TextRule rule)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            properties[this.ScoreTag] = this.ConvertToStringBody(rule.ScoreIfMatches);
            properties[this.ScoringRulePredicate_Tag] = this.ConvertToString(rule.Criteria);
            return this.ConvertToString(properties, this.ScoringRule_Tag);
        }
        public string ConvertToString(TextPredicate predicate)
        {
            ContainsPhrase_Predicate contains = predicate as ContainsPhrase_Predicate;
            if (contains != null)
                return this.ConvertToString(contains);

            AndPredicate and = predicate as AndPredicate;
            if (and != null)
                return this.ConvertToString(and);

            OrPredicate or = predicate as OrPredicate;
            if (or != null)
                return this.ConvertToString(or);

            NotPredicate not = predicate as NotPredicate;
            if (not != null)
                return this.ConvertToString(not);

            throw new Exception("Unrecognized predicate: " + predicate);
        }
        public string ConvertToString(ContainsPhrase_Predicate predicate)
        {
            return this.ConvertToString(predicate.Text, this.ContainsPhrase_Tag);
        }
        public string ConvertToString(AndPredicate predicate)
        {
            StringBuilder content = new StringBuilder();
            foreach (TextPredicate child in predicate.Children)
            {
                content.Append(this.ConvertToString(child));
            }
            return this.ConvertToString(content.ToString(), this.And_Tag);
        }
        public string ConvertToString(OrPredicate predicate)
        {
            StringBuilder content = new StringBuilder();
            foreach (TextPredicate child in predicate.Children)
            {
                content.Append(this.ConvertToString(child));
            }
            return this.ConvertToString(content.ToString(), this.Or_Tag);
        }
        public string ConvertToString(NotPredicate predicate)
        {
            StringBuilder content = new StringBuilder();
            if (predicate.Child != null)
                content.Append(this.ConvertToString(predicate.Child));
            return this.ConvertToString(content.ToString(), this.Not_Tag);
        }
        public string ConvertToString(Dictionary<string, string> properties, string objectName)
        {
            string value = this.ConvertToStringBody(properties);
            return this.ConvertToString(value, objectName);
        }
        public string ConvertToStringBody(Dictionary<string, string> properties)
        {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> entry in properties)
            {
                builder.Append(this.ConvertToString(entry.Value, entry.Key));
            }
            return builder.ToString();
        }
        public string ConvertToString(string stringBody, string objectName)
        {
            return "<" + objectName + ">" + stringBody + "</" + objectName + ">";
        }
        private string ConvertToStringBody(bool value)
        {
            return value.ToString();
        }
        private string ConvertToStringBody(double value)
        {
            return value.ToString();
        }

        public List<PostInteraction> ParsePostInteractions(string text)
        {
            List<PostInteraction> interactions = new List<PostInteraction>();
            IEnumerable<XmlNode> nodes = this.ParseToXmlNodes(text);
            foreach (XmlNode node in nodes)
            {
                if (node.Name == this.PostTag)
                {
                    interactions.Add(this.ReadInteraction(node));
                    continue;
                }
                throw new InvalidDataException("Unrecognized node: <" + node.Name + ">");
            }
            return interactions;
        }

        public UserPreferences_Database ParseUserPreferences(string text)
        {
            UserPreferences_Database db = new UserPreferences_Database();
            IEnumerable<XmlNode> nodes = this.ParseToXmlNodes(text);
            List<string> feedUrls = new List<string>();
            List<TextRule> scoringRules = new List<TextRule>();
            foreach (XmlNode node in nodes)
            {
                if (node.Name == this.FeedTag)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name == this.FeedUrl_Tag)
                        {
                            string url = this.ReadText(child);
                            feedUrls.Add(url);
                        }
                    }
                }
                if (node.Name == this.ScoringRule_Tag)
                {
                    TextRule rule = this.ReadScoringRule(node);
                    scoringRules.Add(rule);
                }
            }
            db.FeedUrls = feedUrls;
            db.ScoringRules = scoringRules;
            return db;
        }

        public PostInteraction ReadInteraction(XmlNode nodeRepresentation)
        {
            PostInteraction interaction = new PostInteraction();
            interaction.Post = new Post();
            foreach (XmlNode child in nodeRepresentation.ChildNodes)
            {
                if (child.Name == this.PostUrl_Tag)
                {
                    interaction.Post.Source = this.ReadText(child);
                    continue;
                }
                if (child.Name == this.PostVisited_Tag)
                {
                    interaction.Visited = this.ReadBool(child);
                    continue;
                }
                if (child.Name == this.PostStarred_Tag)
                {
                    interaction.Starred = this.ReadBool(child);
                    continue;
                }
            }
            return interaction;
        }

        public TextRule ReadScoringRule(XmlNode nodeRepresentation)
        {
            TextPredicate criteria = null;
            double score = 0;
            foreach (XmlNode child in nodeRepresentation.ChildNodes)
            {
                if (child.Name == ScoringRulePredicate_Tag)
                {
                    foreach (XmlNode grandchild in child.ChildNodes)
                    {
                        criteria = this.ReadTextPredicate(grandchild);
                        continue;
                    }
                }
                if (child.Name == this.ScoreTag)
                {
                    score = this.ReadDouble(child);
                    continue;
                }
            }
            TextRule rule = new TextRule(criteria, score);
            return rule;
        }
        public TextPredicate ReadTextPredicate(XmlNode nodeRepresentation)
        {
            if (nodeRepresentation.Name == this.ContainsPhrase_Tag)
                return this.Read_ContainsPhrase_Predicate(nodeRepresentation);
            if (nodeRepresentation.Name == this.And_Tag)
                return this.Read_AndPredicate(nodeRepresentation);
            if (nodeRepresentation.Name == this.Or_Tag)
                return this.Read_OrPredicate(nodeRepresentation);
            if (nodeRepresentation.Name == this.Not_Tag)
                return this.Read_NotPredicate(nodeRepresentation);
            throw new Exception("Unrecognized text predicate " + nodeRepresentation.Name);
        }
        public ContainsPhrase_Predicate Read_ContainsPhrase_Predicate(XmlNode nodeRepresentation)
        {
            string text = this.ReadText(nodeRepresentation);
            return new ContainsPhrase_Predicate(text);
        }
        public AndPredicate Read_AndPredicate(XmlNode nodeRepresentation)
        {
            AndPredicate predicate = new AndPredicate();
            foreach (XmlNode childNode in nodeRepresentation.ChildNodes)
            {
                TextPredicate child = this.ReadTextPredicate(childNode);
                predicate.AddChild(child);
            }
            return predicate;
        }
        public OrPredicate Read_OrPredicate(XmlNode nodeRepresentation)
        {
            OrPredicate predicate = new OrPredicate();
            foreach (XmlNode childNode in nodeRepresentation.ChildNodes)
            {
                TextPredicate child = this.ReadTextPredicate(childNode);
                predicate.AddChild(child);
            }
            return predicate;
        }
        public NotPredicate Read_NotPredicate(XmlNode nodeRepresentation)
        {
            NotPredicate predicate = new NotPredicate();
            foreach (XmlNode childNode in nodeRepresentation.ChildNodes)
            {
                TextPredicate child = this.ReadTextPredicate(childNode);
                predicate.Child = child;;
            }
            return predicate;
        }
        private bool ReadBool(XmlNode nodeRepresentation)
        {
            return bool.Parse(this.ReadText(nodeRepresentation));
        }
        private double ReadDouble(XmlNode nodeRepresentation)
        {
            return double.Parse(this.ReadText(nodeRepresentation));
        }
        private string ReadText(XmlNode nodeRepresentation)
        {
            XmlNode firstChild = nodeRepresentation.FirstChild;
            if (firstChild != null)
            {
                return firstChild.Value;
            }
            return "";
        }

        private IEnumerable<XmlNode> ParseToXmlNodes(string text)
        {
            if (text == null || text.Length <= 0)
                return new List<XmlNode>(0);
            text = "<root>" + text + "</root>";
            XmlDocument document = new XmlDocument();
            try
            {
                document.LoadXml(text);
            }
            catch (XmlException e)
            {
                int lineNumber = e.LineNumber - 1;
                string[] lines = text.Split('\n');
                if (lineNumber >= 0 && lineNumber < lines.Length)
                {
                    string line = lines[lineNumber];
                    throw new XmlException("Failed to parse '" + lines[lineNumber] + "'", e);
                }
                throw e;
            }
            XmlNode root = document.FirstChild;
            if (root == null)
                return null;
            return root.ChildNodes;
        }

        private string XmlEscape(String input)
        {
            return input.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
        public string PostUrl_Tag
        {
            get
            {
                return "url";
            }
        }
        public string PostTag
        {
            get
            {
                return "post";
            }
        }
        public string PostVisited_Tag
        {
            get
            {
                return "visited";
            }
        }
        public string PostStarred_Tag
        {
            get
            {
                return "starred";
            }
        }
        public string FeedTag
        {
            get
            {
                return "feed";
            }
        }
        public string FeedUrl_Tag
        {
            get
            {
                return "url";
            }
        }

        public string ScoringRule_Tag
        {
            get
            {
                return "scoringRule";
            }
        }

        public string ScoringRulePredicate_Tag
        {
            get
            {
                return "if";
            }
        }

        public string ScoreTag
        {
            get
            {
                return "score";
            }
        }

        public string And_Tag
        {
            get
            {
                return "and";
            }
        }
        public string Or_Tag
        {
            get
            {
                return "or";
            }
        }
        public string Not_Tag
        {
            get
            {
                return "not";
            }
        }
        public string ContainsPhrase_Tag
        {
            get
            {
                // kept as this value for backwards compatibility
                return "containsWord";
            }
        }
    }
}

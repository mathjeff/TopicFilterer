using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

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
        public string ConvertToString(PostInteraction post)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            properties[this.PostUrl_Tag] = this.XmlEscape(post.Post.Post.Source);
            if (post.Visited)
                properties[this.PostVisited_Tag] = this.ConvertToStringBody(true);
            return this.ConvertToString(properties, PostTag);
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

        public ParseResult Parse(string text)
        {
            ParseResult result = new ParseResult();
            IEnumerable<XmlNode> nodes = this.ParseToXmlNodes(text);
            foreach (XmlNode node in nodes)
            {
                if (node.Name == this.PostTag)
                {
                    result.PostInteractions.Add(this.ReadInteraction(node));
                    continue;
                }
                throw new InvalidDataException("Unrecognized node: <" + node.Name + ">");
            }
            return result;
        }

        public PostInteraction ReadInteraction(XmlNode nodeRepresentation)
        {
            PostInteraction interaction = new PostInteraction();
            interaction.Post = new AnalyzedPost(new Post(), 0, new List<AnalyzedString>());
            foreach (XmlNode child in nodeRepresentation.ChildNodes)
            {
                if (child.Name == this.PostUrl_Tag)
                {
                    interaction.Post.Post.Source = this.ReadText(child);
                    continue;
                }
                if (child.Name == this.PostVisited_Tag)
                {
                    interaction.Visited = this.ReadBool(child);
                    continue;
                }
            }
            return interaction;
        }
        private bool ReadBool(XmlNode nodeRepresentation)
        {
            return bool.Parse(this.ReadText(nodeRepresentation));
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
                return "Visited";
            }
        }
    }

    public class ParseResult
    {
        public List<PostInteraction> PostInteractions = new List<PostInteraction>();
    }
}

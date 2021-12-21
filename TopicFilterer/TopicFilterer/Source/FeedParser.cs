using System;
using System.Collections.Generic;
using System.Text;

namespace TopicFilterer
{
    internal class FeedParser
    {
        public List<Post> parse(string text)
        {
            XmlDocument parse = new XmlDocument();
            parse.LoadXml(text);

            XmlNode feed = parse.FirstChild;
            if (feed.ChildNodes.Count == 1 && feed.FirstChild.Name == "channel")
            {
                feed = feed.FirstChild;
            }
            return this.extractPosts(feed);
        }
        private List<Post> extractPosts(XmlNode feed)
        {
            List<XmlNode> items = new List<XmlNode>();

            foreach (XmlNode node in feed.ChildNodes)
            {
                if (node.Name == "entry" || node.Name == "item")
                {
                    items.Add(node);
                }
            }

            List<Post> posts = new List<Post>();

            foreach (XmlNode node in items)
            {
                Post post = this.postFromData(node);
                if (post.Title != null && post.Title != "")
                    posts.Add(post);
                else
                    System.Diagnostics.Debug.WriteLine("Invalid post: " + post);
            }
            return posts;
        }

        private Post postFromData(XmlNode data)
        {
            Post post = new Post();

            foreach (XmlNode child in data.ChildNodes)
            {
                if (child.Name == "title")
                {
                    if (child.ChildNodes.Count == 1)
                        post.Title = child.FirstChild.Value;
                }
                if (child.Name == "content")
                {
                    if (child.ChildNodes.Count == 1)
                        post.Text = child.FirstChild.Value;
                }
                if (child.Name == "link")
                {
                    String attributeValue = child.getAttribute("href");
                    if (attributeValue != null)
                    {
                        post.Source = attributeValue;
                    }
                    else
                    {
                        if (child.ChildNodes.Count == 1)
                            post.Source = child.FirstChild.Value;
                    }
                }
            }
            return post;
        }
    }
}

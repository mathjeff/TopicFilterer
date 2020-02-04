using System;
using System.Collections.Generic;
using System.Text;

namespace TopicFilterer
{
    class PostInteraction_Database
    {
        public static PostInteraction_Database Parse(string text)
        {
            ParseResult result = (new TextConverter()).Parse(text);
            PostInteraction_Database db = new PostInteraction_Database();
            db.AddPosts(result.PostInteractions);
            return db;
        }

        public override string ToString()
        {
            return (new TextConverter()).ConvertToString(this);
        }

        public PostInteraction_Database()
        {
        }

        public PostInteraction Get(AnalyzedPost post)
        {
            string key = post.Post.Source;
            if (this.keyedInteractions.ContainsKey(key))
            {
                PostInteraction interaction = this.keyedInteractions[key];
                interaction.Post = post;
                return interaction;
            }
            else
            {
                PostInteraction interaction = new PostInteraction();
                interaction.Post = post;
                this.Add(interaction);
                return interaction;
            }
        }

        public void Add(PostInteraction interaction)
        {
            this.orderedInteractions.Add(interaction);
            this.keyedInteractions[interaction.Post.Post.Source] = interaction;
        }
        public void AddPosts(IEnumerable<PostInteraction> posts)
        {
            foreach (PostInteraction interaction in posts)
            {
                this.Add(interaction);
            }
        }
        public void ShrinkToSize(int size)
        {
            if (this.orderedInteractions.Count <= size)
                return;
            int numToRemove = this.orderedInteractions.Count - size;
            List<PostInteraction> itemsToRemove = this.orderedInteractions.GetRange(0, numToRemove);
            foreach (PostInteraction removal in itemsToRemove)
            {
                this.keyedInteractions.Remove(removal.Post.Post.Source);
            }
            this.orderedInteractions = this.orderedInteractions.GetRange(numToRemove, size);
        }
        public IEnumerable<PostInteraction> GetPosts()
        {
            return this.orderedInteractions;
        }

        private List<PostInteraction> orderedInteractions = new List<PostInteraction>();
        private Dictionary<string, PostInteraction> keyedInteractions = new Dictionary<string, PostInteraction>();
    }
}

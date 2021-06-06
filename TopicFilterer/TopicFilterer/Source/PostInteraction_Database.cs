using System;
using System.Collections.Generic;
using System.Text;

namespace TopicFilterer
{
    class PostInteraction_Database
    {
        public static PostInteraction_Database Parse(string text)
        {
            List<PostInteraction> result = (new TextConverter()).ParsePostInteractions(text);
            PostInteraction_Database db = new PostInteraction_Database();
            db.AddPosts(result);
            return db;
        }

        public override string ToString()
        {
            return (new TextConverter()).ConvertToString(this);
        }

        public PostInteraction_Database()
        {
        }

        public PostInteraction Get(Post post)
        {
            string key = post.Source;
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
            this.keyedInteractions[interaction.Post.Source] = interaction;
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
            List<PostInteraction> newOrderedInteractions = new List<PostInteraction>();
            Dictionary<string, PostInteraction> newKeyedInteractions = new Dictionary<string, PostInteraction>();
            int numSkipped = 0;

            for (int i = 0; i < this.orderedInteractions.Count; i++)
            {
                bool include = false;
                if (numSkipped >= numToRemove)
                {
                    include = true;
                }
                else
                {
                    if (this.orderedInteractions[i].Starred)
                    {
                        include = true;
                    }
                }
                if (include)
                {
                    PostInteraction interaction = this.orderedInteractions[i];
                    newOrderedInteractions.Add(interaction);
                    newKeyedInteractions.Add(interaction.Post.Source, interaction);
                }
                else
                {
                    numSkipped++;
                }
            }
            this.orderedInteractions = newOrderedInteractions;
            this.keyedInteractions = newKeyedInteractions;
        }
        public IEnumerable<PostInteraction> GetPosts()
        {
            return this.orderedInteractions;
        }

        private List<PostInteraction> orderedInteractions = new List<PostInteraction>();
        private Dictionary<string, PostInteraction> keyedInteractions = new Dictionary<string, PostInteraction>();
    }
}

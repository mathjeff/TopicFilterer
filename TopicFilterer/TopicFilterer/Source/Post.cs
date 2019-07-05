using System;
using System.Collections.Generic;
using System.Text;

namespace TopicFilterer
{
    // a Post is an individual unit of content that came from a single source and that is essentially an article or a story or a comic or something like that
    public class Post
    {
        public Post()
        {
        }
        public string Source;
        public string Text;
        public string Title;
    }

    public class ScoredPost
    {
        public ScoredPost(Post Post, double Score)
        {
            this.Post = Post;
            this.Score = Score;
        }
        public Post Post;
        public double Score;
    }

    public class ScoredPost_Sorter : IComparer<ScoredPost>
    {
        public int Compare(ScoredPost a, ScoredPost b)
        {
            return a.Score.CompareTo(b.Score);
        }
    }

}

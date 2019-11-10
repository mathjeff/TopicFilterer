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

    public class AnalyzedPost
    {
        public AnalyzedPost(Post Post, double Score, List<AnalyzedString> analyzedTitle)
        {
            this.Post = Post;
            this.Score = Score;
            this.TitleComponents = analyzedTitle;
        }
        public Post Post;
        public double Score;
        public List<AnalyzedString> TitleComponents;
    }

    public class AnalyzedString
    {
        public AnalyzedString(string text, double score)
        {
            this.Text = text;
            this.Score = score;
        }
        public string Text;
        public double Score;
    }

    public class ScoredPost_Sorter : IComparer<AnalyzedPost>
    {
        public int Compare(AnalyzedPost a, AnalyzedPost b)
        {
            return a.Score.CompareTo(b.Score);
        }
    }

}

using System;
using System.Collections.Generic;
using System.Text;

namespace TopicFilterer
{
    public class PostInteraction
    {
        public PostInteraction()
        {
        }
        public PostInteraction(AnalyzedPost post, bool visited)
        {
            this.Post = post;
            this.Visited = visited;
        }
        public AnalyzedPost Post;
        public bool Visited;
            
    }
}

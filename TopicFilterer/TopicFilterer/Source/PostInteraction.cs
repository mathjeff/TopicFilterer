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
        public PostInteraction(Post post, bool visited)
        {
            this.Post = post;
            this.Visited = visited;
        }
        public Post Post;
        public bool Visited;
        public bool Starred;
    }
}

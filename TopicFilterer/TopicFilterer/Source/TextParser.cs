using System;
using System.Collections.Generic;
using System.Text;

namespace TopicFilterer
{
    public class TextParser
    {
        public static bool isBreak(string text, int position)
        {
            if (position == -1 || position == text.Length)
                return true;
            if (position > text.Length)
                return false;
            char c = text[position];
            return wordSeparators.Contains(c);
        }
        private static HashSet<char> wordSeparators = new HashSet<char>() { ' ', ',', '.', '!', '?', '-', ':' };

    }

}

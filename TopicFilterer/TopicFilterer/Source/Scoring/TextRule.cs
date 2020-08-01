using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace TopicFilterer.Scoring
{
    public interface TextPredicate
    {
        // whether this predicate matches
        bool matches(string text);

        // what to show a user that's in the middle of building one of these
        string builderText();
    }

    public class ContainsWord_Predicate : TextPredicate
    {
        public ContainsWord_Predicate(string word)
        {
            this.word = word.ToLower();
        }
        public bool matches(string text)
        {
            char[] wordSeparators = new char[] { ' ', ',', '.', '!', '?', '-' };
            List<String> orderedWords = new List<string>(text.ToLower().Split(wordSeparators));
            return orderedWords.Contains(this.word);
        }
        public string Text
        {
            get
            {
                return this.word;
            }
        }
        public override string ToString()
        {
            return "contains \"" + this.word + "\"";
        }
        public string builderText()
        {
            return this.ToString();
        }
        private string word;
    }

    public class AndPredicate : TextPredicate
    {
        public AndPredicate()
        {
            this.components = new List<TextPredicate>();
        }
        public AndPredicate(TextPredicate a, TextPredicate b)
        {
            this.components = new List<TextPredicate>() { a, b };
        }
        public bool matches(string text)
        {
            foreach (TextPredicate component in this.components)
            {
                if (!component.matches(text))
                {
                    return false;
                }
            }
            return true;
        }
        public void AddChild(TextPredicate child)
        {
            this.components.Add(child);
        }
        public IEnumerable<TextPredicate> Children
        {
            get
            {
                return this.components;
            }
        }
        public override string ToString()
        {
            if (this.components.Count == 0)
                return "true";
            if (this.components.Count == 1)
                return this.components[0].ToString();
            List<String> texts = new List<String>();
            foreach (TextPredicate component in this.components)
            {
                texts.Add(component.ToString());
            }
            return "(" + string.Join(" and ", texts) + ")";
        }

        public string builderText()
        {
            if (this.components.Count == 0)
                return "(and)";
            if (this.components.Count == 1)
                return "(" + this.components[0].builderText() + " and";
            List<String> texts = new List<String>();
            foreach (TextPredicate component in this.components)
            {
                texts.Add(component.ToString());
            }
            return "(" + string.Join(" and ", texts) + ")";
        }
        private List<TextPredicate> components;
    }

    public class OrPredicate : TextPredicate
    {
        public OrPredicate()
        {
            this.components = new List<TextPredicate>();
        }
        public OrPredicate(TextPredicate a, TextPredicate b)
        {
            this.components = new List<TextPredicate>() { a, b };
        }
        public bool matches(string text)
        {
            foreach (TextPredicate component in this.components)
            {
                if (component.matches(text))
                {
                    return true;
                }
            }
            return false;
        }
        public void AddChild(TextPredicate child)
        {
            this.components.Add(child);
        }

        public IEnumerable<TextPredicate> Children
        {
            get
            {
                return this.components;
            }
        }

        public override string ToString()
        {
            if (this.components.Count == 0)
            {
                return "false";
            }
            if (this.components.Count == 1)
            {
                return this.components[0].ToString();
            }
            List<String> texts = new List<String>();
            foreach (TextPredicate component in this.components)
            {
                texts.Add(component.ToString());
            }
            return "(" + string.Join(" or ", texts) + ")";
        }

        public string builderText()
        {
            if (this.components.Count == 0)
                return "(or)";
            if (this.components.Count == 1)
                return "(" + this.components[0].builderText() + " or";
            List<String> texts = new List<String>();
            foreach (TextPredicate component in this.components)
            {
                texts.Add(component.ToString());
            }
            return "(" + string.Join(" or ", texts) + ")";
        }


        private List<TextPredicate> components;
    }

    public class TextRule
    {
        public TextRule(TextPredicate criteria, double score)
        {
            this.criteria = criteria;
            this.score = score;
        }
        public double computeScore(string title)
        {
            if (this.criteria.matches(title))
            {
                return this.ScoreIfMatches;
            }
            return 0;
        }
        public override string ToString()
        {
            string scoreText;
            if (this.ScoreIfMatches > 0)
                scoreText = "+" + this.ScoreIfMatches;
            else
                scoreText = "" + this.ScoreIfMatches;
            return this.criteria.ToString() + " -> " + scoreText;
        }
        public TextPredicate Criteria
        {
            get
            {
                return this.criteria;
            }
        }
        public double ScoreIfMatches
        {
            get
            {
                return this.score;
            }
        }

        private TextPredicate criteria;
        private double score;
    }

}

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace TopicFilterer
{
    class XmlDocument : XmlNode
    {
        public void LoadXml(String xml)
        {
            XmlReader reader = XmlReader.Create(new StringReader(xml));
            List<XmlNode> nodeStack = new List<XmlNode>();
            XmlNode newNode = this;
            nodeStack.Add(newNode);
            try
            {
                while (reader.MoveToNextAttribute() || reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            // found a new element, and now we're reading the nodes inside it
                            newNode = new XmlNode();
                            newNode.Name = reader.Name;
                            newNode.Value = reader.Value;
                            nodeStack[nodeStack.Count - 1].ChildNodes.Add(newNode);
                            if (!reader.IsEmptyElement)
                                nodeStack.Add(newNode);
                            break;
                        case XmlNodeType.Text:
                            // found a new element that doesn't have any more nodes inside it
                            newNode = new XmlNode();
                            newNode.Name = reader.Name;
                            newNode.Value = reader.Value;
                            nodeStack[nodeStack.Count - 1].ChildNodes.Add(newNode);
                            break;
                        case XmlNodeType.EndElement:
                            // found the end of a previous element
                            nodeStack.RemoveAt(nodeStack.Count - 1);
                            break;
                        case XmlNodeType.Attribute:
                            XmlNode attribute = new XmlNode();
                            attribute.Name = reader.Name;
                            attribute.Value = reader.Value;
                            newNode.Attributes.Add(attribute);
                            break;
                        case XmlNodeType.Whitespace:
                            break;
                        case XmlNodeType.XmlDeclaration:
                            break;
                        default:
                            throw new Exception("Unrecognized node type: " + reader.NodeType);
                    }
                }
            }
            catch (XmlException e)
            {
                String text = e.ToString();
                System.Diagnostics.Debug.WriteLine(e);
                throw e;
            }
        }
    }

    public class XmlNode
    {
        public XmlNode()
        {
            this.ChildNodes = new List<XmlNode>();
            this.Attributes = new List<XmlNode>();
        }

        public String getAttribute(String key)
        {
            foreach (XmlNode attribute in this.Attributes)
            {
                if (attribute.Name == key)
                    return attribute.Value;
            }
            return null;
        }
        public List<XmlNode> ChildNodes { get; set; }
        public List<XmlNode> Attributes { get; set; }
        public String Name { get; set; }
        public String Value { get; set; }
        public XmlNode FirstChild
        {
            get
            {
                if (this.ChildNodes.Count > 0)
                    return this.ChildNodes[0];
                return null;
            }
        }
    }
}

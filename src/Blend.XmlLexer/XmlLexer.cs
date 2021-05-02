using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Blend.Xml
{
    public class XmlLexer
    {
        private class ChildHandler
        {
            public ChildHandler(XmlNodeType nodeType, string nodeName, Action<XmlLexer> action)
            {
                NodeType = nodeType;
                NodeName = nodeName;
                Action = action;
            }

            public XmlNodeType NodeType { get; }
            public string NodeName { get; }
            public Action<XmlLexer> Action { get; }
        }

        private readonly List<ChildHandler> handlers = new List<ChildHandler>();
        private readonly List<Action<XmlReader>> actions = new List<Action<XmlReader>>();
        private Action afterAction;

        public XmlLexer Match(XmlNodeType nodeType, string nodeName, Action<XmlLexer> action)
        {
            handlers.Add(new ChildHandler(nodeType, nodeName, action));
            return this;
        }

        public XmlLexer Do(Action<XmlReader> action)
        {
            actions.Add(action);
            return this;
        }

        public XmlLexer OnElementClose(Action afterAction)
        {
            this.afterAction = afterAction;
            return this;
        }

        public bool Execute(XmlReader reader)
        {
            foreach (var action in actions)
                action(reader);

            // Next check for self-closing element, exit in that case.
            if (reader.NodeType == XmlNodeType.Element && reader.IsEmptyElement)
            {
                afterAction?.Invoke();
                return true;
            }

            // Do not continue reading in the case of text/cdata nodes
            if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA)
            {
                afterAction?.Invoke();
                return true;
            }

            // If this is an element, capture the name to know when to stop reading.
            string elementName = (reader.NodeType == XmlNodeType.Element) ? reader.Name : null;

            // Loop through all fragments
            while (reader.Read())
            {
                // If this the closing element, exit
                if (reader.NodeType == XmlNodeType.EndElement && reader.Name == elementName)
                {
                    afterAction?.Invoke();
                    return true;
                }

                // Now process all the rest of the viable handlers.
                // Get all text/cdata handlers if the current element is a text/cdata handler.
                // Get all matching element handlers if current element is a element handler.
                // Otherwise, nothing will match, just continue.
                IEnumerable<ChildHandler> handlers;
                switch (reader.NodeType)
                {
                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                        handlers = this.handlers.Where(x => x.NodeType == XmlNodeType.Text);
                        break;
                    case XmlNodeType.Element:
                        handlers = this.handlers.Where(x => x.NodeType == XmlNodeType.Element 
                            && (x.NodeName == null || x.NodeName == reader.Name));
                        break;
                    default:
                        continue;
                }

                // Now process the handlers
                foreach (ChildHandler handler in handlers)
                {
                    // It's an element, so create a new Lexer "scope" and execute it the action on that scope.
                    XmlLexer scope = new XmlLexer();
                    handler.Action(scope);

                    // Execute, exit if we run out of input.
                    if (!scope.Execute(reader))
                    {
                        afterAction?.Invoke();
                        return false;
                    }
                }
            }

            afterAction?.Invoke();
            return false;
        }
    }

    public static class XmlLexerExtensions
    {
        public static XmlLexer Do(this XmlLexer builder, XmlNodeType nodeType, string nodeName, Action<XmlReader> action)
            => builder.Match(nodeType, nodeName, l => l.Do(action));

        public static XmlLexer OnElement(this XmlLexer builder, string elementName, Action<XmlLexer> action)
            => builder.Match(XmlNodeType.Element, elementName, action);

        public static XmlLexer OnText(this XmlLexer builder, Action<string> action)
            => builder.Do(XmlNodeType.Text, null, node => action(node.Value))
                .Do(XmlNodeType.CDATA, null, node => action(node.Value));

        public static XmlLexer OnElementText(this XmlLexer builder, string elementName, Action<string> action)
            => builder.Match(XmlNodeType.Element, elementName, x => x.OnText(action));

        public static XmlLexer OnAttributeValue(this XmlLexer builder, string attributeName, Action<string> action)
            => builder.Do(node => action(node.GetAttribute(attributeName)));

        public static XmlLexer OnAttributes(this XmlLexer builder, Action<string, string> action)
            => builder.Do(node =>
            {
                if (node.HasAttributes)
                {
                    while (node.MoveToNextAttribute())
                    {
                        action(node.Name, node.Value);
                    }

                    node.MoveToElement();
                }
            });

    }
}

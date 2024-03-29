﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.MarkedNet;
using Ooui;

namespace AdaptiveCards.Rendering.Html
{
    public static class MarkdownToElementConverter
    {
        public static IEnumerable<Element> Convert(string markdown)
        {
            var marked = new Marked();
            marked.Options.Mangle = false;
            marked.Options.Sanitize = true;
            marked.Options.XHtml = true;

            var rawXhtml = marked.Parse(markdown);
            var root = XElement.Parse($"<root>{rawXhtml}</root>");

            return root.Elements().Select(RawXhtmlToHtmlTag);
        }


        private static Element RawXhtmlToHtmlTag(XElement element)
        {
            var htmlTag = new Div() { Title = element.Name.LocalName };

            foreach (var node in element.Nodes())
            {
                switch (node.NodeType)
                {
                    case XmlNodeType.Text:
                        htmlTag.AppendChild(new Span(((XText)node).Value));
                        break;

                    case XmlNodeType.Element:
                        htmlTag.AppendChild(RawXhtmlToHtmlTag((XElement)node));
                        break;
                }
            }

            foreach (var attribute in element.Attributes())
            {
                switch (attribute.Name.LocalName.ToLowerInvariant())
                {
                    case "style":
                        // Style needs to be parsed out into the Styles attribute of the HtmlTag.
                        // But we don't ever expect the markdown processor to return it, so we don't need to handle it for now.
                        throw new InvalidOperationException();

                    case "class":
                        var classNames = attribute.Value.Split(' ').Where(className => !string.IsNullOrWhiteSpace(className));
                        foreach (var className in classNames)
                        {
                            htmlTag.ClassName = className.Trim();
                        }

                        break;

                    default:
                        htmlTag.SetAttribute(attribute.Name.LocalName, attribute.Value);
                        break;
                }
            }

            return htmlTag;
        }
    }
}

using Ooui;
using System;
using System.Linq;

namespace AdaptiveCards.Rendering.Html
{
    public static class ElementHelper
    {
        public static Element AddClass(this Element element,string name)
        {
            element.ClassName += name;
            return element;
        }

        public static Element Style(this Element element,string name,string style)
        {
            element.Style.GetType().GetProperty(name)?.SetValue(element.Style,style);
            return element;
        }
        public static Element SetAttr(this Element element,string name,string value)
        {
            element.SetAttribute(name, value);
            return element;
        }

        public static Element SetInnerText(this Element element, string text)
        {
            element.Text = text;
            return element;
        }

    }
}
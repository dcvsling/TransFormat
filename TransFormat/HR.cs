using AdaptiveCards;
using Microsoft.AspNetCore.Http;
using System;

namespace Ooui.AspNetCore
{
    public class HR : Element { public HR() : base("hr") {} }

    public class AdaptiveActionElement : AdaptiveTypedElement
    {
        public override string Type { get; set; } = "ac-el";
        public AdaptiveElement Element { get; set; }
        public AdaptiveAction Action { get; set; }
        public AdaptiveSpacing Spacing
        {
            get => Element.Spacing;
            set => Element.Spacing = value;
        }
        public bool Separator
        {
            get => Element.Separator;
            set => Element.Separator = value;
        }
        public string Title
        {
            get => Action.Title;
            set => Action.Title = value;
        }
    }
}

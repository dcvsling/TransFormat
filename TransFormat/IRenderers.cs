using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using AdaptiveCards;

namespace Ooui.AspNetCore
{
    public interface IRenderers<TUIElement, TContext>
        where TUIElement : class
        where TContext : class
    {
        Func<AdaptiveTypedElement, TContext, TUIElement> Get(Type type);
        Func<TElement, TContext, TUIElement> Get<TElement>() where TElement : AdaptiveTypedElement;
        void Remove<TElement>() where TElement : AdaptiveTypedElement;
        void Set<TElement>(Func<TElement, TContext, TUIElement> renderer) where TElement : AdaptiveTypedElement;
    }

    public class ElementRelation : AdaptiveTypedElement
    {
        public override string Type { get; set; } = "relation";
        public AdaptiveElement TriggerElement { get; set; }
        public AdaptiveElement RelatedElement { get; set; }
        public AdaptiveAction action { get; set; }
        public string Name { get; set; }
    }
}
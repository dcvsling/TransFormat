using Ooui;
using System.Collections.Generic;

namespace AdaptiveCards.Rendering.Html
{
    public class ElementRenderedAdaptiveCard : RenderedAdaptiveCardBase
    {
        public ElementRenderedAdaptiveCard(Element element, AdaptiveCard originatingCard, IList<AdaptiveWarning> warnings)
            : base(originatingCard, warnings)
        {
            Element = element;
        }

        /// <summary>
        /// The rendered HTML for the card
        /// </summary>
        public Element Element { get; }
    }
}
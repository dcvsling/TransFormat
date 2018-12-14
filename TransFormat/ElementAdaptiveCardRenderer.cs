using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Ooui;
using Ooui.AspNetCore;

namespace AdaptiveCards.Rendering.Html
{
    /// <summary>
    ///     Render a card as HTML suitable for server side generation
    /// </summary>
    public class ElementAdaptiveCardRenderer : AdaptiveCardRendererBase<Element, ElementAdaptiveRenderContext>
    {
        protected override AdaptiveSchemaVersion GetSupportedSchemaVersion()
        {
            return new AdaptiveSchemaVersion(1, 0);
        }

        /// <summary>
        /// Generate a ID, useful for joining two elements together, e.g., an input and label
        /// </summary>
        public static Func<string> GenerateRandomId => () => "ac-" + Guid.NewGuid().ToString().Substring(0, 8);

        /// <summary>
        /// Adds a CSS class to the action based on it's type name. Default is "ac-action-[actionName]
        /// </summary>
        public static Func<AdaptiveAction, string> GetActionCssClass = (action) =>
        {
            var lenFromDot = action.Type.IndexOf(".") + 1;
            var suffix = action.Type.Substring(lenFromDot, action.Type.Length - lenFromDot);
            return "ac-action-" + suffix.Replace(suffix[0], char.ToLower(suffix[0]));
        };

        /// <summary>
        /// A set of transforms that are applied to the Elements for specific types
        /// </summary>
        public static AdaptiveRenderTransformers<Element, ElementAdaptiveRenderContext> ActionTransformers { get; } = new AdaptiveRenderTransformers<Element, ElementAdaptiveRenderContext>();

        public ElementAdaptiveCardRenderer() : this(new AdaptiveHostConfig()) { }

        public ElementAdaptiveCardRenderer(AdaptiveHostConfig config)
        {
            SetObjectTypes();
            HostConfig = config;
        }

        public ElementRenderedAdaptiveCard RenderElement(AdaptiveCard card)
        {
            EnsureCanRender(card);

            try
            {
                var context = new ElementAdaptiveRenderContext(HostConfig, ElementRenderers);
                var tag = context.Render(card);
                return new ElementRenderedAdaptiveCard(tag, card, context.Warnings);
            }
            catch (Exception ex)
            {
                throw new AdaptiveRenderException("Failed to render card", ex)
                {
                    CardFallbackText = card.FallbackText
                };
            }
        }

        private void SetObjectTypes()
        {
            ElementRenderers.Set<AdaptiveCard>(AdaptiveCardRender);

            ElementRenderers.Set<AdaptiveTextBlock>(TextBlockRender);
            ElementRenderers.Set<AdaptiveImage>(ImageRender);

            ElementRenderers.Set<AdaptiveContainer>(ContainerRender);
            ElementRenderers.Set<AdaptiveColumn>(ColumnRender);
            ElementRenderers.Set<AdaptiveColumnSet>(ColumnSetRender);
            ElementRenderers.Set<AdaptiveFactSet>(FactSetRender);
            ElementRenderers.Set<AdaptiveImageSet>(ImageSetRender);

            ElementRenderers.Set<AdaptiveChoiceSetInput>(ChoiceSetRender);
            ElementRenderers.Set<AdaptiveTextInput>(TextInputRender);
            ElementRenderers.Set<AdaptiveNumberInput>(NumberInputRender);
            ElementRenderers.Set<AdaptiveDateInput>(DateInputRender);
            ElementRenderers.Set<AdaptiveTimeInput>(TimeInputRender);
            ElementRenderers.Set<AdaptiveToggleInput>(ToggleInputRender);

            ElementRenderers.Set<AdaptiveSubmitAction>(AdaptiveActionRender);
            ElementRenderers.Set<AdaptiveOpenUrlAction>(AdaptiveActionRender);
            ElementRenderers.Set<AdaptiveShowCardAction>(AdaptiveActionRender);

            ActionTransformers.Register<AdaptiveOpenUrlAction>((action, tag, context) => tag.SetAttr("data-ac-url", action.Url.AbsoluteUri));
            ActionTransformers.Register<AdaptiveSubmitAction>((action, tag, context) => tag.SetAttr("data-ac-submitData", Newtonsoft.Json.JsonConvert.SerializeObject(action.Data, Formatting.None)));
            ActionTransformers.Register<AdaptiveShowCardAction>((action, tag, context) => tag.SetAttr("data-ac-showCardId", GenerateRandomId()));
        }

        protected static Element AddActionAttributes(AdaptiveAction action, Element tag, ElementAdaptiveRenderContext context)
        {
            tag.AddClass(GetActionCssClass(action))
                .SetAttr("role", "button")
                .SetAttr("aria-label", action.Title ?? "");

            ActionTransformers.Apply(action, tag, context);

            return tag;
        }

        protected static Element AdaptiveActionRender(AdaptiveAction action, ElementAdaptiveRenderContext context)
        {
            if (context.Config.SupportsInteractivity)
            {
                var buttonElement = new Ooui.Anchor() { Text = action.Title }
                    .Style("overflow", "hidden")
                    .Style("white-space", "nowrap")
                    .Style("text-overflow", "ellipsis")
                    .Style("flex",
                        context.Config.Actions.ActionAlignment == AdaptiveHorizontalAlignment.Stretch ? "0 1 100%" : "0 1 auto")
                    .AddClass("ac-pushButton");

                AddActionAttributes(action, buttonElement, context);
                return buttonElement;
            }

            return null;
        }

        protected static Element AdaptiveCardRender(AdaptiveCard card, ElementAdaptiveRenderContext context)
        {
            var uiCard = new Div() { Text = card.FallbackText }
                .AddClass($"ac-{card.Type.ToLower()}")
                .Style("width", "100%")
                .Style("background-color", context.GetRGBColor(context.Config.ContainerStyles.Default.BackgroundColor))
                .Style("padding", $"{context.Config.Spacing.Padding}px")
                .Style("box-sizing", "border-box");

            if (!string.IsNullOrEmpty(context.Config.FontFamily))
                uiCard.Style("font-family", context.Config.FontFamily);

            if (card.BackgroundImage != null)
                uiCard.Style("background-image", $"url('{card.BackgroundImage}')")
                    .Style("background-repeat", "no-repeat")
                    .Style("background-size", "cover");

            AddContainerElements(uiCard, card.Body, card.Actions, context);

            return uiCard;
        }

        protected static void AddContainerElements(Element uiContainer, IList<AdaptiveElement> elements, IList<AdaptiveAction> actions, ElementAdaptiveRenderContext context)
        {
            if (elements != null)
            {
                foreach (var cardElement in elements)
                {
                    // each element has a row
                    var uiElement = context.Render(cardElement);
                    if (uiElement != null)
                    {
                        if (uiContainer.Children.Any())
                        {
                            AddSeparator(uiContainer, cardElement, context);
                        }

                        uiContainer.AppendChild(uiElement);
                    }
                }
            }

            if (context.Config.SupportsInteractivity && actions != null)
            {
                var uiButtonStrip = new Div()
                    .AddClass("ac-actionset")
                    .Style("display", "flex");

                // TODO: This top marging is currently being double applied, will have to investigate later
                //.Style("margin-top", $"{context.Config.GetSpacing(context.Config.Actions.Spacing)}px");

                // contains ShowCardAction.AdaptiveCard
                var showCards = new List<Element>();

                if (context.Config.Actions.ActionsOrientation == ActionsOrientation.Horizontal)
                {
                    uiButtonStrip.Style("flex-direction", "row");

                    switch (context.Config.Actions.ActionAlignment)
                    {
                        case AdaptiveHorizontalAlignment.Center:
                            uiButtonStrip.Style("justify-content", "center");
                            break;
                        case AdaptiveHorizontalAlignment.Right:
                            uiButtonStrip.Style("justify-content", "flex-end");
                            break;
                        default:
                            uiButtonStrip.Style("justify-content", "flex-start");
                            break;
                    }
                }
                else
                {
                    uiButtonStrip.Style("flex-direction", "column");
                    switch (context.Config.Actions.ActionAlignment)
                    {
                        case AdaptiveHorizontalAlignment.Center:
                            uiButtonStrip.Style("align-items", "center");
                            break;
                        case AdaptiveHorizontalAlignment.Right:
                            uiButtonStrip.Style("align-items", "flex-end");
                            break;
                        case AdaptiveHorizontalAlignment.Stretch:
                            uiButtonStrip.Style("align-items", "stretch");
                            break;
                        default:
                            uiButtonStrip.Style("align-items", "flex-start");
                            break;
                    }
                }

                var maxActions = Math.Min(context.Config.Actions.MaxActions, actions.Count);
                for (var i = 0; i < maxActions; i++)
                {
                    // add actions
                    var uiAction = context.Render(actions[i]);
                    if (uiAction != null)
                    {
                        if (actions[i] is AdaptiveShowCardAction showCardAction)
                        {
                            var cardId = uiAction.GetAttribute<string>("data-ac-showCardId", string.Empty);

                            var uiCard = context.Render(showCardAction.Card);
                            if (uiCard != null)
                            {
                                uiCard.SetAttr("id", cardId)
                                    .AddClass("ac-showCard")
                                    .Style("padding", "0")
                                    .Style("display", "none")
                                    .Style("margin-top", $"{context.Config.Actions.ShowCard.InlineTopMargin}px");

                                showCards.Add(uiCard);
                            }
                        }
                        uiButtonStrip.AppendChild(uiAction);
                    }

                    // add spacer between buttons according to config
                    if (i < maxActions - 1 && context.Config.Actions.ButtonSpacing > 0)
                    {
                        var uiSpacer = new Div();

                        if (context.Config.Actions.ActionsOrientation == ActionsOrientation.Horizontal)
                        {
                            uiSpacer.Style("flex", "0 0 auto");
                            uiSpacer.Style("width", context.Config.Actions.ButtonSpacing + "px");
                        }
                        else
                        {
                            uiSpacer.Style("height", context.Config.Actions.ButtonSpacing + "px");
                        }
                        uiButtonStrip.AppendChild(uiSpacer);
                    }
                }

                if (uiButtonStrip.Children.Any())
                {
                    ElementAdaptiveCardRenderer.AddSeparator(uiContainer, new AdaptiveContainer(), context);
                    uiContainer.AppendChild(uiButtonStrip);
                }

                foreach (var showCard in showCards)
                {
                    uiContainer.AppendChild(showCard);
                }
            }
        }

        protected static void AddSeparator(Element uiContainer, AdaptiveElement adaptiveElement, ElementAdaptiveRenderContext context)
        {
            if (!adaptiveElement.Separator && adaptiveElement.Spacing == AdaptiveSpacing.None)
            {
                return;
            }

            int spacing = context.Config.GetSpacing(adaptiveElement.Spacing);

            if (adaptiveElement.Separator)
            {
                SeparatorConfig sep = context.Config.Separator;
                var uiSep = new HR()
                        .AddClass("ac-separator")
                        .Style("padding-top", $"{spacing / 2}px")
                        .Style("margin-top", $"{spacing / 2}px")
                        .Style("border-top-color", $"{context.GetRGBColor(sep.LineColor)}")
                        .Style("border-top-width", $"{sep.LineThickness}px")
                        .Style("border-top-style", "solid");
                uiContainer.AppendChild(uiSep);
            }
            else 
            {
                var uiSep = new HR()
                    .AddClass("ac-separator")
                    .Style("height", $"{spacing}px");
                uiContainer.AppendChild(uiSep);
            }
        }

        protected static Element ColumnRender(AdaptiveColumn column, ElementAdaptiveRenderContext context)
        {
            var uiColumn = new Div()
                .AddClass($"ac-{column.Type.Replace(".", "").ToLower()}");

            AddContainerElements(uiColumn, column.Items, null, context);

            // selectAction
            if (context.Config.SupportsInteractivity && column.SelectAction != null)
            {
                uiColumn.AddClass("ac-selectable");
                AddActionAttributes(column.SelectAction, uiColumn, context);
            }

            return uiColumn;
        }

        protected static Element ColumnSetRender(AdaptiveColumnSet columnSet, ElementAdaptiveRenderContext context)
        {
            var uiColumnSet = new Div()
                .AddClass($"ac-{columnSet.Type.Replace(".", "").ToLower()}")
                .Style("overflow", "hidden")
                .Style("display", "flex");

            // selectAction
            if (context.Config.SupportsInteractivity && columnSet.SelectAction != null)
            {
                uiColumnSet.AddClass("ac-selectable");
                AddActionAttributes(columnSet.SelectAction, uiColumnSet, context);
            }

            var max = Math.Max(1.0, columnSet.Columns.Select(col =>
            {
                if (col.Width != null && double.TryParse(col.Width, out double widthVal))
                    return widthVal;
#pragma warning disable CS0618 // Type or member is obsolete
                if (double.TryParse(col.Size ?? "0", out double val))
#pragma warning restore CS0618 // Type or member is obsolete
                    return val;
                return 0;
            }).Sum());

            foreach (var column in columnSet.Columns)
            {
                var uiColumn = context.Render(column);

                // Add horizontal Seperator
                if (uiColumnSet.Children.Any() && (column.Separator || column.Spacing != AdaptiveSpacing.None))
                {
                    SeparatorConfig sep = context.Config.Separator;

                    int spacing = context.Config.GetSpacing(column.Spacing) / 2;
                    int lineThickness = column.Separator ? sep.LineThickness : 0;

                    if (sep != null)
                    {
                        uiColumnSet.AppendChild(new Div()
                            .AddClass($"ac-columnseparator")
                            .Style("flex", "0 0 auto")
                            .Style("padding-left", $"{spacing}px")
                            .Style("margin-left", $"{spacing}px")
                            .Style("border-left-color", $"{context.GetRGBColor(sep.LineColor)}")
                            .Style("border-left-width", $"{lineThickness}px")
                            .Style("border-left-style", $"solid"));
                    }
                }

                // do some sizing magic 
                var width = column.Width?.ToLower();
                if (string.IsNullOrEmpty(width))
#pragma warning disable CS0618 // Type or member is obsolete
                    width = column.Size?.ToLower();
#pragma warning restore CS0618 // Type or member is obsolete
                if (width == null || width == AdaptiveColumnWidth.Stretch.ToLower())
                {
                    uiColumn = uiColumn.Style("flex", "1 1 auto");
                }
                else if (width == AdaptiveColumnWidth.Auto.ToLower())
                {
                    uiColumn = uiColumn.Style("flex", "0 1 auto");
                }
                else
                {
                    double val;
                    if (double.TryParse(width, out val))
                    {
                        var percent = Convert.ToInt32(100 * (val / max));
                        uiColumn = uiColumn.Style("flex", $"1 1 {percent}%");
                    }
                    else
                    {
                        uiColumn = uiColumn.Style("flex", "0 0 auto");
                    }
                }

                uiColumnSet.AppendChild(uiColumn);
            }

            return uiColumnSet;
        }

        protected static Element ContainerRender(AdaptiveContainer container, ElementAdaptiveRenderContext context)
        {
            var uiContainer = new Div()
                .AddClass($"ac-{container.Type.Replace(".", "").ToLower()}");

            AddContainerElements(uiContainer, container.Items, null, context);

            if (context.Config.SupportsInteractivity && container.SelectAction != null)
            {
                uiContainer.AddClass("ac-selectable");
                AddActionAttributes(container.SelectAction, uiContainer, context);
            }

            return uiContainer;
        }

        protected static Element FactSetRender(AdaptiveFactSet factSet, ElementAdaptiveRenderContext context)
        {
            var uiFactSet = new List()
                .AddClass($"ac-{factSet.Type.Replace(".", "").ToLower()}")
                .Style("overflow", "hidden");
            
            foreach (var fact in factSet.Facts)
            {
                AdaptiveTextBlock factTitle = new AdaptiveTextBlock()
                {
                    Text = fact.Title,
                    Size = context.Config.FactSet.Title.Size,
                    Color = context.Config.FactSet.Title.Color,
                    Weight = context.Config.FactSet.Title.Weight,
                    IsSubtle = context.Config.FactSet.Title.IsSubtle,
                    Wrap = context.Config.FactSet.Title.Wrap,
                    MaxWidth = context.Config.FactSet.Title.MaxWidth
                };
                var uiTitle = context.Render(factTitle)
                    .AddClass("ac-facttitle")
                    .Style("margin-right", $"{context.Config.FactSet.Spacing}px");

                AdaptiveTextBlock factValue = new AdaptiveTextBlock()
                {
                    Text = fact.Value,
                    Size = context.Config.FactSet.Value.Size,
                    Color = context.Config.FactSet.Value.Color,
                    Weight = context.Config.FactSet.Value.Weight,
                    IsSubtle = context.Config.FactSet.Value.IsSubtle,
                    Wrap = context.Config.FactSet.Value.Wrap,
                    // MaxWidth is not supported on the Value of FactSet. Do not set it.
                };
                var uiValue = context.Render(factValue)
                    .AddClass("ac-factvalue");
                
                // create row in factset 
                var uiRow = new ListItem();
                uiRow.Style("height", "1px");
                
                // add elements as cells
                uiRow.AppendChild(new Heading(3)
                    .AddClass("ac-factset-titlecell").Style("height", "inherit")
                    .Style("max-width", $"{context.Config.FactSet.Title.MaxWidth}px")
                    .AppendChild(uiTitle));
                uiRow.AppendChild(new Span()
                    .AddClass("ac-factset-valuecell")
                    .Style("height", "inherit")
                    .AppendChild(uiValue));
                
                uiFactSet.AppendChild(uiRow);
            }
            return uiFactSet;
        }

        protected static Element TextBlockRender(AdaptiveTextBlock textBlock, ElementAdaptiveRenderContext context)
        {
            int fontSize;
            switch (textBlock.Size)
            {
                case AdaptiveTextSize.Small:
                    fontSize = context.Config.FontSizes.Small;
                    break;
                case AdaptiveTextSize.Medium:
                    fontSize = context.Config.FontSizes.Medium;
                    break;
                case AdaptiveTextSize.Large:
                    fontSize = context.Config.FontSizes.Large;
                    break;
                case AdaptiveTextSize.ExtraLarge:
                    fontSize = context.Config.FontSizes.ExtraLarge;
                    break;
                case AdaptiveTextSize.Default:
                default:
                    fontSize = context.Config.FontSizes.Default;
                    break;
            }
            int weight = 400;
            switch (textBlock.Weight)
            {
                case AdaptiveTextWeight.Lighter:
                    weight = 200;
                    break;

                case AdaptiveTextWeight.Bolder:
                    weight = 600;
                    break;
            }

            // Not sure where this magic value comes from?
            var lineHeight = fontSize * 1.33;
            
            var uiTextBlock = new Paragraph()
                .AddClass($"ac-{textBlock.Type.Replace(".", "").ToLower()}")
                .Style("box-sizing", "border-box")
                .Style("text-align", textBlock.HorizontalAlignment.ToString().ToLower())
                .Style("color", context.GetColor(textBlock.Color, textBlock.IsSubtle))
                .Style("line-height", $"{lineHeight.ToString("F")}px")
                .Style("font-size", $"{fontSize}px")
                .Style("font-weight", $"{weight}")
                .Style("height", "100%");
            uiTextBlock.Text = textBlock.Text;

            if (textBlock.MaxLines > 0)
                uiTextBlock = uiTextBlock
                    .Style("max-height", $"{lineHeight * textBlock.MaxLines}px")
                    .Style("overflow", "hidden");

            var setWrapStyleOnParagraph = false;
            if (textBlock.Wrap == false)
            {
                uiTextBlock = uiTextBlock
                    .Style("white-space", "nowrap");
                setWrapStyleOnParagraph = true;
            }
            else
            {
                uiTextBlock = uiTextBlock
                    .Style("word-wrap", "break-word");
            }

            var textTags = MarkdownToElementConverter.Convert(RendererUtilities.ApplyTextFunctions(textBlock.Text));
            textTags.ToList().ForEach(x => uiTextBlock.AppendChild(x));

            Action<Element> setParagraphStyles = null;
            setParagraphStyles = (Element element) =>
            {
                if (element.TagName?.ToLowerInvariant() == "p")
                {
                    element.Style("margin-top", "0px");
                    element.Style("margin-bottom", "0px");
                    element.Style("width", "100%");

                    if (setWrapStyleOnParagraph)
                    {
                        element.Style("text-overflow", "ellipsis");
                        element.Style("overflow", "hidden");
                    }
                }

                foreach (var child in element.Children.OfType<Element>())
                {
                    setParagraphStyles(child);
                }
            };

            setParagraphStyles(uiTextBlock);

            return uiTextBlock;
        }

        protected static Element ImageRender(AdaptiveImage image, ElementAdaptiveRenderContext context)
        {
            var uiDiv = new Div()
                .AddClass($"ac-{image.Type.Replace(".", "").ToLower()}")
                .Style("display", "block")
                .Style("box-sizing", "border-box");

            switch (image.Size)
            {
                case AdaptiveImageSize.Auto:
                    uiDiv = uiDiv.Style("max-width", $"100%");
                    break;
                case AdaptiveImageSize.Small:
                    uiDiv = uiDiv.Style("max-width", $"{context.Config.ImageSizes.Small}px");
                    break;
                case AdaptiveImageSize.Medium:
                    uiDiv = uiDiv.Style("max-width", $"{context.Config.ImageSizes.Medium}px");
                    break;
                case AdaptiveImageSize.Large:
                    uiDiv = uiDiv.Style("max-width", $"{context.Config.ImageSizes.Large}px");
                    break;
                case AdaptiveImageSize.Stretch:
                    uiDiv = uiDiv.Style("width", $"100%");
                    break;
            }

            var uiImage = new Image()
                .Style("width", "100%")
                .SetAttr("alt", image.AltText ?? "card image")
                .SetAttr("src", image.Url.ToString());

            switch (image.Style)
            {
                case AdaptiveImageStyle.Default:
                    break;
                case AdaptiveImageStyle.Person:
                    uiImage = uiImage.Style("background-position", "50% 50%")
                        .Style("border-radius", "50%")
                        .Style("background-repeat", "no-repeat");
                    break;
            }


            switch (image.HorizontalAlignment)
            {
                case AdaptiveHorizontalAlignment.Left:
                    uiDiv = uiDiv.Style("overflow", "hidden")
                        .Style("display", "block");
                    break;
                case AdaptiveHorizontalAlignment.Center:
                    uiDiv = uiDiv.Style("overflow", "hidden")
                        .Style("margin-right", "auto")
                        .Style("margin-left", "auto")
                        .Style("display", "block");
                    break;
                case AdaptiveHorizontalAlignment.Right:
                    uiDiv = uiDiv.Style("overflow", "hidden")
                        .Style("margin-left", "auto")
                        .Style("display", "block");
                    break;
            }
            uiDiv.AppendChild(uiImage);

            if (context.Config.SupportsInteractivity && image.SelectAction != null)
            {
                uiDiv.AddClass("ac-selectable");
                AddActionAttributes(image.SelectAction, uiDiv, context);
            }
            return uiDiv;
        }

        protected static Element ImageSetRender(AdaptiveImageSet imageSet, ElementAdaptiveRenderContext context)
        {
            var uiImageSet = new List()
                .AddClass(imageSet.Type.ToLower());

            foreach (var image in imageSet.Images)
            {
                if (imageSet.ImageSize != AdaptiveImageSize.Auto)
                    image.Size = imageSet.ImageSize;

                var uiImage = context.Render(image)
                    .Style("display", "inline-block")
                    .Style("margin-right", "10px");

                uiImageSet.AppendChild(uiImage);
            }
            return uiImageSet;
        }

        /// <summary>
        /// 1. IsMultiSelect == false && IsCompact == true => render as a drop down select element
        /// 2. IsMultiSelect == false && IsCompact == false => render as a list of radio buttons
        /// 3. IsMultiSelect == true => render as a list of toggle inputs
        /// </summary>
        protected static Element ChoiceSetRender(AdaptiveChoiceSetInput adaptiveChoiceSetInput, ElementAdaptiveRenderContext context)
        {
            if (!adaptiveChoiceSetInput.IsMultiSelect)
            {
                if (adaptiveChoiceSetInput.Style == AdaptiveChoiceInputStyle.Compact)
                {
                    var uiSelectElement = new Select()
                        .SetAttr("name", adaptiveChoiceSetInput.Id)
                        .AddClass("ac-input")
                        .AddClass("ac-multichoiceInput")
                        .Style("width", "100%");

                    foreach (var choice in adaptiveChoiceSetInput.Choices)
                    {
                        var option = new Option() { Text = choice.Title }
                            .SetAttr("value", choice.Value);

                        if (choice.Value == adaptiveChoiceSetInput.Value)
                        {
                            option.SetAttr("selected", string.Empty);
                        }
                        uiSelectElement.AppendChild(option);
                    }

                    return uiSelectElement;
                }
                else
                {
                    return ChoiceSetRenderInternal(adaptiveChoiceSetInput, context, "radio");
                }
            }
            else
            {
                return ChoiceSetRenderInternal(adaptiveChoiceSetInput, context, "checkbox");
            }
        }

        private static Element ChoiceSetRenderInternal(AdaptiveChoiceSetInput adaptiveChoiceSetInput, ElementAdaptiveRenderContext context, string htmlInputType)
        {
            // the default values are specified by a comma separated string input.value
            var defaultValues = adaptiveChoiceSetInput.Value?.Split(',').Select(p => p.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>();

            // render as a series of radio buttons
            var uiElement = new Input(InputType.Button)
                .AddClass("ac-input")
                .Style("width", "100%");

            foreach (var choice in adaptiveChoiceSetInput.Choices)
            {
                var htmlLabelId = GenerateRandomId();

                var uiInput = new Input(InputType.Radio)
                    .SetAttr("id", htmlLabelId)
                    .SetAttr("type", htmlInputType)
                    .SetAttr("name", adaptiveChoiceSetInput.Id)
                    .SetAttr("value", choice.Value)
                    .Style("margin", "0px")
                    .Style("display", "inline-block")
                    .Style("vertical-align", "middle");


                if (defaultValues.Contains(choice.Value))
                {
                    uiInput.SetAttr("checked", string.Empty);
                }

                var uiLabel = CreateLabel(htmlLabelId, choice.Title, context);

                var compoundInputElement = new Div()
                    .AppendChild(uiInput)
                    .AppendChild(uiLabel);

                uiElement.AppendChild(compoundInputElement);
            }

            return uiElement;

        }

        private static Element CreateLabel(string forId, string innerText, ElementAdaptiveRenderContext context)
        {
            var tag = new Label()
                .SetInnerText(innerText)
                .SetAttr("for", forId);
            ApplyDefaultTextAttributes(tag, context);
            return tag;
        }

        private static void ApplyDefaultTextAttributes(Element tag, ElementAdaptiveRenderContext context)
        {
            tag.Style("color", context.GetColor(AdaptiveTextColor.Default, false))
                .Style("font-size", $"{context.Config.FontSizes.Default}px")
                .Style("display", "inline-block")
                .Style("margin-left", "6px")
                .Style("vertical-align", "middle");
        }

        protected static Element DateInputRender(AdaptiveDateInput input, ElementAdaptiveRenderContext context)
        {
            var uiDateInput = new Input(InputType.Date)
                .SetAttr("name", input.Id)
                .SetAttr("type", "date")
                .AddClass("ac-input")
                .AddClass("ac-dateInput")
                .Style("width", "100%");

            if (!string.IsNullOrEmpty(input.Value))
            {
                uiDateInput.SetAttr("value", input.Value);
            }

            if (!string.IsNullOrEmpty(input.Min))
            {
                uiDateInput.SetAttr("min", input.Min);
            }

            if (!string.IsNullOrEmpty(input.Max))
            {
                uiDateInput.SetAttr("max", input.Max);
            }

            return uiDateInput;
        }

        protected static Element NumberInputRender(AdaptiveNumberInput input, ElementAdaptiveRenderContext context)
        {
            var uiNumberInput = new Input(InputType.Number)
                .SetAttr("name", input.Id)
                .AddClass("ac-input")
                .AddClass("ac-numberInput")
                .SetAttr("type", "number")
                .Style("width", "100%");

            if (!double.IsNaN(input.Min))
            {
                uiNumberInput.SetAttr("min", input.Min.ToString());
            }

            if (!double.IsNaN(input.Max))
            {
                uiNumberInput.SetAttr("max", input.Max.ToString());
            }

            if (!double.IsNaN(input.Value))
            {
                uiNumberInput.SetAttr("value", input.Value.ToString());
            }

            return uiNumberInput;
        }

        protected static Element TextInputRender(AdaptiveTextInput input, ElementAdaptiveRenderContext context)
        {
            Element uiTextInput;
            if (input.IsMultiline)
            {
                uiTextInput = new TextInput();

                if (!string.IsNullOrEmpty(input.Value))
                {
                    uiTextInput.Text = input.Value;
                }
            }
            else
            {
                uiTextInput = new TextInput().SetAttr("type", "text");

                if (!string.IsNullOrEmpty(input.Value))
                {
                    uiTextInput.SetAttr("value", input.Value);
                }
            }

            uiTextInput
                .SetAttr("name", input.Id)
                .AddClass("ac-textinput")
                .AddClass("ac-input")
                .Style("width", "100%");

            if (!string.IsNullOrEmpty(input.Placeholder))
            {
                uiTextInput.SetAttr("placeholder", input.Placeholder);
            }

            if (input.MaxLength > 0)
            {
                uiTextInput.SetAttr("maxLength", input.MaxLength.ToString());
            }

            return uiTextInput;
        }

        protected static Element TimeInputRender(AdaptiveTimeInput input, ElementAdaptiveRenderContext context)
        {
            var uiTimeInput = new Input(InputType.Time)
                .SetAttr("type", "time")
                .SetAttr("name", input.Id)
                .AddClass("ac-input")
                .AddClass("ac-timeInput")
                .Style("width", "100%");

            if (!string.IsNullOrEmpty(input.Value))
            {
                uiTimeInput.SetAttr("value", input.Value);
            }

            if (!string.IsNullOrEmpty(input.Min))
            {
                uiTimeInput.SetAttr("min", input.Min);
            }

            if (!string.IsNullOrEmpty(input.Max))
            {
                uiTimeInput.SetAttr("max", input.Max);
            }

            return uiTimeInput;
        }

        protected static Element ToggleInputRender(AdaptiveToggleInput toggleInput, ElementAdaptiveRenderContext context)
        {
            var htmlLabelId = GenerateRandomId();

            var uiElement = new Div()
                .AddClass("ac-input")
                .Style("width", "100%");

            var uiCheckboxInput = new Input(InputType.Checkbox)
                .SetAttr("id", htmlLabelId)
                .SetAttr("type", "checkbox")
                .SetAttr("name", toggleInput.Id)
                .SetAttr("data-ac-valueOn", toggleInput.ValueOn ?? bool.TrueString)
                .SetAttr("data-ac-valueOff", toggleInput.ValueOff ?? bool.FalseString)
                .Style("display", "inline-block")
                .Style("vertical-align", "middle")
                .Style("margin", "0px");

            if (toggleInput.Value == toggleInput.ValueOn)
            {
                uiCheckboxInput.SetAttr("checked", string.Empty);
            }

            var uiLabel = CreateLabel(htmlLabelId, toggleInput.Title, context);

            return (Element)uiElement.AppendChild(uiCheckboxInput).AppendChild(uiLabel);
        }

        protected static string GetFallbackText(AdaptiveElement adaptiveElement)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (!string.IsNullOrEmpty(adaptiveElement.Speak))
            {
#if NET452
                var doc = new System.Xml.XmlDocument();
                var xml = adaptiveElement.Speak;
                if (!xml.Trim().StartsWith("<"))
                    xml = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Speak>{xml}</Speak>";
                else if (!xml.StartsWith("<?xml "))
                    xml = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n{xml}";
                doc.LoadXml(xml);
                return doc.InnerText;
#endif
            }
#pragma warning restore CS0618 // Type or member is obsolete
            return null;
        }
    }
}
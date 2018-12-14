using System.Threading.Tasks;
using AdaptiveCards;
using AdaptiveCards.Rendering.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ooui.AspNetCore
{
    public class AdaptiveCardResult : ActionResult
    {
        readonly AdaptiveCard card;
        readonly string title;
        readonly bool disposeAfterSession;
        readonly ILogger logger;

        public AdaptiveCardResult(AdaptiveCard card, string title = "", bool disposeAfterSession = true, ILogger logger = null)
        {
            this.logger = logger;
            this.card = card;
            this.title = title;
            this.disposeAfterSession = disposeAfterSession;
        }

        public override Task ExecuteResultAsync (ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = 200;
            response.ContentType = "text/html; charset=utf-8";
            response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            return response.WriteAsync(new AdaptiveCardRenderer().RenderCard(card).Html.ToString());
        }

        static double GetCookieDouble (IRequestCookieCollection cookies, string key, double min, double def, double max)
        {
            if (cookies.TryGetValue (key, out var s)) {
                if (double.TryParse (s, out var d)) {
                    if (d < min) return min;
                    if (d > max) return max;
                    return d;
                }
                return def;
            }
            else {
                return def;
            }
        }
    }
}

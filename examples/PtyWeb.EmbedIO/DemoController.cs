using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;

namespace PtyWeb.EmbedIO
{
    public class DemoController : WebApiController
    {
        [Route(HttpVerbs.Get, "/")]
        public string GetDemoIndex() => "hello world";
    }
}

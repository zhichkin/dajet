using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("")]
    public class HomeController : ControllerBase
    {
        public HomeController() { }
        [HttpGet()] public ContentResult Home()
        {
            string root = AppContext.BaseDirectory;
            string filePath = Path.Combine(root, "pages", "home.html");

            FileInfo info = new(filePath);

            if (!info.Exists)
            {
                return new ContentResult()
                {
                    ContentType = "text/html",
                    StatusCode = (int)HttpStatusCode.NotFound,
                    Content = "<html><body>Home page is not found!</body></html>"
                };
            }

            string content = string.Empty;

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                content = reader.ReadToEnd();
            }
            
            return new ContentResult()
            {
                ContentType = "text/html",
                StatusCode = (int)HttpStatusCode.OK,
                Content = content
            };
        }
        [HttpGet("ui/home/js")] public ContentResult LoadJavaScript()
        {
            string root = AppContext.BaseDirectory;
            string filePath = Path.Combine(root, "pages", "home.js");

            FileInfo info = new(filePath);

            if (!info.Exists)
            {
                return new ContentResult()
                {
                    ContentType = "text/html",
                    StatusCode = (int)HttpStatusCode.NotFound,
                    Content = "<html><body>JavaScript is not found!</body></html>"
                };
            }

            string content = string.Empty;

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                content = reader.ReadToEnd();
            }

            return new ContentResult()
            {
                ContentType = "text/javascript",
                StatusCode = (int)HttpStatusCode.OK,
                Content = content
            };
        }
        [HttpGet("ui/home/css")] public ContentResult LoadStyleSheet()
        {
            string root = AppContext.BaseDirectory;
            string filePath = Path.Combine(root, "pages", "home.css");

            FileInfo info = new(filePath);

            if (!info.Exists)
            {
                return new ContentResult()
                {
                    ContentType = "text/html",
                    StatusCode = (int)HttpStatusCode.NotFound,
                    Content = "<html><body>Style sheet is not found!</body></html>"
                };
            }

            string content = string.Empty;

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                content = reader.ReadToEnd();
            }

            return new ContentResult()
            {
                ContentType = "text/css",
                StatusCode = (int)HttpStatusCode.OK,
                Content = content
            };
        }
    }
}
using DaJet.Json;
using DaJet.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("code")] public class DaJetCodeController : ControllerBase
    {
        private readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private readonly IFileProvider _fileProvider;
        public DaJetCodeController(IFileProvider fileProvider)
        {
            _fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));

            JsonOptions.Converters.Add(new DictionaryJsonConverter());
        }
        [HttpGet("dir/{**path}")] public ActionResult GetCodeItems([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            IDirectoryContents folder = _fileProvider.GetDirectoryContents(path);

            if (!folder.Exists)
            {
                return NotFound();
            }

            int count = folder.Count();

            List<CodeItem> items = new(count);

            foreach (IFileInfo info in folder)
            {
                items.Add(new CodeItem()
                {
                    Name = info.Name,
                    IsFolder = info.IsDirectory
                });
            }

            string json = JsonSerializer.Serialize(items, JsonOptions);

            return Content(json);
        }
        [HttpGet("src/{**path}")] public ActionResult GetSourceCode([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            IFileInfo file = _fileProvider.GetFileInfo(path);

            if (!file.Exists)
            {
                return NotFound();
            }
            
            string content = System.IO.File.ReadAllText(file.PhysicalPath, Encoding.UTF8);

            return Content(content, "text/plain", Encoding.UTF8);
        }
    }
}
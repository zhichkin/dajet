using DaJet.Json;
using DaJet.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("code")] public class CodeController : ControllerBase
    {
        private readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private readonly IFileProvider _fileProvider;
        public CodeController(IFileProvider fileProvider)
        {
            _fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));

            JsonOptions.Converters.Add(new DictionaryJsonConverter());
        }
        [HttpGet("{**path}")] public ActionResult GetCodeItems([FromRoute] string path)
        {
            string directory = string.IsNullOrWhiteSpace(path) ? "code" : Path.Combine("code", path);

            IDirectoryContents folder = _fileProvider.GetDirectoryContents(directory);

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
    }
}
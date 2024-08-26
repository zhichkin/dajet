using DaJet.Json;
using DaJet.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using System.Buffers;
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
        [HttpPut("src/{**path}")] public async Task<ActionResult> SaveSourceCode([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            if (_fileProvider is not PhysicalFileProvider provider)
            {
                return BadRequest();
            }

            string filePath = Path.Combine(provider.Root, path);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            string sourceCode = await GetRequestBodyAsString(HttpContext.Request) ?? string.Empty;

            try
            {
                using (StreamWriter writer = System.IO.File.CreateText(filePath))
                {
                    writer.Write(sourceCode);
                }
            }
            catch (Exception error)
            {
                return Problem(ExceptionHelper.GetErrorMessageAndStackTrace(error));
            }

            return Ok();
        }
        private static async Task<string> GetRequestBodyAsString(HttpRequest request)
        {
            if (request.ContentLength == 0) { return null; }

            int size = (int)request.ContentLength.Value;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(size);

            _ = await request.Body.ReadAsync(buffer, 0, size);

            string value = Encoding.UTF8.GetString(buffer, 0, size);

            ArrayPool<byte>.Shared.Return(buffer);

            return value;
        }
        
        [HttpPost("script/{**path}")] public ActionResult CreateScript([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            IFileInfo file = _fileProvider.GetFileInfo(path);

            if (file.Exists)
            {
                return BadRequest();
            }

            if (_fileProvider is not PhysicalFileProvider provider)
            {
                return BadRequest();
            }

            string filePath = Path.Combine(provider.Root, path);

            try
            {
                using (StreamWriter writer = System.IO.File.CreateText(filePath))
                {
                    writer.WriteLine("// Write DaJet code here");
                }
            }
            catch (Exception error)
            {
                return Problem(ExceptionHelper.GetErrorMessageAndStackTrace(error));
            }
            
            return Created(path, path);
        }
        [HttpDelete("script/{**path}")] public ActionResult DeleteScript([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            IFileInfo file = _fileProvider.GetFileInfo(path);

            if (!file.Exists)
            {
                return BadRequest();
            }

            if (_fileProvider is not PhysicalFileProvider provider)
            {
                return BadRequest();
            }

            string filePath = Path.Combine(provider.Root, path);

            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception error)
            {
                return Problem(ExceptionHelper.GetErrorMessageAndStackTrace(error));
            }

            return Ok();
        }
        [HttpPut("script/{**path}")] public async Task<ActionResult> RenameScript([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            if (_fileProvider is not PhysicalFileProvider provider)
            {
                return BadRequest();
            }

            string filePath = Path.Combine(provider.Root, path);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            string name = await GetRequestBodyAsString(HttpContext.Request);

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest();
            }

            string folder = Path.GetDirectoryName(filePath);
            string newPath = Path.Combine(folder, name);

            if (System.IO.File.Exists(newPath))
            {
                return BadRequest();
            }

            System.IO.File.Move(filePath, newPath);

            return Ok();
        }
        [HttpPatch("script/{**path}")] public async Task<ActionResult> MoveScript([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            if (_fileProvider is not PhysicalFileProvider provider)
            {
                return BadRequest();
            }

            string sourcePath = Path.Combine(provider.Root, path);

            if (!System.IO.File.Exists(sourcePath))
            {
                return NotFound();
            }

            string target = await GetRequestBodyAsString(HttpContext.Request);

            if (string.IsNullOrWhiteSpace(target))
            {
                return BadRequest();
            }

            target = target.StartsWith('/') ? target[1..] : target;

            string targetPath = Path.Combine(provider.Root, target);

            if (!Directory.Exists(targetPath))
            {
                return NotFound();
            }

            targetPath = Path.Combine(targetPath, Path.GetFileName(sourcePath));

            System.IO.File.Move(sourcePath, targetPath);

            return Ok();
        }
        
        [HttpPost("folder/{**path}")] public ActionResult CreateFolder([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            IFileInfo file = _fileProvider.GetFileInfo(path);

            if (file.Exists)
            {
                return BadRequest();
            }

            if (_fileProvider is not PhysicalFileProvider provider)
            {
                return BadRequest();
            }

            try
            {
                string fullPath = Path.Combine(provider.Root, path);

                _ = Directory.CreateDirectory(fullPath);
            }
            catch (Exception error)
            {
                return Problem(ExceptionHelper.GetErrorMessageAndStackTrace(error));
            }

            return Created(path, path);
        }
        [HttpDelete("folder/{**path}")] public ActionResult DeleteFolder([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            IDirectoryContents folder = _fileProvider.GetDirectoryContents(path);

            if (!folder.Exists)
            {
                return BadRequest();
            }

            if (_fileProvider is not PhysicalFileProvider provider)
            {
                return BadRequest();
            }

            try
            {
                string fullPath = Path.Combine(provider.Root, path);

                Directory.Delete(fullPath, true);
            }
            catch (Exception error)
            {
                return Problem(ExceptionHelper.GetErrorMessageAndStackTrace(error));
            }

            return Ok();
        }
        [HttpPut("folder/{**path}")] public async Task<ActionResult> RenameFolder([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            if (_fileProvider is not PhysicalFileProvider provider)
            {
                return BadRequest();
            }

            string fullPath = Path.Combine(provider.Root, path);

            if (!Directory.Exists(fullPath))
            {
                return NotFound();
            }

            string name = await GetRequestBodyAsString(HttpContext.Request);

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest();
            }

            string folder = Path.GetDirectoryName(fullPath);
            string newPath = Path.Combine(folder, name);

            if (Directory.Exists(newPath))
            {
                return BadRequest();
            }

            Directory.Move(fullPath, newPath);

            return Ok();
        }
        [HttpPatch("folder/{**path}")] public async Task<ActionResult> MoveFolder([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            if (_fileProvider is not PhysicalFileProvider provider)
            {
                return BadRequest();
            }

            string sourcePath = Path.Combine(provider.Root, path);

            if (!Directory.Exists(sourcePath))
            {
                return NotFound();
            }

            string target = await GetRequestBodyAsString(HttpContext.Request);

            if (string.IsNullOrWhiteSpace(target))
            {
                return BadRequest();
            }

            target = target.StartsWith('/') ? target[1..] : target;

            string targetPath = Path.Combine(provider.Root, target);

            if (!Directory.Exists(targetPath))
            {
                return NotFound();
            }

            targetPath = Path.Combine(targetPath, Path.GetFileName(sourcePath));

            Directory.Move(sourcePath, targetPath);

            return Ok();
        }

        [HttpPost("src/{**path}")] public async Task<ActionResult> ExecuteScript([FromRoute] string path)
        {
            string sourceCode = await GetRequestBodyAsString(HttpContext.Request) ?? string.Empty;

            //TODO: execute DaJet Stream script

            return Ok();
        }
    }
}
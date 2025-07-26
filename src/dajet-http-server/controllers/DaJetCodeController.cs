using DaJet.Http.Server;
using DaJet.Json;
using DaJet.Model;
using DaJet.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("dajet")] public class DaJetCodeController : ControllerBase
    {
        private const string DAJET_SCRIPT_ROOT_FOLDER = "code";

        private readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private readonly IFileProvider _fileProvider;
        public DaJetCodeController(IFileProvider fileProvider)
        {
            _fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));

            JsonOptions.Converters.Add(new DataObjectJsonConverter());
            JsonOptions.Converters.Add(new DictionaryJsonConverter());
        }
        [HttpGet("ping")] public ActionResult Ping()
        {
            string content = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:fff");

            return Content(content, "text/plain", Encoding.UTF8);
        }
        [HttpGet("log")] public ActionResult GetServerLog()
        {
            IFileInfo file = _fileProvider.GetFileInfo("dajet.log");

            string content = string.Empty;

            if (file.Exists)
            {
                content = System.IO.File.ReadAllText(file.PhysicalPath, Encoding.UTF8);
            }

            return Content(content, "text/plain", Encoding.UTF8);
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

        #region "FILE SYSTEM OPERATIONS"
        [HttpGet("src/{**path}")] public ActionResult GetSourceCode([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            path = DAJET_SCRIPT_ROOT_FOLDER + "/" + path;

            IFileInfo file = _fileProvider.GetFileInfo(path);

            if (!file.Exists)
            {
                return NotFound();
            }
            
            string content = System.IO.File.ReadAllText(file.PhysicalPath, Encoding.UTF8);

            return Content(content, "text/plain", Encoding.UTF8);
        }
        [HttpPost("src/{**path}")] public async Task<ActionResult> CreateOrSaveScript([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            path = DAJET_SCRIPT_ROOT_FOLDER + "/" + path;

            if (_fileProvider is not PhysicalFileProvider provider)
            {
                return BadRequest();
            }

            string filePath = Path.Combine(provider.Root, path);

            bool created = !System.IO.File.Exists(filePath);

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

            return created ? Created(path, path) : Ok();
        }
        [HttpDelete("src/{**path}")] public ActionResult DeleteScript([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            path = DAJET_SCRIPT_ROOT_FOLDER + "/" + path;

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
        [HttpPut("src/{**path}")] public async Task<ActionResult> RenameScript([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            path = DAJET_SCRIPT_ROOT_FOLDER + "/" + path;

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
        [HttpPatch("src/{**path}")] public async Task<ActionResult> MoveScript([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            path = DAJET_SCRIPT_ROOT_FOLDER + "/" + path;

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
                target = DAJET_SCRIPT_ROOT_FOLDER;
            }
            else
            {
                target = DAJET_SCRIPT_ROOT_FOLDER + (target.StartsWith('/') ? target : '/' + target);
            }

            string targetPath = Path.Combine(provider.Root, target);

            if (!Directory.Exists(targetPath))
            {
                return NotFound();
            }

            targetPath = Path.Combine(targetPath, Path.GetFileName(sourcePath));

            System.IO.File.Move(sourcePath, targetPath);

            return Ok();
        }

        [HttpGet("dir/{**path}")] public ActionResult GetFolderItems([FromRoute] string path)
        {
            string root = DAJET_SCRIPT_ROOT_FOLDER;

            if (!string.IsNullOrWhiteSpace(path))
            {
                root += "/" + path;
            }

            IDirectoryContents folder = _fileProvider.GetDirectoryContents(root);

            if (!folder.Exists)
            {
                return NotFound();
            }

            List<CodeItem> files = new();
            List<CodeItem> catalogs = new();

            foreach (IFileInfo info in folder)
            {
                if (info.IsDirectory)
                {
                    catalogs.Add(new CodeItem()
                    {
                        Name = info.Name,
                        IsFolder = info.IsDirectory
                    });
                }
                else
                {
                    files.Add(new CodeItem()
                    {
                        Name = info.Name,
                        IsFolder = info.IsDirectory
                    });
                }
            }

            files.AddRange(catalogs);

            string json = JsonSerializer.Serialize(files, JsonOptions);

            return Content(json);
        }
        [HttpPost("dir/{**path}")] public ActionResult CreateFolder([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            path = DAJET_SCRIPT_ROOT_FOLDER + "/" + path;

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
        [HttpDelete("dir/{**path}")] public ActionResult DeleteFolder([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            path = DAJET_SCRIPT_ROOT_FOLDER + "/" + path;

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
        [HttpPut("dir/{**path}")] public async Task<ActionResult> RenameFolder([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            path = DAJET_SCRIPT_ROOT_FOLDER + "/" + path;

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
        [HttpPatch("dir/{**path}")] public async Task<ActionResult> MoveFolder([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            path = DAJET_SCRIPT_ROOT_FOLDER + "/" + path;

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
                target = DAJET_SCRIPT_ROOT_FOLDER;
            }
            else
            {
                target = DAJET_SCRIPT_ROOT_FOLDER + (target.StartsWith('/') ? target : '/' + target);
            }

            string targetPath = Path.Combine(provider.Root, target);

            if (!Directory.Exists(targetPath))
            {
                return NotFound();
            }

            targetPath = Path.Combine(targetPath, Path.GetFileName(sourcePath));

            Directory.Move(sourcePath, targetPath);

            return Ok();
        }
        #endregion

        [HttpPost("exe/{**path}")] public async Task<ActionResult> ExecuteScriptAsync([FromRoute] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest();
            }

            path = DAJET_SCRIPT_ROOT_FOLDER + "/" + path;

            //TODO: cache script processors !?
            //if (ScriptHost.Default.TryRun(in path, out string error))
            //{
            //    return Ok();
            //}
            //else
            //{
            //    return BadRequest();
            //}

            IFileInfo file = _fileProvider.GetFileInfo(path);

            if (!file.Exists)
            {
                return NotFound();
            }

            Dictionary<string, object> parameters = await HttpContext.Request.GetParametersFromBody();

            if (!StreamManager.TryExecute(file.PhysicalPath, in parameters, out object result, out string error))
            {
                return UnprocessableEntity(error); // 422
            }

            if (result is null)
            {
                return Ok();
            }

            string json = JsonSerializer.Serialize(result, result.GetType(), JsonOptions);

            return Content(json);
        }
    }
}
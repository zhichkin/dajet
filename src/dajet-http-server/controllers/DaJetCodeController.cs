﻿using DaJet.Json;
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
        [HttpPut("script/{**path}")] public ActionResult RenameScript([FromRoute] string path, [FromBody] string name)
        {
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
    }
}
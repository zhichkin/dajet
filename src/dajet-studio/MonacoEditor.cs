using DaJet.Studio.Pages.Code;
using Microsoft.JSInterop;
using System.Text.Json;

namespace DaJet.Studio
{
    public sealed class MonacoEditor
    {
        private IJSRuntime Runtime { get; }
        public MonacoEditor(IJSRuntime runtime)
        {
            Runtime = runtime;
        }
        internal async Task CreateMonacoEditor(DaJetCodeEditor editor, string code)
        {
            await Runtime.InvokeVoidAsync("CreateMonacoEditor", code);

            _editor = editor;
        }
        internal async Task DisposeMonacoEditor()
        {
            await Runtime.InvokeVoidAsync("DisposeMonacoEditor");

            _editor = null;
        }
        internal async Task<string> GetMonacoEditorValue()
        {
            return await Runtime.InvokeAsync<string>("GetMonacoEditorValue");
        }
        private static DaJetCodeEditor _editor;
        [JSInvokable] public static Task MonacoEditor_OnValueChanged(JsonElement element)
        {
            _editor?.OnScriptChanged(element);
            
            return Task.CompletedTask;
        }
    }
}
using Microsoft.JSInterop;
using System.Text.Json;

namespace DaJet.Studio
{
    public sealed class MonacoEditor
    {
        private static IScriptEditor _editor;
        private IJSRuntime Runtime { get; }
        public MonacoEditor(IJSRuntime runtime)
        {
            Runtime = runtime;
        }
        internal async Task CreateMonacoEditor(IScriptEditor editor, string code)
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
        [JSInvokable] public static Task MonacoEditor_OnValueChanged(JsonElement element)
        {
            return _editor?.OnScriptChanged(element);
        }
    }
}
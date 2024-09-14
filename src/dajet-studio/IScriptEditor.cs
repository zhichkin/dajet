using System.Text.Json;

namespace DaJet.Studio
{
    public interface IScriptEditor : IAsyncDisposable
    {
        Task OnScriptChanged(JsonElement element);
    }
}
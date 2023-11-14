using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Components.AutoComplete
{
    public partial class AutoComplete : ComponentBase
    {
        private List<string> Values { get; set; }
        [Parameter] public EventCallback<string> ValueSelected { get; set; }
        [Parameter] public Func<string, Task<List<string>>> ValuesProvider { get; set; }
        private async Task OnValueChanged(ChangeEventArgs args)
        {
            Values = await ValuesProvider(args.Value.ToString());
        }
        private async Task ConfirmInputValue(string value)
        {
            Values = null;
            await ValueSelected.InvokeAsync(value);
        }
    }
}
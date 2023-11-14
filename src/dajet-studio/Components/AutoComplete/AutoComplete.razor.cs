using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Components.AutoComplete
{
    public partial class AutoComplete : ComponentBase
    {
        private string _value;
        private List<string> Values { get; set; }
        [Parameter] public EventCallback<string> ValueSelected { get; set; }
        [Parameter] public Func<string, Task<List<string>>> ValuesProvider { get; set; }
        private async Task OnValueChanged(ChangeEventArgs args)
        {
            Values = await ValuesProvider(args.Value.ToString());
        }
        private async Task ConfirmInputValue(string value)
        {
            _value = null; // clear input value
            Values = null; // close selection list
            await ValueSelected.InvokeAsync(value);
        }
        private async Task ToggleButtonClick()
        {
            if (Values is null)
            {
                Values = await ValuesProvider(string.Empty);
            }
            else
            {
                _value = null; // clear input value
                Values = null; // close selection list
            }
        }
    }
}
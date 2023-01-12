using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DaJet.Studio.Components
{
    public partial class TreeNode : ComponentBase
    {
        [Parameter] public TreeNodeModel Model { get; set; }
        private async Task ToggleClick(MouseEventArgs args)
        {
            if (Model != null && Model.UseToggle)
            {
                await Model?.ToggleCommand();
            }
        }
        private async Task OpenContextMenu(MouseEventArgs args)
        {
            await Model?.ContextMenuCommand(DialogService);
        }
    }
}
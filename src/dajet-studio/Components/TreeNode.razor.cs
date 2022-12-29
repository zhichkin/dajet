using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DaJet.Studio.Components
{
    public partial class TreeNode : ComponentBase
    {
        [Parameter] public TreeNodeModel Model { get; set; }
        private async Task OpenCloseClick(MouseEventArgs args)
        {
            await Model?.OpenCloseCommand();
        }
    }
}
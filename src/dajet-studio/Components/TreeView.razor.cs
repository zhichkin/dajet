using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Components
{
    public partial class TreeView : ComponentBase
    {
        [Parameter] public List<TreeNodeModel> Nodes { get; set; } = new();
    }
}
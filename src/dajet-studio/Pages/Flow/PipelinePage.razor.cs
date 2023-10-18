using DaJet.Model;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Flow
{
    public partial class PipelinePage : ComponentBase
    {
        private TreeNodeRecord _folder;
        private TreeNodeRecord _record;
        [Parameter] public Guid Node { get; set; }
        [Parameter] public Guid Uuid { get; set; }
        private string TreeNodeName { get; set; }
        private string PipelineName { get; set; }
        private bool IsNewPipeline { get; set; } = true;
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected override async Task OnParametersSetAsync()
        {
            int typeCode = DomainModel.GetTypeCode(typeof(TreeNodeRecord));

            _folder = await DataSource.SelectAsync(new Entity(typeCode, Node)) as TreeNodeRecord;

            if (_folder is not null)
            {
                TreeNodeName = await DataSource.GetTreeNodeFullName(_folder);
            }

            _record = await DataSource.SelectAsync(new Entity(typeCode, Uuid)) as TreeNodeRecord;

            if (_record is not null)
            {
                PipelineName = _record.Name;
            }
            
            IsNewPipeline = (Uuid == Guid.Empty);
        }
    }
}
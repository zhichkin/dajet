using DaJet.Dto.Client;
using DaJet.Flow.Model;
using DaJet.Model;
using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace DaJet.Studio.Controllers
{
    public sealed class FlowTreeViewController
    {
        private HttpClient Http { get; set; }
        private DataSource DataSource { get; set; }
        private IJSRuntime JSRuntime { get; set; }
        private NavigationManager Navigator { get; set; }
        public FlowTreeViewController(HttpClient http, IJSRuntime js, NavigationManager navigator, DataSource source)
        {
            Http = http;
            JSRuntime = js;
            Navigator = navigator;
            DataSource = source;
        }
        public TreeNodeModel CreateRootNode()
        {
            return new TreeNodeModel()
            {
                Url = $"/flow",
                Title = "flow",
                OpenNodeHandler = OpenRootNodeHandler
            };
        }
        private async Task OpenRootNodeHandler(TreeNodeModel root)
        {
            if (root is null || root.Nodes.Count > 0)
            {
                return;
            }

            QueryObject query = new()
            {
                Query = "SELECT uuid, parent, name, is_folder, entity_type, entity_uuid FROM maintree WHERE name = @name;",
                Parameters = new()
                {
                    { "name", "test тест" }
                }
            };

            List<EntityObject> records = await DataSource.SelectAsync(query);

            try
            {
                HttpResponseMessage response = await Http.GetAsync(root.Url);

                if (response.IsSuccessStatusCode)
                {
                    List<PipelineInfo> pipelines = await response.Content.ReadFromJsonAsync<List<PipelineInfo>>();

                    CreateRootNodeChildren(in root, in pipelines);
                }
                else
                {
                    //TODO: show error message

                    string result = await response.Content?.ReadAsStringAsync();

                    string error = response.ReasonPhrase
                        + (string.IsNullOrEmpty(result)
                        ? string.Empty
                        : Environment.NewLine + result);
                }
            }
            catch (Exception error)
            {
                //TODO: show error message
            }
        }
        private void CreateRootNodeChildren(in TreeNodeModel root, in List<PipelineInfo> pipelines)
        {
            foreach (PipelineInfo model in pipelines)
            {
                root.Nodes.Add(CreatePipelineNode(in root, in model));
            }
        }
        private TreeNodeModel CreatePipelineNode(in TreeNodeModel parent, in PipelineInfo model)
        {
            TreeNodeModel node = new()
            {
                Parent = parent,
                Tag = model,
                Title = model.Name,
                UseToggle = false,
                Url = $"{parent.Url}/{model.Uuid.ToString().ToLower()}"
            };

            return node;
        }
        private void NavigateToPipelinePage()
        {
            // /dajet-flow/pipeline/{uuid:guid?}
        }
    }
}
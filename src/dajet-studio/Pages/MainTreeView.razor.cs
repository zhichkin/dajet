using DaJet.Studio.Model;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages
{
    public partial class MainTreeView : ComponentBase
    {
        private const string NODE_TYPE_CONFIGURATION = "Конфигурация";
        private const string NODE_TYPE_EXTENSIONS = "Расширения";
        private const string NODE_TYPE_CATALOG = "Справочник";
        private const string NODE_TYPE_DOCUMENT = "Документ";
        private const string NODE_TYPE_INFOREGISTER = "РегистрСведений";
        private const string NODE_TYPE_ACCUMREGISTER = "РегистрНакопления";
        [Inject] protected HttpClient Http { get; set; }
        protected HashSet<TreeNodeModel> RootNodes { get; set; } = new();
        protected override async Task OnInitializedAsync()
        {
            RootNodes.Clear();

            try
            {
                HttpResponseMessage response = await Http.GetAsync("/md");

                List<InfoBaseModel> list = await response.Content.ReadFromJsonAsync<List<InfoBaseModel>>();

                foreach (InfoBaseModel model in list)
                {
                    TreeNodeModel node = new() { CanExpand = true };
                    ConfigureInfoBaseNode(in node, in model);
                    RootNodes.Add(node);
                }
            }
            catch (Exception error)
            {
                RootNodes.Add(new TreeNodeModel()
                {
                    Icon = Icons.Filled.Error,
                    Title = "Ошибка загрузки данных!"
                });
            }
        }
        private void ConfigureInfoBaseNode(in TreeNodeModel node, in InfoBaseModel model)
        {
            node.Model = model;
            node.Title = model.Name;

            node.Nodes.Add(new TreeNodeModel()
            {
                Model = model,
                Title = NODE_TYPE_EXTENSIONS,
                Icon = Icons.Material.Filled.Extension,
                CanExpand = true
            });

            TreeNodeModel configuration = new()
            {
                Model = model,
                Title = NODE_TYPE_CONFIGURATION,
                Icon = Icons.Material.Outlined.Settings,
                CanExpand = true
            };
            
            ConfigureConfigurationTreeNode(in configuration);
            
            node.Nodes.Add(configuration);
        }
        private void ConfigureConfigurationTreeNode(in TreeNodeModel parent)
        {
            parent.Nodes.Add(new TreeNodeModel()
            {
                Model = parent.Model,
                Title = NODE_TYPE_CATALOG,
                //Icon = Icons.Material.Outlined.Folder,
                CanExpand = true
            });
            parent.Nodes.Add(new TreeNodeModel()
            {
                Model = parent.Model,
                Title = NODE_TYPE_DOCUMENT,
                //Icon = Icons.Material.Outlined.Folder,
                CanExpand = true
            });
            parent.Nodes.Add(new TreeNodeModel()
            {
                Model = parent.Model,
                Title = NODE_TYPE_INFOREGISTER,
                //Icon = Icons.Material.Outlined.Folder,
                CanExpand = true
            });
            parent.Nodes.Add(new TreeNodeModel()
            {
                Model = parent.Model,
                Title = NODE_TYPE_ACCUMREGISTER,
                //Icon = Icons.Material.Outlined.Folder,
                CanExpand = true
            });
        }
        public async Task<HashSet<TreeNodeModel>> GetTreeNodeItems(TreeNodeModel node)
        {
            if (node.Model is InfoBaseModel infoBase)
            {
                if (node.Title == NODE_TYPE_EXTENSIONS)
                {
                    node.Nodes = await GetExtensions(infoBase);
                }
                else
                {
                    //TODO
                }
            }

            return node.Nodes;
        }
        private async Task<HashSet<TreeNodeModel>> GetExtensions(InfoBaseModel infoBase)
        {
            HashSet<TreeNodeModel> result = new();

            try
            {
                HttpResponseMessage response = await Http.GetAsync("/mdex/" + infoBase.Name);

                List<ExtensionModel> list = await response.Content.ReadFromJsonAsync<List<ExtensionModel>>();

                foreach (ExtensionModel model in list)
                {
                    TreeNodeModel node = new()
                    {
                        Model = model,
                        Title = model.Name,
                        CanExpand = true
                    };
                    
                    ConfigureConfigurationTreeNode(in node);

                    result.Add(node);
                }
            }
            catch (Exception error)
            {
                //TODO: show error
            }

            return result;
        }
        private HashSet<TreeNodeModel> GetMetadataObjects(TreeNodeModel node)
        {
            HashSet<TreeNodeModel> result = new();

            //TODO

            return result;
        }
    }
}
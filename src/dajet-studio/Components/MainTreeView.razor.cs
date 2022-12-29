using DaJet.Studio.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using System.Net.Http.Json;

namespace DaJet.Studio.Components
{
    public partial class MainTreeView : ComponentBase
    {
        public const string NODE_TYPE_CONFIGURATION = "Конфигурация";
        public const string NODE_TYPE_EXTENSIONS = "Расширения";
        public const string NODE_TYPE_CATALOG = "Справочник";
        public const string NODE_TYPE_DOCUMENT = "Документ";
        public const string NODE_TYPE_INFOREGISTER = "РегистрСведений";
        public const string NODE_TYPE_ACCUMREGISTER = "РегистрНакопления";
        protected List<TreeNodeModel> Nodes { get; set; } = new();
        protected override async Task OnInitializedAsync()
        {
            await IntializeData();
        }
        protected async Task Refresh(MouseEventArgs args)
        {
            await IntializeData();
        }
        private async Task IntializeData()
        {
            try
            {
                Nodes.Clear();

                HttpResponseMessage response = await Http.GetAsync("/md");

                List<InfoBaseModel> list = await response.Content.ReadFromJsonAsync<List<InfoBaseModel>>();

                foreach (InfoBaseModel model in list)
                {
                    TreeNodeModel node = new();

                    ConfigureInfoBaseNode(in node, in model);

                    Nodes.Add(node);
                }
            }
            catch (Exception error)
            {
                Nodes.Add(new TreeNodeModel()
                {
                    Icon = Icons.Filled.Error,
                    Title = "Ошибка загрузки данных!"
                });
            }
        }
        public void ConfigureInfoBaseNode(in TreeNodeModel node, in InfoBaseModel model)
        {
            node.Tag = model;
            node.Url = $"/md/{model.Name}";
            node.Title = model.Name;
            node.OpenNodeHandler = OpenNodeHandler;

            node.Nodes.Add(new TreeNodeModel()
            {
                Tag = model,
                Url = $"/mdex/{model.Name}",
                Title = NODE_TYPE_EXTENSIONS,
                Icon = Icons.Material.Filled.Extension,
                OpenNodeHandler = OpenNodeHandler
            });

            TreeNodeModel configuration = new()
            {
                Tag = model,
                Url = node.Url,
                Title = NODE_TYPE_CONFIGURATION,
                Icon = Icons.Material.Outlined.Settings,
                OpenNodeHandler = OpenNodeHandler
            };

            ConfigureConfigurationTreeNode(in configuration);

            node.Nodes.Add(configuration);
        }
        public void ConfigureConfigurationTreeNode(in TreeNodeModel parent)
        {
            parent.Nodes.Add(new TreeNodeModel()
            {
                Tag = parent.Tag,
                Url = $"{parent.Url}/{NODE_TYPE_CATALOG}",
                Title = NODE_TYPE_CATALOG,
                Type = TreeNodeType.Catalog,
                OpenNodeHandler = OpenNodeHandler
            });
            parent.Nodes.Add(new TreeNodeModel()
            {
                Tag = parent.Tag,
                Url = $"{parent.Url}/{NODE_TYPE_DOCUMENT}",
                Title = NODE_TYPE_DOCUMENT,
                Type = TreeNodeType.Document,
                OpenNodeHandler = OpenNodeHandler
            });
            parent.Nodes.Add(new TreeNodeModel()
            {
                Tag = parent.Tag,
                Url = $"{parent.Url}/{NODE_TYPE_INFOREGISTER}",
                Title = NODE_TYPE_INFOREGISTER,
                Type = TreeNodeType.InfoReg,
                OpenNodeHandler = OpenNodeHandler
            });
            parent.Nodes.Add(new TreeNodeModel()
            {
                Tag = parent.Tag,
                Url = $"{parent.Url}/{NODE_TYPE_ACCUMREGISTER}",
                Title = NODE_TYPE_ACCUMREGISTER,
                Type = TreeNodeType.AccumReg,
                OpenNodeHandler = OpenNodeHandler
            });
        }

        private async Task OpenNodeHandler(TreeNodeModel node)
        {
            if (node == null || node.Nodes.Count > 0)
            {
                return;
            }

            if (node.Tag is InfoBaseModel || node.Tag is ExtensionModel)
            {
                if (node.Title == NODE_TYPE_EXTENSIONS)
                {
                    node.Nodes = await GetExtensions(node);
                }
                else
                if (node.Type != TreeNodeType.Undefined)
                {
                    node.Nodes = await GetMetadataItems(node);
                }
            }
        }
        private async Task<List<TreeNodeModel>> GetExtensions(TreeNodeModel parent)
        {
            List<TreeNodeModel> result = new();

            try
            {
                HttpResponseMessage response = await Http.GetAsync(parent.Url);

                List<ExtensionModel> list = await response.Content.ReadFromJsonAsync<List<ExtensionModel>>();

                foreach (ExtensionModel model in list)
                {
                    TreeNodeModel node = new()
                    {
                        Tag = model,
                        Url = $"{parent.Url}/{model.Name}",
                        Title = model.Name,
                        OpenNodeHandler = OpenNodeHandler
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
        private async Task<List<TreeNodeModel>> GetMetadataItems(TreeNodeModel parent)
        {
            List<TreeNodeModel> result = new();

            try
            {
                HttpResponseMessage response = await Http.GetAsync(parent.Url);

                string content = await response.Content.ReadAsStringAsync();

                List<MetadataItemModel> list = await response.Content.ReadFromJsonAsync<List<MetadataItemModel>>();

                foreach (MetadataItemModel item in list)
                {
                    TreeNodeModel node = new()
                    {
                        Tag = item,
                        Url = $"{parent.Url}/{item.Name}",
                        Title = item.Name,
                        Type = parent.Type,
                        OpenNodeHandler = OpenNodeHandler
                    };

                    result.Add(node);
                }
            }
            catch (Exception error)
            {
                //TODO: show error
            }

            return result;
        }
    }
}
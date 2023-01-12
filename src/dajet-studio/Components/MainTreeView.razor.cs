using DaJet.Studio.Controllers;
using DaJet.Studio.Model;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;
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
        protected string FilterValue { get; set; } = string.Empty;
        protected List<TreeNodeModel> Nodes { get; set; } = new();
        [Inject] private ApiTreeViewController ApiTreeViewController { get; set; }
        protected override async Task OnInitializedAsync()
        {
            await IntializeInfoBaseList();
            
            AppState.RefreshInfoBaseCommand += Refresh;
        }
        private async void Refresh()
        {
            try
            {
                await IntializeInfoBaseList();
                
                StateHasChanged();
            }
            catch (Exception error)
            {
                //TODO: handle error
            }
        }

        private async Task IntializeInfoBaseList()
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
                    UseToggle = false,
                    Title = "Ошибка загрузки данных!"
                });
            }
        }
        public void ConfigureInfoBaseNode(in TreeNodeModel node, in InfoBaseModel model)
        {
            node.Tag = model;
            node.Title = model.Name;
            node.Url = $"/md/{model.Name}";

            ConfigureApiTreeViewNode(in node, in model);

            node.Nodes.Add(new TreeNodeModel()
            {
                Tag = model,
                Parent = node,
                Url = $"/mdex/{model.Name}",
                Title = NODE_TYPE_EXTENSIONS,
                OpenNodeHandler = OpenExtensionsNodeHandler
            });

            TreeNodeModel configuration = new()
            {
                Tag = model,
                Url = node.Url,
                Parent = node,
                Title = NODE_TYPE_CONFIGURATION
            };

            ConfigureConfigurationTreeNode(in configuration);

            node.Nodes.Add(configuration);
        }
        private void ConfigureApiTreeViewNode(in TreeNodeModel node, in InfoBaseModel model)
        {
            try
            {
                TreeNodeModel api = ApiTreeViewController.CreateRootNode(model);
                api.Parent = node;
                node.Nodes.Add(api);
            }
            catch (Exception error)
            {
                Snackbar.Add(error.Message, Severity.Error);
            }
        }
        private async Task OpenExtensionsNodeHandler(TreeNodeModel node)
        {
            if (node == null || node.Nodes.Count > 0)
            {
                return;
            }

            try
            {
                HttpResponseMessage response = await Http.GetAsync(node.Url);

                List<ExtensionModel> list = await response.Content.ReadFromJsonAsync<List<ExtensionModel>>();

                foreach (ExtensionModel item in list)
                {
                    TreeNodeModel model = new()
                    {
                        Tag = item,
                        Title = item.Name,
                        Parent = node,
                        Url = $"{node.Url}/{item.Name}"
                    };

                    ConfigureConfigurationTreeNode(in model);

                    node.Nodes.Add(model);
                }
            }
            catch (Exception error)
            {
                //TODO: show error
            }
        }
        public void ConfigureConfigurationTreeNode(in TreeNodeModel parent)
        {
            parent.Nodes.Add(new TreeNodeModel()
            {
                Parent = parent,
                Tag = parent.Tag,
                Title = NODE_TYPE_CATALOG,
                Url = $"{parent.Url}/{NODE_TYPE_CATALOG}",
                OpenNodeHandler = OpenMetadataNodeHandler
            });
            parent.Nodes.Add(new TreeNodeModel()
            {
                Parent = parent,
                Tag = parent.Tag,
                Title = NODE_TYPE_DOCUMENT,
                Url = $"{parent.Url}/{NODE_TYPE_DOCUMENT}",
                OpenNodeHandler = OpenMetadataNodeHandler
            });
            parent.Nodes.Add(new TreeNodeModel()
            {
                Parent = parent,
                Tag = parent.Tag,
                Title = NODE_TYPE_INFOREGISTER,
                Url = $"{parent.Url}/{NODE_TYPE_INFOREGISTER}",
                OpenNodeHandler = OpenMetadataNodeHandler
            });
            parent.Nodes.Add(new TreeNodeModel()
            {
                Parent = parent,
                Tag = parent.Tag,
                Title = NODE_TYPE_ACCUMREGISTER,
                Url = $"{parent.Url}/{NODE_TYPE_ACCUMREGISTER}",
                OpenNodeHandler = OpenMetadataNodeHandler
            });
        }
        
        private async Task OpenMetadataNodeHandler(TreeNodeModel node)
        {
            if (node == null || node.Nodes.Count > 0)
            {
                return;
            }

            try
            {
                HttpResponseMessage response = await Http.GetAsync(node.Url);

                string content = await response.Content.ReadAsStringAsync();

                List<MetadataItemModel> list = await response.Content.ReadFromJsonAsync<List<MetadataItemModel>>();

                foreach (MetadataItemModel item in list)
                {
                    TreeNodeModel model = new()
                    {
                        Tag = item,
                        Parent = node,
                        Title = item.Name,
                        Url = $"{node.Url}/{item.Name}",
                        OpenNodeHandler = OpenEntityNodeHandler
                    };

                    node.Nodes.Add(model);
                }
            }
            catch (Exception error)
            {
                //TODO: show error
            }
        }
        private async Task OpenEntityNodeHandler(TreeNodeModel node)
        {
            if (node == null || node.Nodes.Count > 0)
            {
                return;
            }

            try
            {
                HttpResponseMessage response = await Http.GetAsync(node.Url);

                string content = await response.Content.ReadAsStringAsync();

                EntityModel entity = await response.Content.ReadFromJsonAsync<EntityModel>();

                ConfigureEntityNode(in node, in entity);
            }
            catch (Exception error)
            {
                //TODO: show error
            }
        }
        private void ConfigureEntityNode(in TreeNodeModel node, in EntityModel entity)
        {
            // TODO: node.Tag = entity; // Change MetadataItemModel to EntityModel ???

            foreach (PropertyModel property in entity.Properties)
            {
                TreeNodeModel model = new()
                {
                    Tag = property,
                    Parent = node,
                    Title = property.Name,
                    UseToggle = false
                };

                node.Nodes.Add(model);
            }

            foreach (EntityModel table in entity.TableParts)
            {
                TreeNodeModel model = new()
                {
                    Tag = table,
                    Parent = node,
                    Title = table.Name
                };

                ConfigureEntityNode(in model, in table);

                node.Nodes.Add(model);
            }
        }

        #region "Filter Tree View"
        protected async Task FilterTreeView(string filter)
        {
            string database = AppState.CurrentInfoBase;

            TreeNodeModel target = null;

            foreach (TreeNodeModel node in Nodes)
            {
                if (node.Title == database)
                {
                    target = node;
                    target.IsExpanded = true;
                }
                else
                {
                    node.IsExpanded = false;
                }
            }

            if (target == null) { return; }

            await LazyLoad_MetadataItems(target);

            Search(in target, in filter);
        }
        private async Task LazyLoad_MetadataItems(TreeNodeModel target)
        {
            foreach (var node in target.Nodes)
            {
                if (node.Title == NODE_TYPE_EXTENSIONS)
                {
                    // TODO: ignore ?
                }
                else if (node.Title == NODE_TYPE_CONFIGURATION)
                {
                    foreach (var metaNode in node.Nodes)
                    {
                        await OpenMetadataNodeHandler(metaNode);
                    }
                }
            }
        }
        private void Search(in TreeNodeModel node, in string filter)
        {
            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo("ru-RU");
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.CurrentUICulture;
            }

            if (string.IsNullOrWhiteSpace(filter))
            {
                ClearFilter(node.Nodes);
            }
            else
            {
                FilterNodes(node.Nodes, filter, culture);
            }
        }
        private void ClearFilter(IEnumerable<TreeNodeModel> nodes)
        {
            foreach (TreeNodeModel node in nodes)
            {
                ClearFilter(node.Nodes);

                node.IsVisible = true;
                node.IsExpanded = false;
            }
        }
        private void FilterNodes(IEnumerable<TreeNodeModel> nodes, string filter, CultureInfo culture)
        {
            foreach (TreeNodeModel node in nodes)
            {
                if (node.Tag is ExtensionModel)
                {
                    node.IsExpanded = false;
                    continue; // TODO: ignore ?
                }

                FilterNodes(node.Nodes, filter, culture);

                if (node.Tag is InfoBaseModel)
                {
                    node.IsExpanded = true;
                }
                else if (node.Tag is MetadataItemModel model)
                {
                    node.IsVisible = culture.CompareInfo.IndexOf(model.Name, filter, CompareOptions.IgnoreCase) >= 0;
                    
                    if (node.IsVisible)
                    {
                        node.IsExpanded = false;
                    }
                }
                else
                {
                    node.IsExpanded = false;
                }
            }
        }
        #endregion 
    }
}
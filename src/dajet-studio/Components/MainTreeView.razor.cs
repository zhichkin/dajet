using DaJet.Studio.Controllers;
using DaJet.Studio.Model;
using DaJet.Studio.Pages;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace DaJet.Studio.Components
{
    public partial class MainTreeView : ComponentBase, IDisposable
    {
        #region "CONSTANTS"
        private readonly Guid ENUMERATION_TYPE = new("f6a80749-5ad7-400b-8519-39dc5dff2542");
        private readonly Guid SHARED_PROPERTY_TYPE = new("15794563-ccec-41f6-a83c-ec5f7b9a5bc1");
        private readonly Guid NAMED_DATA_TYPE_TYPE = new("c045099e-13b9-4fb6-9d50-fca00202971e");
        private const string NODE_TYPE_SERVICE = "Служебные";
        private const string NODE_TYPE_SHARED_PROPERTY = "ОбщийРеквизит";
        private const string NODE_TYPE_NAMED_DATA_TYPE = "ОпределяемыйТип";
        private const string NODE_TYPE_CONFIGURATION = "Конфигурация";
        private const string NODE_TYPE_EXTENSIONS = "Расширения";
        private const string NODE_TYPE_PUBLICATION = "ПланОбмена";
        private const string NODE_TYPE_ENUMERATION = "Перечисление";
        private const string NODE_TYPE_CATALOG = "Справочник";
        private const string NODE_TYPE_DOCUMENT = "Документ";
        private const string NODE_TYPE_CHARACTERISTIC = "ПланВидовХарактеристик";
        private const string NODE_TYPE_INFOREGISTER = "РегистрСведений";
        private const string NODE_TYPE_ACCUMREGISTER = "РегистрНакопления";
        #endregion
        protected string FilterValue { get; set; } = string.Empty;
        protected List<TreeNodeModel> Nodes { get; set; } = new();
        [Inject] private DbViewController DbViewController { get; set; }
        [Inject] private ApiTreeViewController ApiTreeViewController { get; set; }
        protected override async Task OnInitializedAsync()
        {
            await IntializeInfoBaseList();
            
            AppState.RefreshInfoBaseCommand += Refresh;
        }
        public void Dispose()
        {
            if (AppState != null)
            {
                AppState.RefreshInfoBaseCommand -= Refresh;
            }
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

        private void ConfigureDbViewNode(in TreeNodeModel node, in InfoBaseModel model)
        {
            try
            {
                TreeNodeModel dbview = DbViewController.CreateRootNode(model);
                dbview.Parent = node;
                node.Nodes.Add(dbview);
            }
            catch (Exception error)
            {
                Snackbar.Add(error.Message, Severity.Error);
            }
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
        public void ConfigureInfoBaseNode(in TreeNodeModel node, in InfoBaseModel model)
        {
            node.Tag = model;
            node.Title = model.Name;
            node.Url = $"/md/{model.Name}";
            node.ContextMenuHandler = InfoBaseContextMenuHandler;

            ConfigureApiTreeViewNode(in node, in model);

            ConfigureDbViewNode(in node, in model);

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
        public void ConfigureConfigurationTreeNode(in TreeNodeModel parent)
        {
            TreeNodeModel service = new()
            {
                Parent = parent,
                Tag = parent.Tag,
                Title = NODE_TYPE_SERVICE,
                Url = $"{parent.Url}"
            };
            service.Nodes.Add(new TreeNodeModel()
            {
                Parent = service,
                Tag = parent.Tag,
                Title = NODE_TYPE_SHARED_PROPERTY,
                Url = $"{parent.Url}/{NODE_TYPE_SHARED_PROPERTY}",
                OpenNodeHandler = OpenMetadataNodeHandler
            });
            service.Nodes.Add(new TreeNodeModel()
            {
                Parent = service,
                Tag = parent.Tag,
                Title = NODE_TYPE_NAMED_DATA_TYPE,
                Url = $"{parent.Url}/{NODE_TYPE_NAMED_DATA_TYPE}",
                OpenNodeHandler = OpenMetadataNodeHandler
            });
            parent.Nodes.Add(service);

            parent.Nodes.Add(new TreeNodeModel()
            {
                Parent = parent,
                Tag = parent.Tag,
                Title = NODE_TYPE_PUBLICATION,
                Url = $"{parent.Url}/{NODE_TYPE_PUBLICATION}",
                OpenNodeHandler = OpenMetadataNodeHandler
            });
            parent.Nodes.Add(new TreeNodeModel()
            {
                Parent = parent,
                Tag = parent.Tag,
                Title = NODE_TYPE_ENUMERATION,
                Url = $"{parent.Url}/{NODE_TYPE_ENUMERATION}",
                OpenNodeHandler = OpenMetadataNodeHandler
            });
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
                Title = NODE_TYPE_CHARACTERISTIC,
                Url = $"{parent.Url}/{NODE_TYPE_CHARACTERISTIC}",
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

        private async Task InfoBaseContextMenuHandler(TreeNodeModel node, IDialogService dialogService)
        {
            if (node.Tag is not InfoBaseModel model)
            {
                return;
            }

            string _backupName = model.Name; // TODO: make full copy of the model

            DialogParameters parameters = new()
            {
                { "Model", model },
                { "TreeNode", node},
                { "MainTreeView", this }
            };
            DialogOptions options = new() { CloseButton = true };
            var dialog = dialogService.Show<InfoBaseDialog>("DaJet Studio", parameters, options);
            var result = await dialog.Result;
            if (result.Cancelled)
            {
                model.Name = _backupName; // TODO: restore state of the model
                return;
            }

            if (result.Data is not InfoBaseModel entity)
            {
                return;
            }

            try
            {
                HttpResponseMessage response = await Http.PutAsJsonAsync("/md", entity);

                if (!response.IsSuccessStatusCode)
                {
                    model.Name = _backupName; // TODO: restore state of the model
                    throw new Exception(response.ReasonPhrase);
                }

                node.Title = entity.Name; // change view
                node.Nodes.Clear();
                ConfigureInfoBaseNode(node, entity);

                InfoBaseModel database = AppState.GetDatabase(entity.Uuid);
                if (database != null)
                {
                    database.Name = entity.Name;
                    AppState.CurrentDatabase = database; // notify MainLayout
                }

                Snackbar.Add($"Свойства базы данных [{entity.Name}] обновлены успешно.", Severity.Success);
            }
            catch (Exception error)
            {
                Snackbar.Add(error.Message, Severity.Error);
            }
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
                    bool toggle = true;

                    if (item.Type == SHARED_PROPERTY_TYPE || item.Type == NAMED_DATA_TYPE_TYPE)
                    {
                        toggle = false;
                    }

                    TreeNodeModel model = new()
                    {
                        Tag = item,
                        Parent = node,
                        Title = item.Name,
                        Url = $"{node.Url}/{item.Name}",
                        UseToggle = toggle,
                        OpenNodeHandler = (toggle ? OpenEntityNodeHandler : null)
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

            if (node.Tag is not MetadataItemModel item)
            {
                return;
            }

            try
            {
                HttpResponseMessage response = await Http.GetAsync(node.Url);

                string content = await response.Content.ReadAsStringAsync();

                if (item.Type == ENUMERATION_TYPE)
                {
                    EnumModel enumeration = await response.Content.ReadFromJsonAsync<EnumModel>();
                    ConfigureEnumerationNode(in node, in enumeration);
                }
                else
                {
                    EntityModel entity = await response.Content.ReadFromJsonAsync<EntityModel>();
                    ConfigureEntityNode(in node, in entity);
                }
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
        private void ConfigureEnumerationNode(in TreeNodeModel node, in EnumModel entity)
        {
            // TODO: node.Tag = entity; // Change MetadataItemModel to EntityModel ???

            foreach (EnumValue value in entity.Values)
            {
                TreeNodeModel model = new()
                {
                    Tag = value,
                    Parent = node,
                    Title = value.Name,
                    UseToggle = false
                };
                node.Nodes.Add(model);
            }
        }

        #region "FILTER TREE VIEW"
        protected async Task FilterTreeView(string filter)
        {
            InfoBaseModel database = AppState.CurrentDatabase;

            TreeNodeModel target = null;

            foreach (TreeNodeModel node in Nodes)
            {
                if (node.Title == database.Name)
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
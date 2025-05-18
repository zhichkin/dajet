﻿using DaJet.Http.Client;
using DaJet.Http.Model;
using DaJet.Model;
using DaJet.Studio.Controllers;
using DaJet.Studio.Pages;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;
using System.Net.Http.Json;

namespace DaJet.Studio.Components
{
    public partial class MainTreeView : ComponentBase, IDisposable
    {
        #region "CONSTANTS"
        private readonly Guid CONSTANT_TYPE = new("0195e80c-b157-11d4-9435-004095e12fc7");
        private readonly Guid ENUMERATION_TYPE = new("f6a80749-5ad7-400b-8519-39dc5dff2542");
        private readonly Guid SHARED_PROPERTY_TYPE = new("15794563-ccec-41f6-a83c-ec5f7b9a5bc1");
        private readonly Guid NAMED_DATA_TYPE_TYPE = new("c045099e-13b9-4fb6-9d50-fca00202971e");
        private readonly Guid ACCOUNTING_REGISTER = new("2deed9b8-0056-4ffe-a473-c20a6c32a0bc");
        private readonly HashSet<Guid> METADATA_ENTITY =
        [
            new Guid("c045099e-13b9-4fb6-9d50-fca00202971e"), // Определяемые типы
            new Guid("cf4abea6-37b2-11d4-940f-008048da11f9"), // Справочники
            new Guid("061d872a-5787-460e-95ac-ed74ea3a3e84"), // Документы
            new Guid("857c4a91-e5f4-4fac-86ec-787626f1c108"), // Планы обмена
            new Guid("82a1b659-b220-4d94-a9bd-14d757b95a48"), // Планы видов характеристик
            new Guid("13134201-f60b-11d5-a3c7-0050bae0a776"), // Регистры сведений
            new Guid("b64d9a40-1642-11d6-a3c7-0050bae0a776")  // Регистры накопления
        ];
        private const string NODE_TYPE_SERVICE = "Служебные";
        private const string NODE_TYPE_SHARED_PROPERTY = "ОбщийРеквизит";
        private const string NODE_TYPE_NAMED_DATA_TYPE = "ОпределяемыйТип";
        private const string NODE_TYPE_CONFIGURATION = "Конфигурация";
        private const string NODE_TYPE_EXTENSIONS = "Расширения";
        private const string NODE_TYPE_CONSTANT = "Константа";
        private const string NODE_TYPE_PUBLICATION = "ПланОбмена";
        private const string NODE_TYPE_ENUMERATION = "Перечисление";
        private const string NODE_TYPE_CATALOG = "Справочник";
        private const string NODE_TYPE_DOCUMENT = "Документ";
        private const string NODE_TYPE_CHARACTERISTIC = "ПланВидовХарактеристик";
        private const string NODE_TYPE_INFOREGISTER = "РегистрСведений";
        private const string NODE_TYPE_ACCUMREGISTER = "РегистрНакопления";
        private const string NODE_TYPE_CHART_OF_ACCOUNTS = "ПланСчетов";
        private const string NODE_TYPE_ACCOUNTING_REGISTER = "РегистрБухгалтерии";
        #endregion
        protected string FilterValue { get; set; } = string.Empty;
        protected List<TreeNodeModel> Nodes { get; set; } = new();
        [Inject] private DaJetHttpClient DaJetClient { get; set; }
        [Inject] private DaJetCodeController CodeController { get; set; }
        [Inject] private MdTreeViewController MdTreeViewController { get; set; }
        [Inject] private FlowTreeViewController FlowController { get; set; }
        [Inject] private DbViewController DbViewController { get; set; }
        [Inject] private ApiTreeViewController ApiTreeViewController { get; set; }
        [Inject] private ExchangeTreeViewController ExchangeTreeViewController { get; set; }
        protected override async Task OnInitializedAsync()
        {
            Nodes.Add(CodeController.CreateRootNode());

            Nodes.Add(CreateDbmsRootNode());

            var nodes = await DaJetClient.QueryAsync<TreeNodeRecord>();

            foreach (TreeNodeRecord node in nodes)
            {
                if (node.Name == "flow")
                {
                    Nodes.Add(FlowController.CreateRootNode(node));
                }
            }

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
                TreeNodeModel dbms = Nodes.Where(node => node.Title == "data").FirstOrDefault();

                if (dbms is not null)
                {
                    await OpenDbmsNodeHandler(dbms);

                    StateHasChanged();
                }
            }
            catch (Exception error)
            {
                //TODO: handle error
            }
        }

        private TreeNodeModel CreateDbmsRootNode()
        {
            return new TreeNodeModel()
            {
                Url = $"/md",
                Title = "data",
                OpenNodeHandler = OpenDbmsNodeHandler
            };
        }
        private async Task OpenDbmsNodeHandler(TreeNodeModel root)
        {
            if (root is null) { return; }

            try
            {
                root.Nodes.Clear();

                IEnumerable<InfoBaseRecord> list = await DaJetClient.QueryAsync<InfoBaseRecord>();

                foreach (InfoBaseRecord model in list)
                {
                    TreeNodeModel node = new();

                    ConfigureInfoBaseNode(in node, in model);

                    root.Nodes.Add(node);
                }
            }
            catch
            {
                root.Nodes.Add(new TreeNodeModel()
                {
                    UseToggle = false,
                    Title = "Ошибка загрузки данных!"
                });
            }
        }
        private void ConfigureDbViewNode(in TreeNodeModel node, in InfoBaseRecord model)
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
        private void ConfigureApiTreeViewNode(in TreeNodeModel node, in InfoBaseRecord model)
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
        private void ConfigureExchangeTreeViewNode(in TreeNodeModel node, in InfoBaseRecord model)
        {
            try
            {
                TreeNodeModel root = ExchangeTreeViewController.CreateRootNode(model);
                root.Parent = node;
                node.Nodes.Add(root);
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
        public void ConfigureInfoBaseNode(in TreeNodeModel node, in InfoBaseRecord model)
        {
            node.Tag = model;
            node.Title = model.Name;
            node.Url = $"/md/{model.Name}";
            node.ContextMenuHandler = InfoBaseContextMenuHandler;

            ConfigureApiTreeViewNode(in node, in model);

            //ConfigureExchangeTreeViewNode(in node, in model);

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
                Title = NODE_TYPE_CONSTANT,
                Url = $"{parent.Url}/{NODE_TYPE_CONSTANT}",
                OpenNodeHandler = OpenMetadataNodeHandler
            });
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
                Title = NODE_TYPE_CHART_OF_ACCOUNTS,
                Url = $"{parent.Url}/{NODE_TYPE_CHART_OF_ACCOUNTS}",
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
            parent.Nodes.Add(new TreeNodeModel()
            {
                Parent = parent,
                Tag = parent.Tag,
                Title = NODE_TYPE_ACCOUNTING_REGISTER,
                Url = $"{parent.Url}/{NODE_TYPE_ACCOUNTING_REGISTER}",
                OpenNodeHandler = OpenMetadataNodeHandler
            });
        }

        private async Task InfoBaseContextMenuHandler(TreeNodeModel node, ElementReference element)
        {
            if (node.Tag is not InfoBaseRecord model)
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
            var dialog = DialogService.Show<InfoBaseDialog>("DaJet Studio", parameters, options);
            var result = await dialog.Result;
            if (result.Canceled)
            {
                model.Name = _backupName; // TODO: restore state of the model
                return;
            }

            if (result.Data is not InfoBaseRecord entity)
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

                InfoBaseRecord database = AppState.GetDatabase(entity.Identity);
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

                    if (item.Type == SHARED_PROPERTY_TYPE ||
                        item.Type == NAMED_DATA_TYPE_TYPE ||
                        item.Type == CONSTANT_TYPE)
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

                    if (METADATA_ENTITY.Contains(item.Type))
                    {
                        model.ContextMenuHandler = MdTreeViewController.OpenEntityNodeContextMenuHandler;
                    }

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

            if (node.Tag is MetadataItemModel metadata && metadata.Type == ACCOUNTING_REGISTER)
            {
                string name = "ЗначенияСубконто";

                if (node.Title == name)
                {
                    //FIXME: по сути тут у нас хак для служебной таблицы регистра - второй раз добавлять не нужно
                }
                else
                {
                    TreeNodeModel model = new()
                    {
                        Tag = metadata,
                        Parent = node,
                        Title = name,
                        Url = $"{node.Url}.{name}", //FIXME: вот так хитро через точку, а не слэш ¯\_(ツ)_/¯
                        OpenNodeHandler = OpenEntityNodeHandler
                    };

                    node.Nodes.Add(model);
                }
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
            TreeNodeModel dbms = Nodes.Where(node => node.Title == "data").FirstOrDefault();

            if (dbms is null) { return; }

            InfoBaseRecord database = AppState.CurrentDatabase;

            TreeNodeModel target = null;

            foreach (TreeNodeModel node in dbms.Nodes)
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

                if (node.Tag is InfoBaseRecord)
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
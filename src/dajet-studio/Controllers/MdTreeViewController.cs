using DaJet.Http.Client;
using DaJet.Http.Model;
using DaJet.Model;
using DaJet.Studio.Components;
using DaJet.Studio.Pages;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DaJet.Studio.Controllers
{
    public sealed class MdTreeViewController
    {
        #region "CONSTANTS"
        private readonly Guid CONSTANT_TYPE = new("0195e80c-b157-11d4-9435-004095e12fc7");
        private readonly Guid ENUMERATION_TYPE = new("f6a80749-5ad7-400b-8519-39dc5dff2542");
        private readonly Guid SHARED_PROPERTY_TYPE = new("15794563-ccec-41f6-a83c-ec5f7b9a5bc1");
        private readonly Guid NAMED_DATA_TYPE_TYPE = new("c045099e-13b9-4fb6-9d50-fca00202971e");
        private readonly Guid ACCOUNTING_REGISTER = new("2deed9b8-0056-4ffe-a473-c20a6c32a0bc");
        private readonly HashSet<Guid> METADATA_OBJECT =
        [
            new Guid("0195e80c-b157-11d4-9435-004095e12fc7"), // Константы
            new Guid("cf4abea6-37b2-11d4-940f-008048da11f9"), // Справочники
            new Guid("061d872a-5787-460e-95ac-ed74ea3a3e84"), // Документы
            new Guid("82a1b659-b220-4d94-a9bd-14d757b95a48"), // Планы видов характеристик
            new Guid("857c4a91-e5f4-4fac-86ec-787626f1c108"), // Планы обмена
            new Guid("238e7e88-3c5f-48b2-8a3b-81ebbecb20ed"), // Планы счетов
            new Guid("13134201-f60b-11d5-a3c7-0050bae0a776"), // Регистры сведений
            new Guid("b64d9a40-1642-11d6-a3c7-0050bae0a776"), // Регистры накопления
            new Guid("2deed9b8-0056-4ffe-a473-c20a6c32a0bc")  // Регистры бухгатерии
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

        private readonly ISnackbar SnackbarService;
        private readonly IDialogService DialogService;
        private readonly DaJetHttpClient DaJetClient;
        private readonly NavigationManager Navigator;
        private readonly DbViewController DbViewController;
        private readonly ApiTreeViewController ApiTreeViewController;
        public Func<TreeNodeModel, ElementReference, Task> OpenInfoBaseContextMenuHandler { get; set; }
        public Func<TreeNodeModel, ElementReference, Task> OpenMetadataObjectContextMenuHandler { get; set; }
        public MdTreeViewController(DaJetHttpClient client, NavigationManager navigator, IServiceProvider services)
        {
            DaJetClient = client ?? throw new ArgumentNullException(nameof(client));
            Navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            DialogService = services.GetRequiredService<IDialogService>();
            SnackbarService = services.GetRequiredService<ISnackbar>();
            DbViewController = services.GetRequiredService<DbViewController>();
            ApiTreeViewController = services.GetRequiredService<ApiTreeViewController>();
        }
        public void NavigateToDbSchemaDiagnosticPage(in TreeNodeModel model)
        {
            if (model.Tag is not InfoBaseRecord infoBase) { return; }

            Navigator.NavigateTo($"/db-schema-diagnostic-page/{infoBase.Name}");
        }
        public void NavigateToMetadataObjectPage(in TreeNodeModel model)
        {
            if (model.Tag is not MetadataItemModel) { return; }

            if (string.IsNullOrWhiteSpace(model.Url)) { return; }

            string url = model.Url.Replace('/', '~');

            Navigator.NavigateTo($"/metadata-object-page/{url}");
        }
        public async Task ClearInfoBaseMetadataCache(TreeNodeModel node)
        {
            if (node.Tag is not InfoBaseRecord model)
            {
                return;
            }

            try
            {
                QueryResponse response = await DaJetClient.ClearInfoBaseMetadataCache(model.Name);

                if (response.Success)
                {
                    SnackbarService.Add($"Кэш базы данных [{model}] обновлён успешно.", Severity.Success);
                }
                else
                {
                    SnackbarService.Add($"Ошибка! [{model}]: {response.Message}", Severity.Error);
                }

                if (response.Success)
                {
                    node.Nodes.Clear();
                    node.IsExpanded = false;
                    node.NotifyStateChanged();
                    ConfigureInfoBaseNode(in node, in model);
                }
            }
            catch (Exception error)
            {
                SnackbarService.Add($"Ошибка! [{model}]: {error.Message}", Severity.Error);
            }
        }
        public async Task OpenInfoBaseSettingsDialog(TreeNodeModel node)
        {
            if (node.Tag is not InfoBaseRecord model)
            {
                return;
            }

            InfoBaseRecord backup = model.Copy();

            DialogParameters parameters = new()
            {
                { "Model", model }
            };
            DialogOptions options = new() { CloseButton = true };
            var dialog = DialogService.Show<InfoBaseDialog>("DaJet Studio", parameters, options);
            var result = await dialog.Result;
            
            if (result.Canceled)
            {
                model.Restore(in backup); return;
            }

            if (result.Data is not InfoBaseRecord entity)
            {
                return;
            }

            try
            {
                await DaJetClient.UpdateAsync(entity);

                // change view model
                node.Title = entity.Name;
                node.IsExpanded = false;
                node.Nodes.Clear();
                node.NotifyStateChanged();
                ConfigureInfoBaseNode(node, entity);

                SnackbarService.Add($"Свойства базы данных [{entity.Name}] обновлены успешно.", Severity.Success);
            }
            catch (Exception error)
            {
                model.Restore(in backup);
                SnackbarService.Add(error.Message, Severity.Error);
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
                List<ExtensionModel> list = await DaJetClient.GetExtensions(node.Url);

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
        public async Task OpenMetadataNodeHandler(TreeNodeModel node)
        {
            if (node == null || node.Nodes.Count > 0)
            {
                return;
            }

            try
            {
                List<MetadataItemModel> list = await DaJetClient.GetMetadataItems(node.Url);

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

                    if (METADATA_OBJECT.Contains(item.Type))
                    {
                        model.ContextMenuHandler = OpenMetadataObjectContextMenuHandler;
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
                if (item.Type == ENUMERATION_TYPE)
                {
                    EnumModel enumeration = await DaJetClient.GetEnumObject(node.Url);

                    ConfigureEnumerationNode(in node, in enumeration);
                }
                else
                {
                    EntityModel entity = await DaJetClient.GetEntityObject(node.Url);

                    ConfigureEntityNode(in node, in entity);
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
            node.ContextMenuHandler = OpenInfoBaseContextMenuHandler;

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
                SnackbarService.Add(error.Message, Severity.Error);
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
                SnackbarService.Add(error.Message, Severity.Error);
            }
        }
        private void ConfigureConfigurationTreeNode(in TreeNodeModel parent)
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
    }
}
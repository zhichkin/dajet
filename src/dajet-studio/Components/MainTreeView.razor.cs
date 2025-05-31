using DaJet.Http.Client;
using DaJet.Http.Model;
using DaJet.Model;
using DaJet.Studio.Controllers;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;

namespace DaJet.Studio.Components
{
    public partial class MainTreeView : ComponentBase, IDisposable
    {
        protected string FilterValue { get; set; } = string.Empty;
        protected List<TreeNodeModel> Nodes { get; set; } = new();
        [Inject] private DaJetHttpClient DaJetClient { get; set; }
        [Inject] private DaJetCodeController CodeController { get; set; }
        [Inject] private FlowTreeViewController FlowController { get; set; }
        [Inject] private MdTreeViewController MdTreeViewController { get; set; }
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

                    MdTreeViewController.ConfigureInfoBaseNode(in node, in model);

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
                if (node.Title == "Расширения")
                {
                    // TODO: ignore ?
                }
                else if (node.Title == "Конфигурация")
                {
                    foreach (var metaNode in node.Nodes)
                    {
                        await MdTreeViewController.OpenMetadataNodeHandler(metaNode);
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
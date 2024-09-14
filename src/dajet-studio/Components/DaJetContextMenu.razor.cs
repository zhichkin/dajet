using DaJet.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DaJet.Studio.Components
{
    public partial class DaJetContextMenu : ComponentBase, IDisposable
    {
        protected TreeNodeModel Model { get; set; }
        protected string Title { get; set; }
        protected bool IsFolder { get; set; }
        protected override void OnInitialized()
        {
            if (CodeController is not null)
            {
                CodeController.OpenContextMenuHandler = OpenContextMenuHandler;
            }
        }
        private async Task OpenContextMenuHandler(TreeNodeModel node, ElementReference source)
        {
            if (node.Tag is not CodeItem model)
            {
                return;
            }

            Model = node;
            Title = node.Url;
            IsFolder = model.IsFolder;

            StateHasChanged();

            await JSRuntime.InvokeVoidAsync("OpenCodeItemContextMenu", source);
        }
        public void Dispose()
        {
            if (CodeController is not null)
            {
                CodeController.OpenContextMenuHandler = null;
            }
        }
        private async Task CloseContextMenu()
        {
            await JSRuntime.InvokeVoidAsync("CloseCodeItemContextMenu");
        }
        private async Task OpenServerLogPage(MouseEventArgs args)
        {
            await CloseContextMenu();

            CodeController.NavigateToServerLogPage();
        }
        private async Task CreateScriptFolder(MouseEventArgs args)
        {
            await CloseContextMenu();

            if (Model.Tag is not CodeItem item)
            {
                return;
            }

            TreeNodeModel parent = item.IsFolder ? Model : Model.Parent;

            string name = "new_folder";
            string url = $"{parent.Url}/{name}";

            string response = await DaJetClient.CreateScriptFolder(url);

            if (string.IsNullOrEmpty(response))
            {
                parent.Nodes.Add(new TreeNodeModel()
                {
                    Url = url,
                    Title = name,
                    Parent = parent,
                    Tag = new CodeItem()
                    {
                        Name = name,
                        IsFolder = true
                    },
                    Icon = "/img/folder-closed.png",
                    UseToggle = true,
                    CanBeEdited = true,
                    IsDraggable = true,
                    NodeClickHandler = CodeController.CodeItemClickHandler,
                    UpdateTitleCommand = CodeController.UpdateNodeTitleHandler,
                    ContextMenuHandler = CodeController.ShowContextMenu,
                    DropDataHandler = CodeController.DropDataHandler,
                    CanAcceptDropData = CodeController.CanAcceptDropData
                });

                parent.NotifyStateChanged();
            }
        }
        private async Task DeleteScriptFolder(MouseEventArgs args)
        {
            await CloseContextMenu();

            if (Model.Tag is not CodeItem item) { return; }

            if (!item.IsFolder) { return; }

            string message = $"Удалить каталог {Model.Url} ?";

            bool confirmed = await JSRuntime.InvokeAsync<bool>("confirm", message);

            if (!confirmed) { return; }

            string response = await DaJetClient.DeleteScriptFolder(Model.Url);

            if (string.IsNullOrEmpty(response))
            {
                Model.Parent.Nodes.Remove(Model);
                Model.Parent.NotifyStateChanged();
            }
        }
        private async Task CreateScriptFile(MouseEventArgs args)
        {
            await CloseContextMenu();

            if (Model.Tag is not CodeItem item)
            {
                return;
            }

            TreeNodeModel parent = item.IsFolder ? Model : Model.Parent;

            string name = "new_script.djs";
            string url = $"{parent.Url}/{name}";

            string response = await DaJetClient.CreateScriptFile(url);

            if (string.IsNullOrEmpty(response))
            {
                parent.Nodes.Add(new TreeNodeModel()
                {
                    Url = url,
                    Title = name,
                    Parent = parent,
                    Tag = new CodeItem()
                    {
                        Name = name,
                        IsFolder = false
                    },
                    Icon = "/img/script.png",
                    UseToggle = false,
                    CanBeEdited = true,
                    IsDraggable = true,
                    NodeClickHandler = CodeController.CodeItemClickHandler,
                    UpdateTitleCommand = CodeController.UpdateNodeTitleHandler,
                    ContextMenuHandler = CodeController.ShowContextMenu,
                    DropDataHandler = CodeController.DropDataHandler,
                    CanAcceptDropData = CodeController.CanAcceptDropData
                });

                parent.NotifyStateChanged();
            }
        }
        private async Task DeleteScriptFile(MouseEventArgs args)
        {
            await CloseContextMenu();

            if (Model.Tag is not CodeItem item) { return; }

            if (item.IsFolder) { return; }

            string message = $"Удалить скрипт {Model.Url} ?";

            bool confirmed = await JSRuntime.InvokeAsync<bool>("confirm", message);

            if (!confirmed) { return; }

            string response = await DaJetClient.DeleteScriptFile(Model.Url);

            if (string.IsNullOrEmpty(response))
            {
                Model.Parent.Nodes.Remove(Model);
                Model.Parent.NotifyStateChanged();
            }
        }
    }
}
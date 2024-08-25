using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.ComponentModel;

namespace DaJet.Studio.Components
{
    public partial class TreeNode : ComponentBase
    {
        private ElementReference TitleInput;
        private ElementReference TreeNodeSpan;
        private string _title = string.Empty;
        [Parameter] public TreeNodeModel Model { get; set; }
        protected override void OnParametersSet()
        {
            if (Model is not null)
            {
                Model.StateHasChanged += StateHasChanged;
            }
        }
        public void Dispose()
        {
            if (Model is not null)
            {
                Model.StateHasChanged -= StateHasChanged;
            }
        }
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (Model.CanBeEdited && Model.IsInEditMode)
            {
                await Task.Delay(500); // hack =)
                await JSRuntime.InvokeVoidAsync("BlazorFocusElement", TitleInput);
            }
        }
        private async Task NodeClick(MouseEventArgs args)
        {
            if (Model is not null && !Model.IsInEditMode)
            {
                await Model.NodeClickCommand();
            }
        }
        private async Task ToggleClick(MouseEventArgs args)
        {
            if (Model is not null && Model.UseToggle)
            {
                await Model.ToggleCommand();
            }
        }
        private async Task OpenContextMenu(MouseEventArgs args)
        {
            await Model?.ContextMenuCommand(TreeNodeSpan);
        }
        private void DoubleClick(MouseEventArgs args)
        {
            if (Model.CanBeEdited && !Model.IsInEditMode)
            {
                StartEditOperation();
            }
        }
        private void LostFocus(FocusEventArgs args)
        {
            if (Model.CanBeEdited && Model.IsInEditMode)
            {
                CancelEditOperation();
            }
        }
        private void CancelEditMode(MouseEventArgs args)
        {
            if (Model.CanBeEdited && Model.IsInEditMode)
            {
                CancelEditOperation();
            }
        }
        private async Task KeyPress(KeyboardEventArgs args)
        {
            if (!(Model.CanBeEdited && Model.IsInEditMode))
            {
                return;
            }
            
            if (args.Key == "Enter")
            {
                if (string.IsNullOrWhiteSpace(Model.Title))
                {
                    CancelEditOperation();
                }
                else
                {
                    CancelEventArgs state = new()
                    {
                        Cancel = false
                    };
                    await Model.UpdateTitleCommand(Model, state);

                    if (state.Cancel)
                    {
                        CancelEditOperation();
                    }
                    else
                    {
                        CloseEditOperation();
                    }
                }
            }
            else if (args.Key == "Escape")
            {
                CancelEditOperation();
            }
        }
        private void StartEditOperation()
        {
            _title = Model.Title;
            Model.IsInEditMode = true;
        }
        private void CloseEditOperation()
        {
            _title = string.Empty;
            Model.IsInEditMode = false;
        }
        private void CancelEditOperation()
        {
            Model.Title = _title;
            _title = string.Empty;
            Model.IsInEditMode = false;
        }


        private static TreeNode _draggingNode;
        private string DropStyle { get; set; } = string.Empty;
        private void DragStartHandler()
        {
            if (Model is not null && Model.IsDraggable)
            {
                _draggingNode = this;
            }
        }
        private void DragEnterHandler()
        {
            if (_draggingNode is null || _draggingNode == this) { return; }

            if (Model is null || Model.CanAcceptDropData is null)
            {
                DropStyle = "border: 2px dashed red;";
            }
            else if (Model.CanAcceptDropData(_draggingNode.Model, Model))
            {
                DropStyle = "border: 2px dashed green;";
            }
            else
            {
                DropStyle = "border: 2px dashed red;";
            }
        }
        private void DragLeaveHandler()
        {
            if (_draggingNode == this) { return; }

            DropStyle = string.Empty;
        }
        private async Task DropHandler()
        {
            if (_draggingNode is null || _draggingNode == this) { return; }

            DropStyle = string.Empty;

            if (Model is null || Model.CanAcceptDropData is null || Model.DropDataHandler is null)
            {
                return;
            }

            TreeNodeModel source = _draggingNode.Model;

            try
            {
                await Model.DropDataHandler(source, Model);
            }
            finally
            {
                _draggingNode = null;
            }
        }
    }
}
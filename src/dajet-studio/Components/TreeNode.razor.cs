using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.ComponentModel;

namespace DaJet.Studio.Components
{
    public partial class TreeNode : ComponentBase
    {
        private ElementReference TitleInput;
        private string _title = string.Empty;
        [Parameter] public TreeNodeModel Model { get; set; }
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (Model.CanBeEdited && Model.IsInEditMode)
            {
                await Task.Delay(500); // hack =)
                await JSRuntime.InvokeVoidAsync("BlazorFocusElement", TitleInput);
            }
        }
        private async Task ToggleClick(MouseEventArgs args)
        {
            if (Model != null && Model.UseToggle)
            {
                await Model?.ToggleCommand();
            }
        }
        private async Task OpenContextMenu(MouseEventArgs args)
        {
            await Model?.ContextMenuCommand(DialogService);
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
    }
}
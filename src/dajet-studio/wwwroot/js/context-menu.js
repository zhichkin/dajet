const dajet_context_menu_id = "dajet-context-menu";
function OpenCodeItemContextMenu(element)
{
    if (!element) { return; }

    let dialog = document.getElementById(dajet_context_menu_id);

    if (dialog)
    {
        let box = element.getBoundingClientRect();

        dialog.style.top = box.bottom + "px";
        dialog.style.left = box.left + "px";
        
        dialog.addEventListener("click", DialogClickHandler);

        dialog.showModal();
    }
}
function CloseCodeItemContextMenu()
{
    let dialog = document.getElementById(dajet_context_menu_id);

    if (dialog)
    {
        dialog.close();

        dialog.removeEventListener("click", DialogClickHandler);
    }
}
function DialogClickHandler(event)
{
    let dialog = document.getElementById(dajet_context_menu_id);

    if (dialog)
    {
        let box = dialog.getBoundingClientRect();

        if (event.clientX < box.left ||
            event.clientX > box.right ||
            event.clientY < box.top ||
            event.clientY > box.bottom) {
            CloseCodeItemContextMenu();
        }
    }
}

const infobase_context_menu_id = "infobase-context-menu";
function OpenInfoBaseContextMenu(element)
{
    if (!element) { return; }

    let dialog = document.getElementById(infobase_context_menu_id);

    if (dialog)
    {
        let box = element.getBoundingClientRect();

        dialog.style.top = box.bottom + "px";
        dialog.style.left = box.left + "px";

        dialog.addEventListener("click", InfoBaseContextMenuClickHandler);

        dialog.showModal();
    }
}
function CloseInfoBaseContextMenu()
{
    let dialog = document.getElementById(infobase_context_menu_id);

    if (dialog)
    {
        dialog.close();

        dialog.removeEventListener("click", InfoBaseContextMenuClickHandler);
    }
}
function InfoBaseContextMenuClickHandler(event)
{
    let dialog = document.getElementById(infobase_context_menu_id);

    if (dialog)
    {
        let box = dialog.getBoundingClientRect();

        if (event.clientX < box.left ||
            event.clientX > box.right ||
            event.clientY < box.top ||
            event.clientY > box.bottom)
        {
            CloseInfoBaseContextMenu();
        }
    }
}

const metadata_object_context_menu_id = "metadata-object-context-menu";
function OpenMetadataObjectContextMenu(element)
{
    if (!element) { return; }

    let dialog = document.getElementById(metadata_object_context_menu_id);

    if (dialog)
    {
        let box = element.getBoundingClientRect();

        dialog.style.top = box.bottom + "px";
        dialog.style.left = box.left + "px";

        dialog.addEventListener("click", MetadataObjectContextMenuClickHandler);

        dialog.showModal();
    }
}
function CloseMetadataObjectContextMenu()
{
    let dialog = document.getElementById(metadata_object_context_menu_id);

    if (dialog)
    {
        dialog.close();

        dialog.removeEventListener("click", MetadataObjectContextMenuClickHandler);
    }
}
function MetadataObjectContextMenuClickHandler(event)
{
    let dialog = document.getElementById(metadata_object_context_menu_id);

    if (dialog)
    {
        let box = dialog.getBoundingClientRect();

        if (event.clientX < box.left ||
            event.clientX > box.right ||
            event.clientY < box.top ||
            event.clientY > box.bottom)
        {
            CloseMetadataObjectContextMenu();
        }
    }
}

var dajet_modal_dialog_id = "";
function OpenDaJetModalDialog(id)
{
    dajet_modal_dialog_id = id;

    let dialog = document.getElementById(dajet_modal_dialog_id);

    if (dialog)
    {
        dialog.addEventListener("click", DaJetModalDialogClickHandler);

        dialog.showModal();
    }
}
function CloseDaJetModalDialog(id)
{
    let dialog = document.getElementById(id);

    if (dialog)
    {
        dialog.close();

        dialog.removeEventListener("click", DaJetModalDialogClickHandler);

        dajet_modal_dialog_id = "";
    }
}
function DaJetModalDialogClickHandler(event)
{
    let dialog = document.getElementById(dajet_modal_dialog_id);

    if (dialog)
    {
        let box = dialog.getBoundingClientRect();

        if (event.clientX < box.left ||
            event.clientX > box.right ||
            event.clientY < box.top ||
            event.clientY > box.bottom) {
            CloseDaJetModalDialog(dajet_modal_dialog_id);
        }
    }
}
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

const metadata_entity_context_menu_id = "metadata-entity-context-menu";
function OpenMetadataEntityContextMenu(element)
{
    if (!element) { return; }

    let dialog = document.getElementById(metadata_entity_context_menu_id);

    if (dialog)
    {
        let box = element.getBoundingClientRect();

        dialog.style.top = box.bottom + "px";
        dialog.style.left = box.left + "px";

        dialog.addEventListener("click", MetadataEntityContextMenuClickHandler);

        dialog.showModal();
    }
}
function CloseMetadataEntityContextMenu()
{
    let dialog = document.getElementById(metadata_entity_context_menu_id);

    if (dialog)
    {
        dialog.close();

        dialog.removeEventListener("click", MetadataEntityContextMenuClickHandler);
    }
}
function MetadataEntityContextMenuClickHandler(event)
{
    let dialog = document.getElementById(metadata_entity_context_menu_id);

    if (dialog)
    {
        let box = dialog.getBoundingClientRect();

        if (event.clientX < box.left ||
            event.clientX > box.right ||
            event.clientY < box.top ||
            event.clientY > box.bottom)
        {
            CloseMetadataEntityContextMenu();
        }
    }
}
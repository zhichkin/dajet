
function OpenCodeItemContextMenu(text)
{
    alert(text);

    let dialog = document.getElementById("dajet-code-context-menu");

    if (dialog) { dialog.showModal(); }
}
function CloseCodeItemContextMenu(text) {

    alert(text);

    let dialog = document.getElementById("dajet-code-context-menu");

    dialog.close();

    alert(dialog.retunValue);
}
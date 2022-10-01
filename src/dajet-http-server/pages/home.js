function test() {
    alert("test");
}

window.addEventListener("load", initTreeView, { once: true });

function initTreeView() {
    let ul = document.getElementById("MainTreeView");
    ul.replaceChildren();
    let tree = new TreeNode(ul);
    GetInfoBases(tree);
}

async function GetMetadata(url) {

    let footer = document.getElementById("footer");
    footer.replaceChildren();

    let response = await fetch(url, { method: "GET" });

    if (!response.ok) {

        let message = await response.text();
        let text = document.createTextNode(message);
        footer.replaceChildren(text);
        return null;
    }

    return await response.json();
}

async function GetInfoBases(node) {

    if (node.Count > 0) { return; }

    let data = await GetMetadata("/md");

    if (data == null) { return; }

    if (data.length == 0) {
        footer.replaceChildren(document.createTextNode("Список информационных баз 1С пуст."));
        return;
    }

    for (let infoBase of data) {

        let child = new TreeNode();
        child.Model = infoBase;
        child.Title = infoBase.Name;
        child.Image = "/ui/img/database.png";
        child.OnMouseClick = GetCatalogs;
        child.OnContextMenu = function (model, event) {
            alert(model.Name + " {X = " + event.clientX + " : Y = " + event.clientY + "}");
        };
        node.Add(child);
    }
}

async function GetCatalogs(node) {

    if (node.Count > 0) { return; }

    let data = await GetMetadata("/md/" + node.Model.Name + "/Справочник");

    if (data == null || data.length == 0) { return; }

    for (let catalog of data) {

        let child = new TreeNode();
        child.Model = catalog;
        child.Title = catalog.Name;
        child.Image = "/ui/img/Справочник.png";
        child.OnMouseClick = GetProperties;
        child.OnContextMenu = function (model, event) {
            alert(model.Name + " {X = " + event.clientX + " : Y = " + event.clientY + "}");
        };
        node.Add(child);
    }
}

async function GetProperties(node) {

    if (node.Count > 0) { return; }

    let catalog = await GetMetadata("/md/" + "dajet-metadata-ms" + "/Справочник/" + node.Model.Name);

    if (catalog == null) { return; }

    for (let property of catalog.Properties) {

        let child = new TreeNode();
        child.Model = property;
        child.Title = property.Name;
        child.Image = "/ui/img/Реквизит.png";
        node.Add(child);
    }

    for (let table of catalog.TableParts) {

        let tableNode = new TreeNode();
        tableNode.Model = table;
        tableNode.Title = table.Name;
        tableNode.Image = "/ui/img/ВложеннаяТаблица.png";
        node.Add(tableNode);

        for (let property of table.Properties) {

            let child = new TreeNode();
            child.Model = property;
            child.Title = property.Name;
            child.Image = "/ui/img/Реквизит.png";
            tableNode.Add(child);
        }
    }
}
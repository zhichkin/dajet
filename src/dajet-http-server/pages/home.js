window.addEventListener("load", initMainTreeView, { once: true });

var MainTreeView;

function initMainTreeView() {
    let ul = document.getElementById("MainTreeView");
    ul.replaceChildren();
    MainTreeView = new TreeNode(ul);
    GetInfoBases(MainTreeView);
}
function AddInfoBaseTreeNode(root, infoBase) {

    let node = new TreeNode();
    node.Url = "/md/" + infoBase.Name;
    node.Model = infoBase;
    node.Title = infoBase.Name;
    node.Image = "/ui/img/database.png";
    //node.OnMouseClick = ShowInfoBaseProperties;
    //node.ContextMenu = new ContextMenu();
    root.Add(node);

    let meta = new TreeNode();
    meta.Url = "/md/" + infoBase.Name + "/Справочник";
    meta.Title = "Справочники";
    meta.Image = "/ui/img/Справочник.png";
    meta.OnMouseClick = GetMetadataObjects;
    node.Add(meta);

    meta = new TreeNode();
    meta.Url = "/md/" + infoBase.Name + "/Документ";
    meta.Title = "Документы";
    meta.Image = "/ui/img/Документ.png";
    meta.OnMouseClick = GetMetadataObjects;
    node.Add(meta);

    meta = new TreeNode();
    meta.Url = "/md/" + infoBase.Name + "/Перечисление";
    meta.Title = "Перечисления";
    meta.Image = "/ui/img/Перечисление.png";
    meta.OnMouseClick = GetMetadataObjects;
    node.Add(meta);

    meta = new TreeNode();
    meta.Url = "/md/" + infoBase.Name + "/ПланВидовХарактеристик";
    meta.Title = "Планы видов характеристик";
    meta.Image = "/ui/img/ПланВидовХарактеристик.png";
    meta.OnMouseClick = GetMetadataObjects;
    node.Add(meta);

    meta = new TreeNode();
    meta.Url = "/md/" + infoBase.Name + "/РегистрСведений";
    meta.Title = "Регистры сведений";
    meta.Image = "/ui/img/РегистрСведений.png";
    meta.OnMouseClick = GetMetadataObjects;
    node.Add(meta);

    meta = new TreeNode();
    meta.Url = "/md/" + infoBase.Name + "/РегистрНакопления";
    meta.Title = "Регистры накопления";
    meta.Image = "/ui/img/РегистрНакопления.png";
    meta.OnMouseClick = GetMetadataObjects;
    node.Add(meta);

    meta = new TreeNode();
    meta.Url = "/md/" + infoBase.Name + "/ПланОбмена";
    meta.Title = "Планы обмена";
    meta.Image = "/ui/img/ПланОбмена.png";
    meta.OnMouseClick = GetMetadataObjects;
    node.Add(meta);
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
async function GetInfoBases(root) {

    if (root.Count > 0) { return; }

    let data = await GetMetadata("/md");

    if (data == null) { return; }

    if (data.length == 0) {
        footer.replaceChildren(document.createTextNode("Список информационных баз 1С пуст."));
        return;
    }

    let selector = document.getElementById("InfoBaseSelector");
    selector.replaceChildren();

    for (let infoBase of data) {

        AddInfoBaseOption(selector, infoBase);

        AddInfoBaseTreeNode(root, infoBase);
    }
}
async function GetMetadataObjects(node) {
    if (node.Count > 0) { return; }

    let data = await GetMetadata(node.Url);

    if (data == null || data.length == 0) { return; }

    for (let item of data) {

        let child = new TreeNode();
        child.Url = node.Url + "/" + item.Name;
        child.Model = item;
        child.Title = item.Name;
        child.Image = node.Image;
        child.OnMouseClick = GetMetadataProperties;
        node.Add(child);
    }
}
async function GetMetadataProperties(node) {

    if (node.Count > 0) { return; }

    let metadata = await GetMetadata(node.Url);

    if (metadata == null) { return; }

    for (let property of metadata.Properties) {

        let child = new TreeNode();
        child.Model = property;
        child.Title = property.Name;
        if (property.Purpose == 1) {
            child.Image = "/ui/img/Реквизит.png";
        }
        else if (property.Purpose == 2) {
            child.Image = "/ui/img/Измерение.png";
        }
        else if (property.Purpose == 3) {
            child.Image = "/ui/img/Ресурс.png";
        }
        else {
            child.Image = "/ui/img/Реквизит.png";
        }
        node.Add(child);
    }

    if (metadata.hasOwnProperty("TableParts")) {
        for (let table of metadata.TableParts) {

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
}

// MAIN MENU
function RegisterInfoBase() {
    let popup = new PopupWindow();
    popup.Title("Добавить информационную базу");
    popup.Model({
        "Name": "",
        "Description": "",
        "DatabaseProvider": "",
        "ConnectionString": ""
    });
    popup.OnConfirm(insertInfoBase);
    popup.Show("/ui/html/add-info-base-popup.html");
}
async function insertInfoBase(infoBase) {

    let footer = document.getElementById("footer");
    footer.replaceChildren();

    let response = await fetch("/md",
        {
            method: 'POST',
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(infoBase)
        });

    if (!response.ok) {

        let message = await response.text();
        let text = document.createTextNode(message);
        footer.replaceChildren(text);
        return;
    }

    AddInfoBaseTreeNode(MainTreeView, infoBase);

    let selector = document.getElementById("InfoBaseSelector");
    AddInfoBaseOption(selector, infoBase);
}
async function RemoveInfoBase() {

    let selector = document.getElementById("InfoBaseSelector");
    let name = selector.value;

    if (!confirm("Удалить '" + name + "' из списка ?")) {
        return;
    }

    let footer = document.getElementById("footer");
    footer.replaceChildren();

    let response = await fetch("/md",
        {
            method: "DELETE",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ "Name": name })
        });

    if (!response.ok) {

        let message = await response.text();
        let text = document.createTextNode(message);
        footer.replaceChildren(text);
        return;
    }

    initMainTreeView();
}

// InfoBase Selector
function AddInfoBaseOption(selector, infoBase) {
    if (selector != null) {
        let option = document.createElement("option");
        option.value = infoBase.Name;
        let optionText = document.createTextNode(infoBase.Name);
        option.appendChild(optionText);
        selector.appendChild(option);
    }
}

// Open new query page
function OpenQueryPage() {

    let selector = document.getElementById("InfoBaseSelector");
    
    UiLoader.GetJavaScript("ui/js/1ql-query.js", () => {
        QueryViewController.createView({
            "InfoBaseName": selector.value
        });
    });
}

// CREATE DYNAMIC TABLE
function createTable(data) {

    let table = document.createElement('table');
    table.style.border = '1px solid black';

    if (data.length > 0) {

        createTableHeaders(table, data[0]);
    }

    for (let item of data) {

        createTableRow(table, item);
    }

    return table;
}
function createTableHeaders(table, item) {

    let tr = table.insertRow();

    let names = Object.getOwnPropertyNames(item);

    for (let name of names) {

        let td = createTableRowCell(tr, name);
        td.style.fontWeight = 'bold';
    }
}
function createTableRow(table, item) {

    let tr = table.insertRow();

    let names = Object.getOwnPropertyNames(item);

    for (let name of names) {

        createTableRowCell(tr, item[name]);
    }
}
function createTableRowCell(tr, value) {

    let td = tr.insertCell();
    td.style.padding = '5px';
    td.style.border = '1px solid black';

    let text = document.createTextNode(value);

    td.appendChild(text);
    tr.appendChild(td);

    return td;
}
class MetadataViewController {
    constructor() { }
    static async createView(model) {

        let _model = model;

        UiLoader.GetCss("ui/js/metadata-view.css", null);

        let content = document.getElementById("content");
        content.innerHTML = await UiLoader.GetHtml("/ui/html/metadata-view.html");

        let closeView = content.querySelector(".view-close-button");
        if (closeView != null) {
            closeView.onclick = () => { content.replaceChildren(); };
        }

        for (let element of content.getElementsByTagName("span")) {
            let propertyName = element.getAttribute("data-bind");
            if (_model.hasOwnProperty(propertyName)) {
                let propertyValue = _model[propertyName];
                let textNode = document.createTextNode(propertyValue);
                element.replaceChildren(textNode);
            }
        }
    }
    static SelectTab(idx) { //idx - выбранная вкладка
        let tabCount = 3;
        for (let i = 1; i <= tabCount; ++i) {
            let _tab = document.getElementById("metadata-view-tab" + i); //Div - содержимое вкладки i
            let _tabTitle = document.getElementById("metadata-view-tab-title" + i); //Li - вкладка i
            if (idx != i) { //Если это не выбранная вкладка
                _tab.setAttribute("class", "metadata-view-tab");
                _tabTitle.setAttribute("class", "metadata-view-tab-title");
            }
            else { //Если это выбранная вкладка
                _tab.setAttribute("class", "metadata-view-tab selected");
                _tabTitle.setAttribute("class", "metadata-view-tab-title selected-li");
            }
        }
    }
}
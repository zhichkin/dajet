class QueryViewController {
    constructor() { }
    static async createView(model) {

        let _model = model;

        let content = document.getElementById("content");
        content.innerHTML = await UiLoader.GetHtml("/ui/html/1ql-query.html");

        let query_view_script = content.querySelector(".query-view-script");
        let query_view_result = content.querySelector(".query-view-result");
        let query_view_error = content.querySelector(".query-view-error");

        let rejectButton = content.querySelector(".view-close-button");
        if (rejectButton != null) {
            rejectButton.onclick = () => { content.replaceChildren(); };
        }

        for (let element of content.getElementsByTagName("span")) {
            let propertyName = element.getAttribute("data-bind");
            if (_model.hasOwnProperty(propertyName)) {
                let propertyValue = _model[propertyName];
                let textNode = document.createTextNode(propertyValue);
                element.replaceChildren(textNode);
            }
        }

        let generateSql = content.querySelector(".query-view-generate-sql");
        if (generateSql != null) {
            generateSql.onclick = async () => {

                query_view_error.replaceChildren();
                query_view_result.replaceChildren();

                let data = await QueryExecutor.generateSql(_model.InfoBaseName, query_view_script.value);

                if (data.Success) {
                    let sql = document.createTextNode(data.Script);
                    query_view_result.replaceChildren(sql);
                }
                else {
                    let error = document.createTextNode(data.Error);
                    query_view_error.replaceChildren(error);
                }
            };
        }

        let executeJson = content.querySelector(".query-view-execute-json");
        if (executeJson != null) {
            executeJson.onclick = async () => {

                query_view_error.replaceChildren();
                query_view_result.replaceChildren();

                let data = await QueryExecutor.executeScript(_model.InfoBaseName, query_view_script.value);

                if (data.Success) {
                    let json = document.createTextNode(JSON.stringify(data.Result));
                    query_view_result.replaceChildren(json);
                }
                else {
                    let error = document.createTextNode(data.Error);
                    query_view_error.replaceChildren(error);
                }
            };
        }

        let executeScript = content.querySelector(".query-view-execute-script");
        if (executeScript != null) {
            executeScript.onclick = async () => {

                query_view_error.replaceChildren();
                query_view_result.replaceChildren();

                let data = await QueryExecutor.executeScript(_model.InfoBaseName, query_view_script.value);

                if (data.Success) {
                    let table = createTable(data.Result);
                    query_view_result.replaceChildren(table);
                }
                else {
                    let error = document.createTextNode(data.Error);
                    query_view_error.replaceChildren(error);
                }
            };
        }
    }
}
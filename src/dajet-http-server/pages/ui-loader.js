class UiLoader {
    constructor() { }
    static async GetHtml(url) {
        let response = await fetch(url, { method: "GET" });
        return await response.text();
    }
    static GetCss(url, callback) {
        let style = document.createElement("link");
        style.setAttribute("href", url);
        style.setAttribute("type", "text/css");
        style.setAttribute("rel", "stylesheet");
        style.onload = callback;
        style.onerror = (event) => { console.log(`Error loading style: ${url}`, event); };
        document.body.appendChild(style);
    }
    static GetJavaScript(url, callback) {
        let script = document.createElement("script");
        script.setAttribute("src", url);
        script.setAttribute("type", "text/javascript");
        script.onload = callback;
        script.onerror = (event) => { console.log(`Error loading script: ${url}`, event); };
        document.body.appendChild(script);
    }
    static BindElementToModel(element, model) {
        let propertyName = element.getAttribute("data-bind");
        if (propertyName != null && model != null && model.hasOwnProperty(propertyName)) {
            element.addEventListener("change", function (event) {
                model[propertyName] = event.target.value;
            });
            element.value = model[propertyName];
            //if (element.nodeName == "SELECT") {
            //    model[propertyName] = element.value;
            //}
        }
    }
}
class UiLoader {
    constructor() { }
    static GetHtml(html_id) {
        let content = document.getElementById(html_id);
        return content.innerHTML;
    }
    static GetCss(url, callback) {
        let styles = document.body.getElementsByTagName("link");
        for (let i = 0; i < styles.length; i++) {
            if (styles[i].getAttribute("href") == url) {
                if (callback != null) {
                    callback();
                }
                return;
            }
        }
        let style = document.createElement("link");
        style.setAttribute("href", url);
        style.setAttribute("type", "text/css");
        style.setAttribute("rel", "stylesheet");
        style.onload = callback;
        style.onerror = (event) => { console.log(`Error loading style: ${url}`, event); };
        document.body.appendChild(style);
    }
    static GetJavaScript(url, callback) {
        let scripts = document.body.getElementsByTagName("script");
        for (let i = 0; i < scripts.length; i++) {
            if (scripts[i].getAttribute("src") == url) {
                if (callback != null) {
                    callback();
                }
                return;
            }
        }
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
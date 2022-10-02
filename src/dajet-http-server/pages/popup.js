function PopupWindow() {

    let closePopup = function () {
        popup.classList.toggle("active");
        content.replaceChildren();
    };

    let popup = document.createElement("div");
    popup.className = "popup";

    let button = document.createElement("input");
    button.type = "button";
    button.value = "✕";
    button.className = "popup-close-button";
    button.onclick = closePopup;

    let header = document.createElement("div");
    header.className = "popup-header";

    let title = document.createElement("span");
    title.style.fontWeight = "bold";
    header.appendChild(title);

    let icons = document.createElement("div");
    icons.className = "popup-icons";
    icons.appendChild(button);

    let content = document.createElement("div");
    content.className = "popup-content";

    popup.appendChild(icons);
    popup.appendChild(header);
    popup.appendChild(content);
    document.body.appendChild(popup);

    let model = null;
    this.Model = function (value) {
        model = value;
    };
    let confirm = null;
    this.OnConfirm = function (callback) {
        confirm = callback;
    };
    this.Title = function (value) {
        title.innerText = value;
    };
    this.Show = async function (url) {
        content.innerHTML = await GetHtmlPart(url);

        let confirmButton = content.querySelector(".confirm-button");
        if (confirmButton != null && confirm != null) {
            confirmButton.onclick = function () {
                closePopup();
                confirm(model);
            };
        }

        let rejectButton = content.querySelector(".reject-button");
        if (rejectButton != null) {
            rejectButton.onclick = closePopup;
        }

        if (model != null) {
            for (let element of content.getElementsByTagName("input")) {
                BindElementToModel(element, model);
            }
            for (let element of content.getElementsByTagName("select")) {
                BindElementToModel(element, model);
            }
        }

        popup.classList.toggle("active");
    };
}
function BindElementToModel(element, model) {
    let propertyName = element.getAttribute("data-bind");
    if (propertyName != null && model != null && model.hasOwnProperty(propertyName)) {
        element.addEventListener("change", function (event) {
            model[propertyName] = event.target.value;
        });
        if (element.nodeName == "SELECT") {
            model[propertyName] = element.value;
        }
    }
}
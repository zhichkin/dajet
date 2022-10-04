async function SayHello(model) {

    let _model = model;
    let content = document.getElementById("content");

    content.innerHTML = await UiLoader.GetHtml("/ui/html/test.html");

    for (let element of content.getElementsByTagName("input")) {
        UiLoader.BindElementToModel(element, model);
    }
    for (let element of content.getElementsByTagName("select")) {
        UiLoader.BindElementToModel(element, model);
    }

    let confirmButton = content.querySelector(".confirm-button");
    if (confirmButton != null) {
        confirmButton.onclick = function () {
            for (let property in _model) {
                if (_model.hasOwnProperty(property)) {
                    console.log(`${property}: ${_model[property]}`);
                }
            }
        };
    }

    let rejectButton = content.querySelector(".reject-button");
    if (rejectButton != null) {
        rejectButton.onclick = () => { content.replaceChildren(); };
    }
}
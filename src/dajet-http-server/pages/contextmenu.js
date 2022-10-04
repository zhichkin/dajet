function ContextMenu() {

    let div = document.createElement("div");
    div.className = "context-menu";
    document.body.appendChild(div);

    let closeMe = function () {
        div.classList.toggle("active");
    };

    document.addEventListener("click", function (event) {
        var button = event.which || event.button;
        if (button === 1) {
            closeMe();
        }
    });

    document.addEventListener("keyup", function (event) {
        if (event.keyCode === 27) {
            closeMe();
        }
    });

    this.Init = async function () {
        let html = await UiLoader.GetHtml("/ui/html/InfoBase.html");
        if (html != null) {
            div.innerHTML = html;
        }
    };
    this.Show = async function (model, event) {
        await this.Init();
        div.style.top = event.clientY;
        div.style.left = event.clientX;
        div.classList.toggle("active");
    };
    this.Close = closeMe;
}
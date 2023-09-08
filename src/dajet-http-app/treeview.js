function TreeNode(ul) {
    this.Url = "";
    this.View = ul;
    this.Model = null;
    this.Nodes = [];
    this.Count = 0;
    this.Title = "";
    this.TitleNode = null;
    this.Image = "";
    this.ContextMenu = null;
    this.OnMouseClick = null;
    this.Clear = function clearAll() {
        this.View.replaceChildren();
    };
    this.Add = function addNode(node) {

        let li = document.createElement("li");
        li.style.whiteSpace = "nowrap";

        let icon = document.createElement("img");
        icon.src = node.Image;
        icon.width = 16;
        icon.height = 16;
        icon.style.verticalAlign = "middle";
        li.appendChild(icon);

        let span = document.createElement("span");
        //span.className = "caret";
        span.style.cursor = "pointer";
        span.style.userSelect = "none";
        span.style.verticalAlign = "middle";
        span.addEventListener("click", function () {
            this.parentElement.querySelector(".nested").classList.toggle("active");
            //this.classList.toggle("caret-down");
            if (node.OnMouseClick != null) {
                node.OnMouseClick(node);
            }
        });
        span.addEventListener("contextmenu", function (event) {
            event.preventDefault();
            if (node.ContextMenu != null) {
                node.ContextMenu.Show(node.Model, event);
            }
        });
        let title = document.createTextNode(node.Title);
        node.TitleNode = title;
        span.appendChild(title);
        li.appendChild(span);

        let ul = document.createElement("ul");
        ul.className = "nested";
        node.View = ul;
        li.appendChild(ul);

        this.Count = this.Nodes.push(node);
        this.View.appendChild(li);
    };
}
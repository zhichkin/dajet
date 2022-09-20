function test() {

    alert("Hello from test function!");
}

window.addEventListener("load", initTreeView, { once: true });

function initTreeView() {

    var toggler = document.getElementsByClassName("caret");

    for (let i = 0; i < toggler.length; i++) {

        toggler[i].addEventListener("click", function () {

            this.parentElement.querySelector(".nested").classList.toggle("active");
            this.classList.toggle("caret-down");
        });
    }
}
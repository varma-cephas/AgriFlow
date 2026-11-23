// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

const sidebar = document.querySelector("#farmer-sidebar");
const sidebarOpenButton = document.querySelector("[data-sidebar-open]");
const sidebarCloseButtons = document.querySelectorAll("[data-sidebar-close]");

if (sidebar && sidebarOpenButton) {
    const closeSidebar = () => {
        sidebar.classList.remove("is-open");
        document.body.classList.remove("sidebar-is-open");
    };

    sidebarOpenButton.addEventListener("click", () => {
        sidebar.classList.add("is-open");
        document.body.classList.add("sidebar-is-open");
    });

    sidebarCloseButtons.forEach((button) => button.addEventListener("click", closeSidebar));
}

function openMealDialog() {
    const dialog = document.querySelector("dialog[data-meal-dialog]");
    if (!dialog || dialog.matches(":modal")) return;
    dialog.removeAttribute("open");
    dialog.showModal();
    dialog.addEventListener("cancel", event => {
        event.preventDefault();
        window.location.assign("/Meals");
    }, { once: true });
}
openMealDialog();
document.addEventListener("enhancedload", openMealDialog);

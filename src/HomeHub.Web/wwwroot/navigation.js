const mobile = window.matchMedia("(max-width: 760px)");
const selector = "details[data-responsive-menu]";
const initialized = new WeakSet();

function initialize(root) {
    const menus = root.matches?.(selector) ? [root] : [];
    menus.push(...root.querySelectorAll(selector));
    for (const menu of menus) {
        if (initialized.has(menu)) continue;
        menu.open = !mobile.matches;
        initialized.add(menu);
    }
}

initialize(document);
// Interactive routing can replace layouts without a full page load.
new MutationObserver(records => {
    for (const record of records) {
        for (const node of record.addedNodes) {
            if (node instanceof Element) initialize(node);
        }
    }
}).observe(document.body, { childList: true, subtree: true });

mobile.addEventListener("change", () => {
    for (const menu of document.querySelectorAll(selector)) menu.open = !mobile.matches;
});

document.addEventListener("click", event => {
    if (!mobile.matches || !(event.target instanceof Element)) return;
    const link = event.target.closest("a[href]");
    const menu = link?.closest(selector);
    if (!menu || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    menu.open = false;
});

document.addEventListener("keydown", event => {
    if (event.key !== "Escape" || !(event.target instanceof Element)) return;
    const menu = event.target.closest(selector);
    if (!menu?.open) return;
    menu.open = false;
    menu.querySelector("summary").focus();
    event.preventDefault();
});

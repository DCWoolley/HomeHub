function initializeSidebar() {
    const shell = document.querySelector('.app-shell');
    if (!shell) return;
    shell.dataset.sidebarOpen ??= String(!window.matchMedia('(max-width: 760px)').matches);
    shell.querySelector('[data-sidebar-toggle]').setAttribute('aria-expanded', shell.dataset.sidebarOpen);
}

document.addEventListener('click', event => {
    const toggle = event.target.closest('[data-sidebar-toggle]');
    if (!toggle) return;
    const shell = toggle.closest('.app-shell');
    shell.dataset.sidebarOpen = String(shell.dataset.sidebarOpen !== 'true');
    toggle.setAttribute('aria-expanded', shell.dataset.sidebarOpen);
});

document.addEventListener('keydown', event => {
    if (event.key !== 'Escape') return;
    const shell = document.querySelector('.app-shell');
    if (!shell) return;
    const menu = shell.querySelector('.account-menu[open]');
    if (menu) {
        menu.open = false;
        menu.querySelector('summary').focus();
    } else if (window.matchMedia('(max-width: 760px)').matches && shell.dataset.sidebarOpen === 'true') {
        shell.dataset.sidebarOpen = 'false';
        const toggle = shell.querySelector('[data-sidebar-toggle]');
        toggle.setAttribute('aria-expanded', 'false');
        toggle.focus();
    }
});

initializeSidebar();
Blazor.addEventListener('enhancedload', initializeSidebar);

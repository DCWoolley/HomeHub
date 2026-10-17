export function focusTrigger(id) {
    const trigger = document.getElementById(id) ?? document.getElementById('household-item-add');
    trigger?.focus({ preventScroll: true });
}

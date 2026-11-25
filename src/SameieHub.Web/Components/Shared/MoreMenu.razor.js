// Keyboard and mouse behaviour for the «Flere handlinger» menu (a details element). Opening with Enter or Space and moving between the items
// with Tab is done by the browser itself. This script only closes the menu in the ways people expect.
const attached = new WeakMap();

export function attach(details) {
    if (!details || attached.has(details)) {
        return;
    }

    const summary = details.querySelector(':scope > summary');

    const close = (returnFocus) => {
        if (!details.open) {
            return;
        }

        details.open = false;

        if (returnFocus && summary) {
            summary.focus();
        }
    };

    const onKeydown = (event) => {
        if (event.key === 'Escape' && details.open) {
            event.stopPropagation();
            close(true);
        }
    };

    const onDocumentClick = (event) => {
        if (details.open && !details.contains(event.target)) {
            close(false);
        }
    };

    // Used an item: close and put the focus back on the button that opened the menu, so the focus is never lost.
    const onItemClick = (event) => {
        if (event.target.closest('.more-menu-items button, .more-menu-items a')) {
            close(true);
        }
    };

    const onFocusOut = (event) => {
        if (details.open && event.relatedTarget && !details.contains(event.relatedTarget)) {
            close(false);
        }
    };

    details.addEventListener('keydown', onKeydown);
    details.addEventListener('click', onItemClick);
    details.addEventListener('focusout', onFocusOut);
    document.addEventListener('click', onDocumentClick);

    attached.set(details, { onKeydown, onItemClick, onFocusOut, onDocumentClick });
}

export function detach(details) {
    const handlers = attached.get(details);

    if (!handlers) {
        return;
    }

    details.removeEventListener('keydown', handlers.onKeydown);
    details.removeEventListener('click', handlers.onItemClick);
    details.removeEventListener('focusout', handlers.onFocusOut);
    document.removeEventListener('click', handlers.onDocumentClick);
    attached.delete(details);
}

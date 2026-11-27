// Called when the page shows the message that the program is stopping. The connection to the program is about to end on purpose,
// so the page must not offer to reconnect: it would only fail, and the message is the answer the person needs.
export function markStopped() {
    document.documentElement.setAttribute("data-sameiehub-stopped", "true");

    if (window.Blazor && typeof window.Blazor.disconnect === "function") {
        // Let the message reach the screen first, then end the connection from this side so nothing tries to bring it back.
        setTimeout(() => {
            try {
                window.Blazor.disconnect();
            } catch {
                // The connection may already be gone. The message stays on the page either way.
            }
        }, 300);
    }
}

// Petits helpers JS pour l'interop Blazor.
window.roadtrip = {
    isOnline: () => navigator.onLine,
    copyText: (text) => navigator.clipboard?.writeText(text) ?? Promise.resolve()
};

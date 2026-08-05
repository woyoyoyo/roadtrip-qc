// Petits helpers JS pour l'interop Blazor.
window.roadtrip = {
    isOnline: () => navigator.onLine,

    copyText: (text) => navigator.clipboard?.writeText(text) ?? Promise.resolve(),

    // Web Share API (native Android) avec fallback WhatsApp
    shareText: async (text) => {
        if (navigator.share) {
            await navigator.share({ text });
        } else {
            window.open('https://wa.me/?text=' + encodeURIComponent(text), '_blank');
        }
    },

    // Ouvre tous les jours, imprime, puis restaure l'état
    printPlan: () => {
        const details = document.querySelectorAll('details.day-card');
        const wasOpen = Array.from(details).map(d => d.open);
        details.forEach(d => { d.open = true; });
        window.addEventListener('afterprint', () => {
            details.forEach((d, i) => { d.open = wasOpen[i]; });
        }, { once: true });
        window.print();
    },

    // Fait défiler un conteneur (fil de discussion de l'assistant) vers le bas
    scrollToBottom: (el) => {
        if (el) el.scrollTop = el.scrollHeight;
    },

    // Amène en vue un élément par son id (formulaire d'édition inline)
    scrollToId: (id) => {
        const el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    },

    // ── Reconnaissance vocale (Web Speech API) ────────────────────────────────
    startSpeech: (dotnetRef) => {
        const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SR) return false;
        const r = new SR();
        r.lang = 'fr-FR';
        r.interimResults = false;
        r.maxAlternatives = 1;
        r.continuous = false;
        r.onresult = e => dotnetRef.invokeMethodAsync('OnSpeechResult', e.results[0][0].transcript);
        r.onerror = e => dotnetRef.invokeMethodAsync('OnSpeechError', e.error);
        r.onend = () => dotnetRef.invokeMethodAsync('OnSpeechEnd');
        r.start();
        window._roadtrip_sr = r;
        return true;
    },

    stopSpeech: () => { window._roadtrip_sr?.stop(); }
};

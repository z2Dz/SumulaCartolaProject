// @ts-nocheck
// wwwroot/js/instalar.js
// Lê o evento "beforeinstallprompt" que o index.html guarda em window.__instalarEvento.

const CHAVE_ADIADO = 'instalar-adiado';
const DIAS_ADIADO = 7;

/** True se o app já está rodando instalado (tela cheia) ou foi instalado antes. */
export function jaInstalado() {
    const standalone = window.matchMedia('(display-mode: standalone)').matches
        || window.navigator.standalone === true;
    return standalone || localStorage.getItem('app-instalado') === '1';
}

/** True em iPhone/iPad, onde não existe o botão automático de instalar. */
export function ehIos() {
    const ua = navigator.userAgent || '';
    return /iphone|ipad|ipod/i.test(ua)
        || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
}

/** True se a pessoa tocou em "Agora não" nos últimos dias. */
export function adiadoRecente() {
    const quando = Number(localStorage.getItem(CHAVE_ADIADO) || 0);
    return quando > 0 && (Date.now() - quando) < DIAS_ADIADO * 24 * 60 * 60 * 1000;
}

export function adiar() {
    localStorage.setItem(CHAVE_ADIADO, String(Date.now()));
}

/** Espera até "esperaMs" pelo evento de instalação (Android/Chrome/Brave/Edge). */
export function podeInstalar(esperaMs = 0) {
    return new Promise(resolve => {
        if (window.__instalarEvento) return resolve(true);
        if (!esperaMs) return resolve(false);
        const limite = setTimeout(() => resolve(false), esperaMs);
        window.addEventListener('beforeinstallprompt', () => {
            clearTimeout(limite);
            resolve(true);
        }, { once: true });
    });
}

/** Abre a janela nativa de instalação. Devolve "accepted", "dismissed" ou "indisponivel". */
export async function instalar() {
    const evento = window.__instalarEvento;
    if (!evento) return 'indisponivel';
    evento.prompt();
    const escolha = await evento.userChoice;
    window.__instalarEvento = null;
    if (escolha.outcome === 'accepted') localStorage.setItem('app-instalado', '1');
    return escolha.outcome;
}

let aoInstalarHandler = null;

/** Avisa o Blazor quando o app é instalado por qualquer caminho (inclusive pelo menu do navegador). */
export function observarInstalacao(ref) {
    pararObservacao();
    aoInstalarHandler = () => {
        localStorage.setItem('app-instalado', '1');
        window.__instalarEvento = null;
        ref.invokeMethodAsync('AoInstalado');
    };
    window.addEventListener('appinstalled', aoInstalarHandler, { once: true });
}

export function pararObservacao() {
    if (!aoInstalarHandler) return;
    window.removeEventListener('appinstalled', aoInstalarHandler);
    aoInstalarHandler = null;
}
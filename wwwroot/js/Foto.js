// Funções de imagem usadas pelo recorte de foto (RecortarFoto.razor).
export function medir(src) {
    return new Promise(ok => {
        const i = new Image();
        i.onload = () => ok([i.naturalWidth, i.naturalHeight]);
        i.onerror = () => ok([0, 0]);
        i.src = src;
    });
}

export function recortar(src, sx, sy, sw, sh, w, h) {
    return new Promise((ok, erro) => {
        const i = new Image();
        i.onload = () => {
            const c = document.createElement('canvas');
            c.width = w; c.height = h;
            const x = c.getContext('2d');
            x.imageSmoothingEnabled = true;
            x.imageSmoothingQuality = 'high';
            x.fillStyle = '#fff';
            x.fillRect(0, 0, w, h);
            x.drawImage(i, sx, sy, sw, sh, 0, 0, w, h);
            ok(c.toDataURL('image/jpeg', 0.92).split(',')[1]);
        };
        i.onerror = () => erro('imagem');
        i.src = src;
    });
}
// Funções de imagem usadas pelo recorte de foto (RecortarFoto.razor).
export function medir(src) {
    return new Promise(ok => {
        const i = new Image();
        i.onload = () => ok([i.naturalWidth, i.naturalHeight]);
        i.onerror = () => ok([0, 0]);
        i.src = src;
    });
}

function carregar(src) {
    return new Promise((ok, erro) => {
        const i = new Image();
        i.crossOrigin = 'anonymous';   // foto de outro domínio só funciona se o servidor liberar CORS
        i.onload = () => ok(i);
        i.onerror = () => erro('imagem');
        i.src = src;
    });
}

function novoCanvas(w, h) {
    const c = document.createElement('canvas');
    c.width = w; c.height = h;
    return c;
}

function camada(w, h) {
    const x = novoCanvas(w, h).getContext('2d');
    x.imageSmoothingEnabled = true;
    x.imageSmoothingQuality = 'high';
    return x;
}

// Contorno fino em volta do atleta: várias cópias da silhueta pintada de `cor`,
// deslocadas em círculo, e a imagem original por cima.
function desenharComBorda(ctx, w, h, desenhar, espessura, cor) {
    const sil = novoCanvas(w, h);
    const s = sil.getContext('2d');
    desenhar(s);
    s.globalCompositeOperation = 'source-in';
    s.fillStyle = cor;
    s.fillRect(0, 0, w, h);

    const passos = 32;
    for (let k = 0; k < passos; k++) {
        const a = (k / passos) * Math.PI * 2;
        ctx.drawImage(sil, Math.cos(a) * espessura, Math.sin(a) * espessura);
    }
    desenhar(ctx);
}

const espessuraSaida = (w, h) => Math.max(2, Math.round(Math.min(w, h) * 0.008));   // ~0,8% do lado menor (discreto)

// Recorte simples (sem remover fundo): fundo branco, JPEG.
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

// ---------------------------------------------------------------------------
// Seleção do atleta (remover fundo com seleção editável)
//
// 1) iniciarSelecao detecta o atleta no navegador (@imgly/background-removal, baixada sob
//    demanda; o modelo de ~40 MB fica em cache) e guarda uma MÁSCARA do tamanho da foto.
// 2) desenhar mostra a seleção em azul sobre a foto (resultado = false) ou o recorte
//    final sobre fundo xadrez (resultado = true). Com ferramenta "add"/"apagar" o usuário
//    pinta direto na máscara para corrigir o que a detecção errou.
// 3) recortarSelecao gera o PNG final (foto x máscara, com borda opcional).
// ---------------------------------------------------------------------------
let _lib = null;
let _sel = null;     // { orig, mask, auto, w, h }
let _vista = null;   // último estado de visualização enviado por desenhar()

export async function iniciarSelecao(src) {
    _lib ??= await import('https://cdn.jsdelivr.net/npm/@imgly/background-removal@1.5.5/+esm');
    const blob = await _lib.removeBackground(src, { output: { format: 'image/png' } });
    const url = URL.createObjectURL(blob);
    try {
        const [orig, corte] = await Promise.all([carregar(src), carregar(url)]);
        const w = orig.naturalWidth, h = orig.naturalHeight;
        const mask = camada(w, h); mask.drawImage(corte, 0, 0, w, h);
        const auto = camada(w, h); auto.drawImage(mask.canvas, 0, 0);
        _sel = { orig, mask: mask.canvas, auto: auto.canvas, w, h };
    } finally {
        URL.revokeObjectURL(url);
    }
}

export function redefinirSelecao() {
    if (!_sel) return;
    const c = _sel.mask.getContext('2d');
    c.globalCompositeOperation = 'copy';
    c.drawImage(_sel.auto, 0, 0);
    c.globalCompositeOperation = 'source-over';
}

export function liberarSelecao() { _sel = null; _vista = null; }

// foto recortada pela máscara, no tamanho natural
function recorteCompleto() {
    const x = camada(_sel.w, _sel.h);
    x.drawImage(_sel.orig, 0, 0);
    x.globalCompositeOperation = 'destination-in';
    x.drawImage(_sel.mask, 0, 0);
    return x.canvas;
}

// v = { w, h, saidaW, saidaH, resultado, borda, ox, oy, escala, ferramenta, pincel }
export function desenhar(tela, v) {
    if (!_sel) return;
    _vista = v;
    const K = 2;   // resolução interna 2x, para ficar nítido
    if (tela.width !== v.w * K || tela.height !== v.h * K) { tela.width = v.w * K; tela.height = v.h * K; }
    if (!tela._pronto) { tela._pronto = true; ligarPincel(tela); }

    const ctx = tela.getContext('2d');
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, tela.width, tela.height);
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';

    // mesma geometria da prévia normal: foto centrada no quadro, deslocada por (ox, oy)
    const dw = _sel.w * v.escala * K, dh = _sel.h * v.escala * K;
    const dx = tela.width / 2 + v.ox * K - dw / 2, dy = tela.height / 2 + v.oy * K - dh / 2;

    if (!v.resultado) {
        ctx.drawImage(_sel.orig, dx, dy, dw, dh);
        const t = camada(tela.width, tela.height);
        t.drawImage(_sel.mask, dx, dy, dw, dh);
        t.globalCompositeOperation = 'source-in';
        t.fillStyle = 'rgba(70,110,255,0.5)';
        t.fillRect(0, 0, tela.width, tela.height);
        ctx.drawImage(t.canvas, 0, 0);
    } else {
        const corte = camada(tela.width, tela.height);
        corte.drawImage(_sel.orig, dx, dy, dw, dh);
        corte.globalCompositeOperation = 'destination-in';
        corte.drawImage(_sel.mask, dx, dy, dw, dh);
        const pronto = corte.canvas;
        if (v.borda) {
            const esp = Math.max(2, Math.round(espessuraSaida(v.saidaW, v.saidaH) * (tela.width / v.saidaW)));
            desenharComBorda(ctx, tela.width, tela.height, c => c.drawImage(pronto, 0, 0), esp, 'rgba(255,255,255,0.95)');
        } else {
            ctx.drawImage(pronto, 0, 0);
        }
    }
}

// Pincel: pinta na máscara em coordenadas da foto. Só age com ferramenta "add" ou "apagar";
// com "mover" deixa o evento passar para o arrastar/zoom do Blazor.
function ligarPincel(tela) {
    const quadro = tela.parentElement;
    let ativo = false, ult = null;

    const ponto = e => {
        const r = tela.getBoundingClientRect();
        const lx = (e.clientX - r.left) * (_vista.w / r.width);
        const ly = (e.clientY - r.top) * (_vista.h / r.height);
        return {
            x: _sel.w / 2 + (lx - _vista.w / 2 - _vista.ox) / _vista.escala,
            y: _sel.h / 2 + (ly - _vista.h / 2 - _vista.oy) / _vista.escala
        };
    };

    const riscar = (a, b) => {
        const c = _sel.mask.getContext('2d');
        c.globalCompositeOperation = _vista.ferramenta === 'apagar' ? 'destination-out' : 'source-over';
        c.strokeStyle = '#fff';
        c.lineCap = 'round'; c.lineJoin = 'round';
        c.lineWidth = (_vista.pincel * 2) / _vista.escala;   // pincel = raio em px do quadro
        c.beginPath(); c.moveTo(a.x, a.y); c.lineTo(b.x, b.y); c.stroke();
        c.globalCompositeOperation = 'source-over';
    };

    quadro.addEventListener('pointerdown', e => {
        if (!_sel || !_vista || _vista.resultado || _vista.ferramenta === 'mover' || e.button !== 0) return;
        e.preventDefault(); e.stopPropagation();
        quadro.setPointerCapture(e.pointerId);
        ativo = true; ult = ponto(e);
        riscar(ult, ult); desenhar(tela, _vista);
    });
    quadro.addEventListener('pointermove', e => {
        if (!ativo) return;
        e.stopPropagation();
        const p = ponto(e);
        riscar(ult, p); ult = p; desenhar(tela, _vista);
    });
    const fim = e => {
        if (!ativo) return;
        ativo = false; ult = null;
        try { quadro.releasePointerCapture(e.pointerId); } catch { }
    };
    quadro.addEventListener('pointerup', fim);
    quadro.addEventListener('pointercancel', fim);
}

// PNG final (base64) com fundo transparente: foto x máscara, recortada no enquadramento escolhido.
export function recortarSelecao(sx, sy, sw, sh, w, h, borda) {
    if (!_sel) throw new Error('selecao');
    const corte = recorteCompleto();
    const c = novoCanvas(w, h);
    const x = c.getContext('2d');
    const desenhar = k => {
        k.imageSmoothingEnabled = true;
        k.imageSmoothingQuality = 'high';
        k.drawImage(corte, sx, sy, sw, sh, 0, 0, w, h);
    };
    if (borda) desenharComBorda(x, w, h, desenhar, espessuraSaida(w, h), 'rgba(255,255,255,0.95)');
    else desenhar(x);
    return c.toDataURL('image/png').split(',')[1];
}
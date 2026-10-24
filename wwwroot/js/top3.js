// Gera a imagem "TOP 3 da rodada" num canvas e baixa (ou compartilha, no celular) como PNG.
// Chamada pelo Ranking.razor: gerarTop3({ data, atletas: [{ nome, pos, pontos, foto, silhueta }] }) com os atletas já em ordem (1º, 2º, 3º).
// Também gera o card de um jogador só: gerarCardJogador({ nome, pos, pontos, data, foto, silhueta, escudo }) (moldura em img/moldura-card.png).
// "silhueta" é o caminho SVG (viewBox 100x100) da posição do atleta, usado quando ele não tem foto.

const L = 1600, A = 1020;
const FONTE = '"Arial Black", "Segoe UI Black", Impact, "Segoe UI", Arial, sans-serif';

const TEMAS = [
    // 1º, 2º, 3º
    { a: "#fff0a8", b: "#e3ad14", c: "#a67205", borda: "#ffe9a1", texto: "#1b1404", destaque: "#ffd54a", brilho: "rgba(255,200,40,.55)" },
    { a: "#f4f6f8", b: "#b3b9c2", c: "#737a85", borda: "#eceff3", texto: "#1c1f24", destaque: "#e4e8ee", brilho: "rgba(200,210,225,.35)" },
    { a: "#f4bd94", b: "#c6804f", c: "#80482a", borda: "#f5cfb0", texto: "#25140a", destaque: "#eaa878", brilho: "rgba(230,150,100,.35)" }
];

export async function gerarTop3(dados) {
    const canvas = document.createElement("canvas");
    canvas.width = L;
    canvas.height = A;
    const imagens = {
        vermelho: await carregar("img/time-vermelho.png"),
        amarelo: await carregar("img/time-amarelo.png"),
        // foto sem fundo: se já veio sem fundo usa direto; se tem fundo, remove (e se falhar usa a original)
        fotos: await Promise.all(dados.atletas.map(a => prepararFoto(a.foto))),
        // escudo do time em que cada atleta jogou (aparece embaixo da posição)
        escudosAtletas: await Promise.all(dados.atletas.map(a => (a.escudo ? carregar(a.escudo) : null)))
    };
    desenhar(canvas.getContext("2d"), dados, imagens);
    await entregar(canvas, "top3-rodada.png");
}

function carregar(src) {
    return new Promise(resolve => {
        const img = new Image();
        img.crossOrigin = "anonymous";   // sem isso a foto "suja" o canvas e o PNG não pode ser exportado
        img.onload = () => resolve(img);
        img.onerror = () => resolve(null);   // foto indisponível: o card usa a silhueta
        img.src = src;
    });
}

// Decide o que fazer com a foto ANTES de desenhar: detecta se ela já está sem fundo (PNG com transparência em volta
// do atleta, como as geradas pelo editor de foto) e só chama o removedor de fundo quando a foto tem fundo.
async function prepararFoto(src) {
    if (!src) return null;
    const img = await carregar(src);
    if (!img) return null;
    if (temTransparencia(img)) return recortarPorAlfa(img) || img;
    return (await recortarFundo(img)) || img;
}

function lerPixels(img, maxLado) {
    const esc = Math.min(1, maxLado / Math.max(img.width, img.height));
    const w = Math.max(1, Math.round(img.width * esc));
    const h = Math.max(1, Math.round(img.height * esc));
    const cv = document.createElement("canvas");
    cv.width = w;
    cv.height = h;
    const cx = cv.getContext("2d", { willReadFrequently: true });
    cx.drawImage(img, 0, 0, w, h);
    return { cv, w, h, px: cx.getImageData(0, 0, w, h).data };
}

// true quando a foto já está sem fundo: bastante área transparente e pelo menos 2 cantos transparentes
export function temTransparencia(img) {
    try {
        const { w, h, px } = lerPixels(img, 96);
        let transp = 0;
        for (let i = 0; i < w * h; i++) if (px[i * 4 + 3] < 128) transp++;
        const cantos = [[0, 0], [w - 1, 0], [0, h - 1], [w - 1, h - 1]].filter(([x, y]) => px[(y * w + x) * 4 + 3] < 128).length;
        return transp / (w * h) > .05 && cantos >= 2;
    } catch {
        return false;   // canvas "sujo" (foto sem CORS): trata como foto com fundo
    }
}

// Foto que já está sem fundo: só recorta o canvas na caixa do atleta (mesmo formato que recortarFundo devolve).
export function recortarPorAlfa(img) {
    try {
        const { cv, w, h, px } = lerPixels(img, 768);
        let x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (let y = 0; y < h; y++) {
            for (let x = 0; x < w; x++) {
                if (px[(y * w + x) * 4 + 3] > 128) {
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }
        }
        if (x1 < 0) return null;
        const out = document.createElement("canvas");
        out.width = x1 - x0 + 1;
        out.height = y1 - y0 + 1;
        out.getContext("2d").drawImage(cv, x0, y0, out.width, out.height, 0, 0, out.width, out.height);
        out.isRecorte = true;
        return out;
    } catch {
        return null;
    }
}

// ----- Remoção de fundo -----
// Usa o segmentador de pessoas do MediaPipe (roda no próprio navegador). A biblioteca e o modelo são baixados na primeira vez.
const MP = "https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.14";
const MODELO = "https://storage.googleapis.com/mediapipe-models/image_segmenter/selfie_segmenter/float16/latest/selfie_segmenter.tflite";
let segmentador = null;
let segmentadorFalhou = false;

async function obterSegmentador() {
    if (segmentador) return segmentador;
    if (segmentadorFalhou) return null;
    try {
        const visao = await import(`${MP}/vision_bundle.mjs`);
        const arquivos = await visao.FilesetResolver.forVisionTasks(`${MP}/wasm`);
        segmentador = await visao.ImageSegmenter.createFromOptions(arquivos, {
            baseOptions: { modelAssetPath: MODELO, delegate: "CPU" },
            runningMode: "IMAGE",
            outputConfidenceMasks: true,
            outputCategoryMask: false
        });
        return segmentador;
    } catch (e) {
        console.warn("Top 3: não consegui carregar o removedor de fundo; usando a foto original.", e);
        segmentadorFalhou = true;
        return null;
    }
}

// Devolve um canvas só com a pessoa (fundo transparente, recortado nas bordas dela), ou null se não deu.
async function recortarFundo(img) {
    try {
        const seg = await obterSegmentador();
        if (!seg) return null;

        const esc = Math.min(1, 768 / Math.max(img.width, img.height));
        const w = Math.max(1, Math.round(img.width * esc));
        const h = Math.max(1, Math.round(img.height * esc));
        const cv = document.createElement("canvas");
        cv.width = w;
        cv.height = h;
        const cx = cv.getContext("2d", { willReadFrequently: true });
        cx.drawImage(img, 0, 0, w, h);

        const res = seg.segment(cv);
        try {
            const mascaras = res.confidenceMasks || [];
            if (!mascaras.length) return null;
            const m = mascaras.length > 1 ? mascaras[1] : mascaras[0];   // com 2 classes: 0 = fundo, 1 = pessoa
            const conf = m.getAsFloat32Array();
            return aplicarMascara(cx, cv, conf, m.width, m.height, mascaras.length === 1);
        } finally {
            if (res && res.close) res.close();
        }
    } catch (e) {
        console.warn("Top 3: falha ao remover o fundo da foto.", e);
        return null;
    }
}

// Transforma a máscara em transparência e recorta o canvas na caixa da pessoa.
export function aplicarMascara(cx, cv, conf, mw, mh, talvezInvertida) {
    const w = cv.width, h = cv.height;
    const px = cx.getImageData(0, 0, w, h);
    const valor = (x, y) => conf[Math.min(mh - 1, Math.floor((y / h) * mh)) * mw + Math.min(mw - 1, Math.floor((x / w) * mw))];

    // Com uma classe só não sei se a máscara é "pessoa" ou "fundo": a borda da foto quase sempre é fundo
    let invertida = false;
    if (talvezInvertida) {
        let borda = 0, nb = 0, centro = 0, nc = 0;
        for (let i = 0; i < 40; i++) {
            const t = i / 39;
            for (const [x, y] of [[t * (w - 1), 0], [t * (w - 1), h - 1], [0, t * (h - 1)], [w - 1, t * (h - 1)]]) { borda += valor(x, y); nb++; }
            centro += valor(w * (.35 + .3 * t), h * (.35 + .3 * t)); nc++;
        }
        invertida = borda / nb > centro / nc;
    }

    let x0 = w, y0 = h, x1 = -1, y1 = -1;
    for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
            let m = valor(x, y);
            if (invertida) m = 1 - m;
            const a = Math.min(1, Math.max(0, (m - .35) / .3));   // borda suave entre 0,35 e 0,65
            const i = (y * w + x) * 4 + 3;
            px.data[i] = Math.round(px.data[i] * a);
            if (a > .5) {
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
        }
    }
    if (x1 < 0) return null;   // nenhuma pessoa encontrada

    cx.putImageData(px, 0, 0);
    const out = document.createElement("canvas");
    out.width = x1 - x0 + 1;
    out.height = y1 - y0 + 1;
    out.getContext("2d").drawImage(cv, x0, y0, out.width, out.height, 0, 0, out.width, out.height);
    out.isRecorte = true;
    return out;
}

export function desenhar(ctx, dados, imagens) {
    fundo(ctx);

    // escudos e título
    if (imagens.vermelho) escudo(ctx, imagens.vermelho, 330, 125, 150);
    if (imagens.amarelo) escudo(ctx, imagens.amarelo, 1270, 125, 150);

    ctx.textAlign = "center";
    ctx.textBaseline = "alphabetic";
    ctx.font = '56px "Segoe UI Emoji", "Apple Color Emoji", "Noto Color Emoji", sans-serif';
    ctx.fillStyle = "#f5c518";
    ctx.fillText("🏆", L / 2, 66);

    ctx.save();
    ctx.font = `italic 900 128px ${FONTE}`;
    const g = ctx.createLinearGradient(0, 70, 0, 170);
    g.addColorStop(0, "#fff3b0");
    g.addColorStop(.5, "#f2b705");
    g.addColorStop(1, "#b97a00");
    ctx.fillStyle = g;
    ctx.shadowColor = "rgba(255,190,30,.55)";
    ctx.shadowBlur = 28;
    ctx.fillText("TOP 3", L / 2, 172);
    ctx.restore();

    louro(ctx, L / 2 - 255, 118, 62, "#e8b923", -1);
    louro(ctx, L / 2 + 255, 118, 62, "#e8b923", 1);

    ctx.font = `700 22px ${FONTE}`;
    ctx.fillStyle = "rgba(255,255,255,.75)";
    ctx.fillText(("MELHORES DA RODADA" + (dados.data ? "  ·  " + dados.data : "")).toUpperCase(), L / 2, 212);

    // cards: 2º, 1º, 3º
    const [p1, p2, p3] = dados.atletas;
    const f = imagens.fotos || [];
    const e = imagens.escudosAtletas || [];
    carta(ctx, 185, 315, 350, 510, TEMAS[1], p2, f[1], 2, e[1]);
    carta(ctx, 575, 235, 450, 630, TEMAS[0], p1, f[0], 1, e[0]);
    carta(ctx, 1065, 315, 350, 510, TEMAS[2], p3, f[2], 3, e[2]);

    assinatura(ctx, 36, A - 30, 24, false);
}

function fundo(ctx) {
    const g = ctx.createLinearGradient(0, 0, 0, A);
    g.addColorStop(0, "#14171c");
    g.addColorStop(1, "#050607");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, L, A);

    // holofotes do estádio
    for (const [x, y] of [[40, 190], [140, 205], [1460, 205], [1560, 190]]) {
        const r = ctx.createRadialGradient(x, y, 0, x, y, 90);
        r.addColorStop(0, "rgba(255,255,255,.55)");
        r.addColorStop(1, "rgba(255,255,255,0)");
        ctx.fillStyle = r;
        ctx.fillRect(x - 90, y - 90, 180, 180);
    }
    // luz atrás do pódio
    const luz = ctx.createRadialGradient(L / 2, 560, 60, L / 2, 560, 760);
    luz.addColorStop(0, "rgba(255,255,255,.10)");
    luz.addColorStop(1, "rgba(255,255,255,0)");
    ctx.fillStyle = luz;
    ctx.fillRect(0, 0, L, A);
    // gramado
    const gr = ctx.createLinearGradient(0, 860, 0, A);
    gr.addColorStop(0, "rgba(20,60,35,0)");
    gr.addColorStop(1, "rgba(20,60,35,.35)");
    ctx.fillStyle = gr;
    ctx.fillRect(0, 860, L, A - 860);
}

function escudo(ctx, img, cx, cy, tam) {
    const prop = img.width / img.height;
    const w = prop >= 1 ? tam : tam * prop;
    const h = prop >= 1 ? tam / prop : tam;
    ctx.save();
    ctx.shadowColor = "rgba(0,0,0,.6)";
    ctx.shadowBlur = 18;
    ctx.drawImage(img, cx - w / 2, cy - h / 2, w, h);
    ctx.restore();
}

// ramo de louro em forma de parêntese: lado -1 = "(" e 1 = ")"
function louro(ctx, cx, cy, r, cor, lado) {
    ctx.save();
    ctx.fillStyle = cor;
    const n = 8;
    const ccx = cx - lado * r * .6;
    const a0 = lado < 0 ? Math.PI : 0;
    for (let i = 0; i < n; i++) {
        const a = a0 - .95 + (1.9 * i) / (n - 1);
        const x = ccx + r * Math.cos(a);
        const y = cy + r * 1.25 * Math.sin(a);
        const sentido = Math.atan2(1.25 * Math.cos(a), -Math.sin(a)) + (lado > 0 ? Math.PI : 0);   // sempre apontando para cima
        for (const s of [-1, 1]) {
            ctx.save();
            ctx.translate(x, y);
            ctx.rotate(sentido + s * .5);
            ctx.beginPath();
            ctx.ellipse(r * .2, 0, r * .2, r * .075, 0, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();
        }
    }
    ctx.restore();
}

// cópia chapada do recorte (só o contorno, numa cor), guardada na própria imagem para não refazer a cada uso
function silhueta(foto, cor) {
    if (foto._sil && foto._sil[cor]) return foto._sil[cor];
    const cv = document.createElement("canvas");
    cv.width = foto.width;
    cv.height = foto.height;
    const cx = cv.getContext("2d");
    cx.drawImage(foto, 0, 0);
    cx.globalCompositeOperation = "source-in";
    cx.fillStyle = cor;
    cx.fillRect(0, 0, cv.width, cv.height);
    (foto._sil = foto._sil || {})[cor] = cv;
    return cv;
}

function forma(ctx, x, y, w, h) {
    const s = h * .1;
    ctx.beginPath();
    ctx.moveTo(x, y + s * 1.6);
    ctx.lineTo(x + w / 2, y);
    ctx.lineTo(x + w, y + s * 1.6);
    ctx.lineTo(x + w, y + h - s * 1.2);
    ctx.lineTo(x + w / 2, y + h);
    ctx.lineTo(x, y + h - s * 1.2);
    ctx.closePath();
}

function ajustar(ctx, texto, maxW, tam, peso) {
    let t = tam;
    do {
        ctx.font = `${peso} ${t}px ${FONTE}`;
        t -= 1;
    } while (ctx.measureText(texto).width > maxW && t > 8);
}

function carta(ctx, x, y, w, h, t, atleta, foto, posicao, escudoTime) {
    if (!atleta) return;

    // brilho e corpo
    ctx.save();
    ctx.shadowColor = t.brilho;
    ctx.shadowBlur = posicao === 1 ? 60 : 34;
    forma(ctx, x, y, w, h);
    const g = ctx.createLinearGradient(x, y, x + w, y + h);
    g.addColorStop(0, t.a);
    g.addColorStop(.5, t.b);
    g.addColorStop(1, t.c);
    ctx.fillStyle = g;
    ctx.fill();
    ctx.restore();

    // listras diagonais
    ctx.save();
    forma(ctx, x, y, w, h);
    ctx.clip();
    ctx.strokeStyle = "rgba(255,255,255,.12)";
    ctx.lineWidth = 16;
    for (let k = -h; k < w + h; k += 44) {
        ctx.beginPath();
        ctx.moveTo(x + k, y + h);
        ctx.lineTo(x + k + h, y);
        ctx.stroke();
    }
    ctx.restore();

    // borda
    forma(ctx, x, y, w, h);
    ctx.lineJoin = "round";
    ctx.lineWidth = 7;
    ctx.strokeStyle = t.borda;
    ctx.stroke();

    // foto (ou silhueta)
    const fx = x + w * .31, fy = y + h * .13, fw = w * .65, fh = h * .5;
    ctx.save();
    forma(ctx, x, y, w, h);
    ctx.clip();
    if (foto && foto.isRecorte) {
        // pessoa sem fundo: "em pé" na base da área da foto, um pouco maior que ela
        const esc = Math.min((fw * 1.12) / foto.width, (fh * 1.04) / foto.height);
        const dw = foto.width * esc, dh = foto.height * esc;
        const fxp = fx + fw / 2 - dw / 2 + w * .02, fyp = fy + fh - dh;
        ctx.save();
        ctx.beginPath();
        ctx.rect(x, y, w, fy + fh - y);   // não passa da faixa do nome
        ctx.clip();

        // sombra suave atrás do atleta
        const sil = silhueta(foto, corDoTime(atleta.time));   // contorno na cor do time (vermelho/amarelo)
        ctx.save();
        ctx.shadowColor = "rgba(0,0,0,.45)";
        ctx.shadowBlur = 26;
        ctx.drawImage(sil, fxp, fyp, dw, dh);
        ctx.restore();

        // borda em volta do atleta (contorno de "adesivo"): a silhueta branca repetida em círculo
        const borda = Math.max(4, w * .014);
        const passos = 36;
        for (let k = 0; k < passos; k++) {
            const ang = (k / passos) * Math.PI * 2;
            ctx.drawImage(sil, fxp + Math.cos(ang) * borda, fyp + Math.sin(ang) * borda, dw, dh);
        }
        ctx.drawImage(foto, fxp, fyp, dw, dh);
        ctx.restore();
    } else if (foto) {
        ctx.beginPath();
        ctx.rect(fx, fy, fw, fh);
        ctx.clip();
        const esc = Math.max(fw / foto.width, fh / foto.height);
        const dw = foto.width * esc, dh = foto.height * esc;
        ctx.drawImage(foto, fx + (fw - dw) / 2, fy + (fh - dh) * .25, dw, dh);   // prioriza o topo (rosto)
    } else {
        // sem foto: silhueta da posição do atleta (a mesma da lista)
        ctx.beginPath();
        ctx.rect(x, y, w, fy + fh - y);   // não passa da faixa do nome
        ctx.clip();
        const tam = Math.min(fw * 1.15, fh * 1.05);
        desenharSilhueta(ctx, atleta.silhueta, fx + fw / 2 - tam / 2, fy + fh - tam, tam, "rgba(0,0,0,.34)");
    }
    ctx.restore();

    // esmaece a base da foto na faixa do nome
    ctx.save();
    forma(ctx, x, y, w, h);
    ctx.clip();
    const fade = ctx.createLinearGradient(0, fy + fh * .75, 0, fy + fh);
    fade.addColorStop(0, "rgba(0,0,0,0)");
    fade.addColorStop(1, "rgba(0,0,0,.28)");
    ctx.fillStyle = fade;
    ctx.fillRect(x, fy + fh * .75, w, fh * .25);
    ctx.restore();

    // posição e pontos (coluna da esquerda)
    ctx.textAlign = "left";
    ctx.textBaseline = "alphabetic";
    ctx.fillStyle = t.texto;
    const colW = w * .26;
    ajustar(ctx, atleta.pos || "", colW, h * .052, 800);
    ctx.fillText(atleta.pos || "", x + w * .07, y + h * .24);
    ajustar(ctx, atleta.pontos, colW, h * .075, 900);
    ctx.fillText(atleta.pontos, x + w * .07, y + h * .32);

    // time do atleta, logo abaixo da posição
    const tamEsc = w * .22;
    blocoTime(ctx, escudoTime, atleta.time, x + w * .07 + tamEsc / 2, y + h * .345 + tamEsc / 2, tamEsc);

    // faixa do nome
    const ny = y + h * .645, nh = h * .105;
    ctx.save();
    forma(ctx, x, y, w, h);
    ctx.clip();
    const faixa = ctx.createLinearGradient(0, ny, 0, ny + nh);
    faixa.addColorStop(0, "rgba(255,255,255,.35)");
    faixa.addColorStop(1, "rgba(0,0,0,.12)");
    ctx.fillStyle = faixa;
    ctx.fillRect(x, ny, w, nh);
    ctx.fillStyle = "rgba(255,255,255,.55)";
    ctx.fillRect(x, ny, w, 2);
    ctx.restore();

    ctx.textAlign = "center";
    ctx.fillStyle = t.texto;
    ajustar(ctx, (atleta.nome || "").toUpperCase(), w * .78, h * .05, 800);
    ctx.fillText((atleta.nome || "").toUpperCase(), x + w / 2, ny + nh * .66);

    // caixa de pontos
    const bx = x + w * .2, by = y + h * .765, bw = w * .6, bh = h * .145;
    retArredondado(ctx, bx, by, bw, bh, bh * .28);
    ctx.fillStyle = "#0f1013";
    ctx.fill();
    ctx.fillStyle = "#e9e9e9";
    ajustar(ctx, "PONTOS", bw * .5, bh * .2, 700);
    ctx.fillText("PONTOS", x + w / 2, by + bh * .3);
    ctx.fillStyle = t.destaque;
    ajustar(ctx, atleta.pontos, bw * .8, bh * .5, 900);
    ctx.fillText(atleta.pontos, x + w / 2, by + bh * .86);

    // posição no pódio
    const base = y + h;
    const cy = base + (posicao === 1 ? 78 : 76);
    louro(ctx, x + w / 2 - 40, cy - 6, 36, t.destaque, -1);
    louro(ctx, x + w / 2 + 40, cy - 6, 36, t.destaque, 1);
    ctx.fillStyle = t.destaque;
    ctx.font = `900 ${posicao === 1 ? 72 : 64}px ${FONTE}`;
    ctx.fillText(String(posicao), x + w / 2, cy + 22);
    const lg = ctx.createLinearGradient(x + w * .2, 0, x + w * .8, 0);
    lg.addColorStop(0, "rgba(0,0,0,0)");
    lg.addColorStop(.5, t.destaque);
    lg.addColorStop(1, "rgba(0,0,0,0)");
    ctx.fillStyle = lg;
    ctx.fillRect(x + w * .1, cy + 46, w * .8, 4);
}

async function entregar(canvas, nome, titulo = "Top 3 da rodada") {
    const blob = await new Promise(resolve => canvas.toBlob(resolve, "image/png"));
    if (!blob) throw new Error("não foi possível criar a imagem");

    const arquivo = new File([blob], nome, { type: "image/png" });
    const celular = /Android|iPhone|iPad|iPod/i.test(navigator.userAgent);
    if (celular && navigator.canShare && navigator.canShare({ files: [arquivo] })) {
        try {
            await navigator.share({ files: [arquivo], title: titulo });
            return;
        } catch (e) {
            if (e && e.name === "AbortError") return;   // a pessoa fechou o menu de compartilhar
        }
    }

    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = nome;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
}

// ----- helpers de desenho -----

// Cor do time pelo nome (Vermelho / Amarelo); outro nome cai no branco.
const COR_VERMELHO = "#e5242b", COR_AMARELO = "#ffd21f";
function corDoTime(nome) {
    const n = (nome || "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase();
    if (n.includes("vermelh")) return COR_VERMELHO;
    if (n.includes("amarel")) return COR_AMARELO;
    return "#ffffff";
}

// Escudo do time centrado em (cx, cy); se a imagem não carregar, escreve o nome do time no lugar.
function blocoTime(ctx, img, nome, cx, cy, tam) {
    if (img) { escudo(ctx, img, cx, cy, tam); return; }
    if (!nome) return;
    ctx.save();
    ctx.textAlign = "center";
    ctx.textBaseline = "alphabetic";
    ctx.fillStyle = corDoTime(nome);
    const txt = nome.toUpperCase();
    ajustar(ctx, txt, tam * 1.1, tam * .22, 800);
    ctx.fillText(txt, cx, cy + tam * .08);
    ctx.restore();
}

// Assinatura no canto inferior esquerdo das imagens geradas.
const TEXTO_ASSINATURA = "Rivvals App - AFC";
function assinatura(ctx, x, y, tam, comContorno) {
    ctx.save();
    ctx.textAlign = "left";
    ctx.textBaseline = "alphabetic";
    ctx.font = `700 ${tam}px "Segoe UI", Arial, sans-serif`;
    if (comContorno) {
        // o card tem fundo transparente: o contorno escuro garante leitura em fundo claro ou escuro
        ctx.lineJoin = "round";
        ctx.lineWidth = Math.max(2, tam * .22);
        ctx.strokeStyle = "rgba(0,0,0,.7)";
        ctx.strokeText(TEXTO_ASSINATURA, x, y);
        ctx.fillStyle = "rgba(255,255,255,.95)";
    } else {
        ctx.fillStyle = "rgba(255,255,255,.6)";
    }
    ctx.fillText(TEXTO_ASSINATURA, x, y);
    ctx.restore();
}

// canvas auxiliar (retorna o contexto 2D)
function camada(w, h) {
    const cv = document.createElement("canvas");
    cv.width = w;
    cv.height = h;
    const cx = cv.getContext("2d");
    cx.imageSmoothingEnabled = true;
    cx.imageSmoothingQuality = "high";
    return cx;
}

function retArredondado(ctx, x, y, w, h, r) {
    r = Math.min(r, w / 2, h / 2);
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
}

// Silhueta da posição (caminho SVG em viewBox 100x100, fill-rule evenodd) dentro do quadrado x, y, tam.
function desenharSilhueta(ctx, caminho, x, y, tam, cor) {
    ctx.save();
    ctx.fillStyle = cor;
    if (caminho) {
        ctx.translate(x, y);
        ctx.scale(tam / 100, tam / 100);
        ctx.fill(new Path2D(caminho), "evenodd");
    } else {
        // sem caminho: busto genérico
        ctx.beginPath();
        ctx.arc(x + tam / 2, y + tam * .32, tam * .16, 0, Math.PI * 2);
        ctx.fill();
        ctx.beginPath();
        ctx.ellipse(x + tam / 2, y + tam, tam * .42, tam * .42, 0, 0, Math.PI * 2);
        ctx.fill();
    }
    ctx.restore();
}

function slugNome(nome) {
    return (nome || "jogador").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "") || "jogador";
}

// ----- Card de um jogador só (moldura genérica) -----
// dados = { nome, pos, pontos, data, foto, silhueta, escudo }
const CARD_W = 499, CARD_H = 802, CARD_K = 2;   // tamanho da moldura x resolução (saída 998x1604)
const OURO = "#ffc928";

export async function gerarCardJogador(dados) {
    const canvas = document.createElement("canvas");
    canvas.width = CARD_W * CARD_K;
    canvas.height = CARD_H * CARD_K;
    const [moldura, escudoImg, foto] = await Promise.all([
        carregar("img/moldura-card.png"),
        dados.escudo ? carregar(dados.escudo) : null,
        prepararFoto(dados.foto)   // detecta se já está sem fundo antes de tentar remover
    ]);
    const ctx = canvas.getContext("2d");
    ctx.scale(CARD_K, CARD_K);
    desenharCardJogador(ctx, dados, { moldura, escudo: escudoImg, foto });
    await entregar(canvas, `card-${slugNome(dados.nome)}.png`, dados.nome);
}

function molduraPadrao(ctx, W, H) {
    forma(ctx, 8, 8, W - 16, H - 16);
    const g = ctx.createLinearGradient(0, 0, W, H);
    g.addColorStop(0, "#2a2540");
    g.addColorStop(1, "#0c0c12");
    ctx.fillStyle = g;
    ctx.fill();
    ctx.lineJoin = "round";
    ctx.lineWidth = 6;
    ctx.strokeStyle = "#9aa0a8";
    ctx.stroke();
}

function desenharCardJogador(ctx, d, im) {
    const W = CARD_W, H = CARD_H;
    if (im.moldura) ctx.drawImage(im.moldura, 0, 0, W, H);
    else molduraPadrao(ctx, W, H);

    // área da foto (o atleta "em pé" na base dela, em cima da linha do horizonte da moldura)
    const fx = 138, fy = 120, fw = 348, fh = 354;   // começa abaixo dos ombros da moldura, ao lado da coluna posição/escudo
    ctx.save();
    ctx.beginPath();
    ctx.rect(14, 60, W - 28, fy + fh - 60);
    ctx.clip();
    const foto = im.foto;
    if (foto && foto.isRecorte) {
        // o atleta cabe inteiro na área da foto (sem passar da coluna da esquerda nem da borda do card)
        const esc = Math.min((fw * .96) / foto.width, (fh * .97) / foto.height);
        const dw = foto.width * esc, dh = foto.height * esc;
        const px = fx + fw / 2 - dw / 2, py = fy + fh - dh;
        const sil = silhueta(foto, corDoTime(d.time));   // contorno na cor do time (vermelho/amarelo)

        // desenha numa camada à parte para dissolver a base do corte na linha do horizonte
        const tmp = camada(W * CARD_K, H * CARD_K);
        tmp.scale(CARD_K, CARD_K);
        tmp.save();
        tmp.shadowColor = "rgba(0,0,0,.55)";
        tmp.shadowBlur = 24 * CARD_K;   // o blur da sombra não acompanha o scale
        tmp.drawImage(sil, px, py, dw, dh);
        tmp.restore();
        const borda = 3;   // contorno discreto em volta do atleta
        for (let k = 0; k < 36; k++) {
            const ang = (k / 36) * Math.PI * 2;
            tmp.drawImage(sil, px + Math.cos(ang) * borda, py + Math.sin(ang) * borda, dw, dh);
        }
        tmp.drawImage(foto, px, py, dw, dh);
        tmp.globalCompositeOperation = "destination-out";
        const fd = tmp.createLinearGradient(0, fy + fh - 70, 0, fy + fh);
        fd.addColorStop(0, "rgba(0,0,0,0)");
        fd.addColorStop(1, "rgba(0,0,0,1)");
        tmp.fillStyle = fd;
        tmp.fillRect(0, fy + fh - 70, W, 70);
        ctx.drawImage(tmp.canvas, 0, 0, W, H);
    } else if (foto) {
        // não deu para tirar o fundo: foto inteira num quadro de cantos arredondados
        retArredondado(ctx, fx, fy, fw, fh, 26);
        ctx.clip();
        const esc = Math.max(fw / foto.width, fh / foto.height);
        const dw = foto.width * esc, dh = foto.height * esc;
        ctx.drawImage(foto, fx + (fw - dw) / 2, fy + (fh - dh) * .25, dw, dh);
    } else {
        const tam = Math.min(fw, fh) * 1.05;
        desenharSilhueta(ctx, d.silhueta, fx + fw / 2 - tam / 2, fy + fh - tam, tam, "rgba(255,255,255,.16)");
    }
    ctx.restore();

    // coluna da esquerda: posição e escudo do time
    ctx.textAlign = "center";
    ctx.textBaseline = "alphabetic";
    ctx.fillStyle = OURO;
    ajustar(ctx, d.pos || "", 100, 50, 900);
    ctx.fillText(d.pos || "", 76, 172);
    ctx.fillRect(52, 186, 48, 4);
    blocoTime(ctx, im.escudo, d.time, 76, 268, 104);   // time, logo abaixo da posição

    // faixa do nome
    const ny = 488, nh = 62;
    for (const yy of [ny, ny + nh]) {
        const lg = ctx.createLinearGradient(40, 0, W - 40, 0);
        lg.addColorStop(0, "rgba(255,201,40,0)");
        lg.addColorStop(.5, OURO);
        lg.addColorStop(1, "rgba(255,201,40,0)");
        ctx.fillStyle = lg;
        ctx.fillRect(40, yy, W - 80, 2);
    }
    const nome = (d.nome || "").toUpperCase();
    ctx.save();
    ctx.fillStyle = "#ffffff";
    ctx.shadowColor = "rgba(0,0,0,.6)";
    ctx.shadowBlur = 8;
    ajustar(ctx, nome, 390, 40, 900);
    ctx.fillText(nome, W / 2, ny + nh * .68);
    ctx.restore();

    // caixa de pontos: retangular, cantos arredondados
    const bw = 260, bh = 104, bx = (W - bw) / 2, by = 574;
    retArredondado(ctx, bx, by, bw, bh, 26);
    ctx.fillStyle = "#0f1013";
    ctx.fill();
    ctx.lineWidth = 2;
    ctx.strokeStyle = "rgba(255,201,40,.55)";
    ctx.stroke();
    ctx.fillStyle = "#e9e9e9";
    ajustar(ctx, "PONTOS", bw * .5, 16, 700);
    ctx.fillText("PONTOS", W / 2, by + 31);
    ctx.fillStyle = OURO;
    ajustar(ctx, d.pontos || "", bw * .8, 56, 900);
    ctx.fillText(d.pontos || "", W / 2, by + 90);

    // rodapé
    ctx.fillStyle = "rgba(255,255,255,.6)";
    ajustar(ctx, ("ÚLTIMA PARTIDA" + (d.data ? "  ·  " + d.data : "")), 280, 15, 700);
    ctx.fillText("ÚLTIMA PARTIDA" + (d.data ? "  ·  " + d.data : ""), W / 2, 726);

    assinatura(ctx, 14, H - 12, 15, true);   // canto inferior esquerdo (fora da moldura, com contorno para ler em qualquer fundo)
}
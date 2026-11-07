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
    // prévia antes de baixar: dá para editar a foto de cada atleta (as fotos editadas entram em imagens.fotos)
    await revisarCard({
        canvas,
        redesenhar: () => desenhar(canvas.getContext("2d"), dados, imagens),
        fotos: imagens.fotos,
        nomes: dados.atletas.map(a => a.nome),
        arquivo: dados.arquivo || "top3-rodada.png",
        titulo: "Top 3 da rodada"
    });
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

// Foto que já está sem fundo: limpa pedaços soltos e recorta o canvas na caixa do atleta (mesmo formato que recortarFundo devolve).
export function recortarPorAlfa(img) {
    try {
        const { cv } = lerPixels(img, 768);
        const orig = clonar(cv);
        limparIlhas(cv.getContext("2d", { willReadFrequently: true }), cv.width, cv.height);
        return cortarNoAlfa(cv, orig);
    } catch {
        return null;
    }
}

function clonar(cv) {
    const c = document.createElement("canvas");
    c.width = cv.width;
    c.height = cv.height;
    c.getContext("2d", { willReadFrequently: true }).drawImage(cv, 0, 0);   // o editor lê os pixels várias vezes
    return c;
}

// Recorta o canvas na caixa do que é opaco. O resultado guarda o original (_orig) e o recorte sem cortar (_cheio),
// que o editor de foto usa para apagar/restaurar. Null se não sobrou nada.
function cortarNoAlfa(cv, orig) {
    const w = cv.width, h = cv.height;
    const px = cv.getContext("2d", { willReadFrequently: true }).getImageData(0, 0, w, h).data;
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
    out._orig = orig;
    out._cheio = cv;
    return out;
}

// Limpeza automática do recorte: fica só a figura principal (e pedaços grandes, ≥ 25% dela, ex.: braço separado).
// Apaga as "ilhas" que o removedor de fundo deixa (manchas no canto, pedaços do fundo) e o semitransparente solto em volta delas.
export function limparIlhas(cx, w, h) {
    const img = cx.getImageData(0, 0, w, h), d = img.data, n = w * h;
    const SOLIDO = 128;
    const rot = new Int32Array(n);      // componente de cada pixel sólido (0 = nenhum)
    const pilha = new Int32Array(n);
    const tam = [0];
    let id = 0;
    for (let p = 0; p < n; p++) {
        if (rot[p] || d[p * 4 + 3] <= SOLIDO) continue;
        id++;
        let topo = 0, cont = 0;
        pilha[topo++] = p;
        rot[p] = id;
        while (topo) {
            const q = pilha[--topo];
            cont++;
            const x = q % w, y = (q - x) / w;
            for (let dy = -1; dy <= 1; dy++) {
                const yy = y + dy;
                if (yy < 0 || yy >= h) continue;
                for (let dx = -1; dx <= 1; dx++) {
                    const xx = x + dx;
                    if (xx < 0 || xx >= w) continue;
                    const r = yy * w + xx;
                    if (!rot[r] && d[r * 4 + 3] > SOLIDO) { rot[r] = id; pilha[topo++] = r; }
                }
            }
        }
        tam.push(cont);
    }
    if (id <= 1) return;

    let maior = 0;
    for (let k = 1; k <= id; k++) if (tam[k] > maior) maior = tam[k];
    const manter = tam.map(t => t >= maior * .25);

    // sólidos de ilhas pequenas somem
    for (let p = 0; p < n; p++) if (rot[p] && !manter[rot[p]]) { d[p * 4 + 3] = 0; rot[p] = 0; }
    // semitransparente só fica se encostar (até 2 px) num sólido que ficou
    for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
            const p = y * w + x;
            const a = d[p * 4 + 3];
            if (a === 0 || a > SOLIDO) continue;
            let perto = false;
            for (let dy = -2; dy <= 2 && !perto; dy++) {
                const yy = y + dy;
                if (yy < 0 || yy >= h) continue;
                for (let dx = -2; dx <= 2; dx++) {
                    const xx = x + dx;
                    if (xx < 0 || xx >= w) continue;
                    if (rot[yy * w + xx]) { perto = true; break; }
                }
            }
            if (!perto) d[p * 4 + 3] = 0;
        }
    }
    cx.putImageData(img, 0, 0);
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

    const orig = clonar(cv);   // foto sem máscara: o editor usa para "restaurar"
    for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
            let m = valor(x, y);
            if (invertida) m = 1 - m;
            const a = Math.min(1, Math.max(0, (m - .35) / .3));   // borda suave entre 0,35 e 0,65
            const i = (y * w + x) * 4 + 3;
            px.data[i] = Math.round(px.data[i] * a);
        }
    }
    cx.putImageData(px, 0, 0);
    limparIlhas(cx, w, h);    // tira os pedaços soltos que o removedor de fundo deixa
    return cortarNoAlfa(cv, orig);   // null = nenhuma pessoa encontrada
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
    ctx.fillText(((dados.subtitulo || "MELHORES DA RODADA") + (dados.data ? "  ·  " + dados.data : "")).toUpperCase(), L / 2, 212);

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

// Formato da moldura das cartas do Top 3 (vetorizado de img/moldura: topo arqueado, ombros recortados para dentro,
// laterais retas e base em ponta com cantos arredondados). Coordenadas em fração da caixa (x, y, w, h), espelhadas no meio.
function formaMoldura(ctx, x, y, w, h) {
    const P = (u, v) => [x + u * w, y + v * h];          // lado esquerdo
    const E = (u, v) => [x + (1 - u) * w, y + v * h];    // espelho (lado direito)
    ctx.beginPath();
    ctx.moveTo(...P(.5, 0));
    // direita: topo arqueado, ombro côncavo, lateral, canto de baixo e curva até a ponta
    ctx.quadraticCurveTo(...E(.3, 0), ...E(.123, .047));
    ctx.quadraticCurveTo(...E(.118, .115), ...E(0, .115));
    ctx.lineTo(...E(0, .823));
    ctx.quadraticCurveTo(...E(0, .875), ...E(.083, .886));
    ctx.quadraticCurveTo(...E(.35, .912), ...P(.5, 1));
    // esquerda (o mesmo, de volta)
    ctx.quadraticCurveTo(...P(.35, .912), ...P(.083, .886));
    ctx.quadraticCurveTo(...P(0, .875), ...P(0, .823));
    ctx.lineTo(...P(0, .115));
    ctx.quadraticCurveTo(...P(.118, .115), ...P(.123, .047));
    ctx.quadraticCurveTo(...P(.3, 0), ...P(.5, 0));
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
    // com lista de ações (Top 3 pontuação / notas): foto e nome sobem para abrir espaço para a lista
    const lista = Array.isArray(atleta.acoes);

    // brilho e corpo
    ctx.save();
    ctx.shadowColor = t.brilho;
    ctx.shadowBlur = posicao === 1 ? 60 : 34;
    formaMoldura(ctx, x, y, w, h);
    const g = ctx.createLinearGradient(x, y, x + w, y + h);
    g.addColorStop(0, t.a);
    g.addColorStop(.5, t.b);
    g.addColorStop(1, t.c);
    ctx.fillStyle = g;
    ctx.fill();
    ctx.restore();

    // listras diagonais só acima da faixa do nome; do nome para baixo a carta fica com cor sólida
    const ny = y + h * (lista ? .53 : .645), nh = h * (lista ? .08 : .105);
    ctx.save();
    formaMoldura(ctx, x, y, w, h);
    ctx.clip();
    ctx.fillStyle = t.b;
    ctx.fillRect(x, ny, w, y + h - ny);
    ctx.beginPath();
    ctx.rect(x, y, w, ny - y);
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
    formaMoldura(ctx, x, y, w, h);
    ctx.lineJoin = "round";
    ctx.lineWidth = 7;
    ctx.strokeStyle = t.borda;
    ctx.stroke();
    // filete interno (a moldura original tem borda dupla)
    const ins = w * .028;
    formaMoldura(ctx, x + ins, y + ins * 1.1, w - ins * 2, h - ins * 2.4);
    ctx.lineWidth = 2;
    ctx.strokeStyle = t.c;
    ctx.globalAlpha = .55;
    ctx.stroke();
    ctx.globalAlpha = 1;

    // foto (ou silhueta)
    const fx = x + w * .31, fy = y + h * .13, fw = w * .65, fh = h * (lista ? .395 : .5);
    ctx.save();
    formaMoldura(ctx, x, y, w, h);
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
    formaMoldura(ctx, x, y, w, h);
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
    if (lista) {
        // como nos cards de um jogador: o número (pontuação ou nota) em cima e a posição embaixo
        const num = atleta.topo || "";
        ctx.save();
        ajustar(ctx, num, colW, h * .08, 900);
        if (atleta.topoCor) {
            // nota na cor da faixa, com contorno escuro para ler no fundo dourado/prateado/bronze
            ctx.lineJoin = "round";
            ctx.lineWidth = Math.max(3, h * .008);
            ctx.strokeStyle = "rgba(0,0,0,.75)";
            ctx.strokeText(num, x + w * .07, y + h * .245);
            ctx.fillStyle = COR_NOTA[atleta.topoCor] || t.texto;
        } else if (atleta.topoNeg) {
            ctx.fillStyle = "#b91c1c";
        }
        ctx.fillText(num, x + w * .07, y + h * .245);
        ctx.restore();
        ctx.fillStyle = t.texto;
        ajustar(ctx, atleta.pos || "", colW, h * .052, 800);
        ctx.fillText(atleta.pos || "", x + w * .07, y + h * .32);
    } else {
        ajustar(ctx, atleta.pos || "", colW, h * .052, 800);
        ctx.fillText(atleta.pos || "", x + w * .07, y + h * .24);
        ajustar(ctx, atleta.pontos, colW, h * .075, 900);
        ctx.fillText(atleta.pontos, x + w * .07, y + h * .32);
    }

    // time do atleta, logo abaixo da posição
    const tamEsc = w * .22;
    blocoTime(ctx, escudoTime, atleta.time, x + w * .07 + tamEsc / 2, y + h * .345 + tamEsc / 2, tamEsc);

    // faixa do nome
    ctx.save();
    formaMoldura(ctx, x, y, w, h);
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

    if (lista) {
        // lista de ações (mesma dos cards de um jogador) direto na cor sólida da carta, com cores escuras
        // termina antes dos cantos de baixo da moldura (a base afunila em ponta a partir de ~82% da altura)
        const bx = x + w * .07, by = y + h * .62, bw = w * .86, bh = h * .225;
        const pad = w * .04;
        caixaAcoes(ctx, atleta, L, {
            meio: x + w / 2,
            gap: w * .025,
            colW: bw / 2 - pad - w * .025,
            topo: by + bh * .05,
            base: by + bh * .95,
            maxRow: h * .05,
            maxNum: h * .04,
            minNum: 8,
            minPts: 9,
            ptsRatio: .66,
            cores: {
                num: t.texto, rot: t.texto, numVazio: "rgba(0,0,0,.22)", rotVazio: "rgba(0,0,0,.28)",
                pos: "#0b6b2c", neg: "#a3121b", zero: "rgba(0,0,0,.55)", div: "rgba(0,0,0,.3)"
            }
        });
    } else {
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
    }

    // posição no pódio
    const base = y + h;
    const cy = base + (posicao === 1 ? 78 : 76);
    ctx.textAlign = "center";   // a lista de ações deixa o alinhamento à esquerda
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
// dados = { nome, pos, data, pontosTopo, pontosNeg, topoCor, foto, silhueta, escudo, time, nota, notaClasse, acoes, total, totalRotulo, totalNeg, totalCor, arquivo }
// acoes = [{ rotulo, qtd, pontos, neg }]: no card de pontuação "pontos" são os pontos da ação; no card com nota, quanto ela somou na nota.
// pontosTopo (card de pontuação) aparece só como número no topo da coluna da esquerda; senão a nota (opcional) aparece no topo da coluna da esquerda; totalRotulo = "TOTAL" ou "NOTA"; totalCor = classe da nota (cor do valor).
const CARD_W = 499, CARD_H = 802, CARD_K = 2;   // tamanho da moldura x resolução (saída 998x1604)
const OURO = "#ffc928";

export async function gerarCardJogador(dados) {
    const canvas = document.createElement("canvas");
    canvas.width = CARD_W * CARD_K;
    canvas.height = CARD_H * CARD_K;
    const [moldura, escudoImg, foto] = await Promise.all([
        null,   // a moldura agora é desenhada (molduraPadrao); img/moldura-card.png não é mais usada
        dados.escudo ? carregar(dados.escudo) : null,
        prepararFoto(dados.foto)   // detecta se já está sem fundo antes de tentar remover
    ]);
    const fotos = [foto];
    await revisarCard({
        canvas,
        redesenhar: () => {
            const ctx = canvas.getContext("2d");
            ctx.setTransform(1, 0, 0, 1, 0, 0);
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            ctx.scale(CARD_K, CARD_K);
            desenharCardJogador(ctx, dados, { moldura, escudo: escudoImg, foto: fotos[0] });
        },
        fotos,
        nomes: [dados.nome],
        arquivo: `${dados.arquivo || "card"}-${slugNome(dados.nome)}.png`,
        titulo: dados.nome
    });
}

// ----- Moldura do card individual (vetorizada da arte: coroa no topo, ombros arredondados,
// borda tripla dourado/preto/dourado, base em ponta e dois raios dourados em cada lado) -----
// Caixa da moldura dentro do card de 499x802 (sobra espaço nas laterais para os raios saírem da borda).
const MJ = { x: 26, y: 12, w: 447, h: 776 };

// Contorno, em fração da caixa, espelhado no meio. Medido da arte (1024x1536).
function formaJogador(ctx, x, y, w, h) {
    const P = (u, v) => [x + u * w, y + v * h];
    const E = (u, v) => [x + (1 - u) * w, y + v * h];
    ctx.beginPath();
    ctx.moveTo(...P(.5, 0));                                   // ponta da coroa
    // lado direito: coroa, ombro, lateral, canto de baixo e curva até a ponta
    ctx.lineTo(...E(.452, .027));
    ctx.quadraticCurveTo(...E(.415, .05), ...E(.378, .016));
    ctx.quadraticCurveTo(...E(.365, .008), ...E(.34, .012));
    ctx.quadraticCurveTo(...E(.2, .045), ...E(0, .106));
    ctx.lineTo(...E(0, .833));
    ctx.quadraticCurveTo(...E(.005, .89), ...E(.075, .899));
    ctx.quadraticCurveTo(...E(.35, .93), ...P(.5, 1));
    // lado esquerdo (o mesmo, de volta)
    ctx.quadraticCurveTo(...P(.35, .93), ...P(.075, .899));
    ctx.quadraticCurveTo(...P(.005, .89), ...P(0, .833));
    ctx.lineTo(...P(0, .106));
    ctx.quadraticCurveTo(...P(.2, .045), ...P(.34, .012));
    ctx.quadraticCurveTo(...P(.365, .008), ...P(.378, .016));
    ctx.quadraticCurveTo(...P(.415, .05), ...P(.452, .027));
    ctx.closePath();
}

// mesma forma, encolhida d px para dentro (para as faixas da borda e o miolo)
function formaJogadorDentro(ctx, d) {
    formaJogador(ctx, MJ.x + d, MJ.y + d * 1.25, MJ.w - d * 2, MJ.h - d * 2.6);
}

function douradoMetal(ctx) {
    const g = ctx.createLinearGradient(MJ.x, MJ.y, MJ.x + MJ.w, MJ.y + MJ.h);
    g.addColorStop(0, "#fff1a6");
    g.addColorStop(.3, "#e2ad1a");
    g.addColorStop(.55, "#fbe07a");
    g.addColorStop(.8, "#b8800a");
    g.addColorStop(1, "#f2cf5a");
    return g;
}

// borda tripla (dourado / preto / dourado) e o miolo escuro do card.
// yNome = topo da faixa do nome: acima dela o miolo é listrado, abaixo é cor sólida.
const MIOLO_SOLIDO = "#15131c";
function molduraPadrao(ctx, yNome = 488) {
    const ouro = douradoMetal(ctx);
    formaJogador(ctx, MJ.x, MJ.y, MJ.w, MJ.h);
    ctx.fillStyle = ouro;
    ctx.fill();
    ctx.lineWidth = 1.5;
    ctx.strokeStyle = "#3a2a05";
    ctx.stroke();
    formaJogadorDentro(ctx, 4);
    ctx.fillStyle = "#0a0a0a";
    ctx.fill();
    formaJogadorDentro(ctx, 12);
    ctx.fillStyle = ouro;
    ctx.fill();
    // miolo: degradê escuro com listras diagonais em cima, cor sólida do nome para baixo
    ctx.save();
    formaJogadorDentro(ctx, 16);
    ctx.clip();
    const g = ctx.createLinearGradient(0, 0, CARD_W, yNome);
    g.addColorStop(0, "#2a2540");
    g.addColorStop(1, "#14121c");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, CARD_W, yNome);
    ctx.fillStyle = MIOLO_SOLIDO;
    ctx.fillRect(0, yNome, CARD_W, CARD_H - yNome);
    // listras (mesmo desenho das cartas do Top 3), só acima do nome
    ctx.beginPath();
    ctx.rect(0, 0, CARD_W, yNome);
    ctx.clip();
    ctx.strokeStyle = "rgba(255,255,255,.05)";
    ctx.lineWidth = 16;
    for (let k = -CARD_H; k < CARD_W + CARD_H; k += 44) {
        ctx.beginPath();
        ctx.moveTo(k, CARD_H);
        ctx.lineTo(k + CARD_H, 0);
        ctx.stroke();
    }
    ctx.restore();
}

// raios dourados das laterais (dois de cada lado); desenhados por cima da foto, como na arte
function raiosJogador(ctx) {
    const ouro = douradoMetal(ctx);
    const P = (u, v, lado) => [MJ.x + (lado < 0 ? u : 1 - u) * MJ.w, MJ.y + v * MJ.h];
    for (const lado of [-1, 1]) {
        for (const dv of [0, .126]) {
            // A = topo, B = ponta, C = dobra interna, D = ponta de fora
            const A = P(.002, .173 + dv, lado), B = P(.182, .378 + dv, lado), C = P(.031, .291 + dv, lado), D = P(-.055, .241 + dv, lado);
            ctx.save();
            ctx.shadowColor = "rgba(0,0,0,.5)";
            ctx.shadowBlur = 6;
            ctx.beginPath();
            ctx.moveTo(...A); ctx.lineTo(...B); ctx.lineTo(...C); ctx.lineTo(...D); ctx.closePath();
            ctx.fillStyle = ouro;
            ctx.fill();
            ctx.restore();
            // face mais escura (dá o efeito de dobra da arte)
            ctx.beginPath();
            ctx.moveTo(...A); ctx.lineTo(...C); ctx.lineTo(...D); ctx.closePath();
            ctx.fillStyle = "rgba(120,80,0,.35)";
            ctx.fill();
            ctx.beginPath();
            ctx.moveTo(...A); ctx.lineTo(...B); ctx.lineTo(...C); ctx.lineTo(...D); ctx.closePath();
            ctx.lineWidth = 1;
            ctx.strokeStyle = "#3a2a05";
            ctx.stroke();
        }
    }
}

// Cor da nota por faixa (mesmas faixas do ranking e da súmula: NotaJogador.Classe)
const COR_NOTA = {
    "nota-azul": "#3b82f6",
    "nota-verde": "#22c55e",
    "nota-amarela": "#a3e635",
    "nota-laranja": "#fb923c",
    "nota-vermelha": "#ef4444"
};

// Sigla curta da ação (estilo carta de FIFA: "78 PAC"). Desconhecida: primeiras letras da descrição.
const SIGLAS_ACAO = [
    ["saldo", "SALDO"], ["pontos iniciais", "BASE"], ["base", "BASE"], ["outros ajustes", "AJUS"], ["ajuste", "AJUS"],
    ["gol do time", "G.TIME"], ["gols do time", "G.TIME"], ["gol sofrido", "G.SOF"], ["gols sofrido", "G.SOF"],
    ["gol contra", "G.CON"], ["gols contra", "G.CON"], ["g. contra", "G.CON"],
    ["pen. com", "P.COM"], ["pen. perd", "P.PER"],
    ["sem sofrer", "SG"], ["chute", "CHU"], ["assist", "ASS"], ["desarme", "DES"], ["defesa", "DEF"],
    ["passe", "PAS"], ["falta", "FAL"], ["penalti comet", "P.COM"], ["penalti perd", "P.PER"], ["penalti defend", "P.DEF"],
    ["amarelo", "AMA"], ["azu", "AZU"], ["vermelho", "VER"], ["gol", "GOL"]
];
function siglaAcao(rotulo) {
    const n = String(rotulo || "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().trim();
    for (const [chave, sigla] of SIGLAS_ACAO) if (n.includes(chave)) return sigla;
    return n.replace(/[^a-z]/g, "").slice(0, 4).toUpperCase() || "?";
}

// Lista de ações do card, no estilo carta de FIFA: duas colunas com divisória no meio,
// "quantidade SIGLA" grande e os pontos da ação pequenos ao lado; o total embaixo.
// d.acoes = [{ rotulo, qtd, pontos ("+8,00" ou null), neg }], d.total = "12,50", d.totalNeg
// cfg (opcional, usado pelas cartas do Top 3): { meio, colW, gap, topo, base, maxRow, maxNum, minNum, minPts, ptsRatio, cores }
// cores (opcional, para fundo claro): { num, rot, numVazio, rotVazio, pos, neg, zero, div }
const CORES_LISTA = {
    num: OURO, rot: "#ffffff", numVazio: "rgba(255,201,40,.25)", rotVazio: "rgba(255,255,255,.28)",
    pos: "#22c55e", neg: "#ef4444", zero: "rgba(255,255,255,.6)", div: "rgba(255,201,40,.55)"
};
function caixaAcoes(ctx, d, W, cfg = {}) {
    const cor = { ...CORES_LISTA, ...(cfg.cores || {}) };
    const acoes = d.acoes || [];
    const topo = cfg.topo ?? 556, base = cfg.base ?? (d.total ? 676 : 690);   // faixa das ações (o total fica abaixo)
    ctx.textBaseline = "alphabetic";

    if (acoes.length === 0) {
        ctx.textAlign = "center";
        ctx.fillStyle = "rgba(255,255,255,.55)";
        ajustar(ctx, "SEM LANCES REGISTRADOS", (cfg.colW ?? 170) * 2, 16, 700);
        ctx.fillText("SEM LANCES REGISTRADOS", cfg.meio ?? W / 2, (topo + base) / 2 + 6);
    } else {
        const porColuna = Math.ceil(acoes.length / 2);   // enche a coluna da esquerda primeiro, como na carta
        const rowH = Math.min(cfg.maxRow ?? 40, (base - topo) / porColuna);
        const tamNum = Math.max(cfg.minNum ?? 13, Math.min(cfg.maxNum ?? 30, rowH * .78));
        const tamRot = tamNum * .78, tamPts = Math.max(cfg.minPts ?? 9, tamNum * (cfg.ptsRatio ?? .45));
        const colW = cfg.colW ?? 168, meio = cfg.meio ?? W / 2, gap = cfg.gap ?? 14;
        const colunas = [meio - gap - colW, meio + gap];   // x inicial de cada coluna

        // divisória vertical
        if (acoes.length > 1) {
            ctx.fillStyle = cor.div;
            ctx.fillRect(meio - 1, topo + 4, 2, rowH * porColuna - 8);
        }

        acoes.forEach((a, i) => {
            if (!a.rotulo) return;   // linha vazia só para alinhar as colunas
            const col = i < porColuna ? 0 : 1;
            const lin = col === 0 ? i : i - porColuna;
            const x = colunas[col];
            const yb = topo + rowH * lin + rowH * .74;
            // a.vazio = ação que o atleta não fez (lista completa do card com nota): aparece apagada, sem número nem valor
            // a.num substitui a quantidade quando ela não é um número simples (ex.: PLACAR "6x3")
            const num = a.vazio ? "–" : (a.num || String(a.qtd > 0 || a.qtd < 0 ? a.qtd : "-"));
            const rot = a.sigla || siglaAcao(a.rotulo);

            // pontos da ação, pequenos, alinhados à direita da coluna
            ctx.textAlign = "right";
            ctx.font = `800 ${tamPts}px ${FONTE}`;
            let wP = 0;
            if (a.pontos) {
                ctx.fillStyle = a.neg ? cor.neg : (a.pontos.startsWith("+") ? cor.pos : cor.zero);
                ctx.fillText(a.pontos, x + colW, yb);
                wP = ctx.measureText(a.pontos).width + 8;
            }

            // número grande (dourado) e sigla (branca), como "78 PAC"
            ctx.textAlign = "left";
            ctx.font = `900 ${tamNum}px ${FONTE}`;
            ctx.fillStyle = a.vazio ? cor.numVazio : cor.num;
            ctx.fillText(num, x, yb);
            const wN = Math.max(ctx.measureText(num).width, ctx.measureText("00").width) + 10;
            ctx.fillStyle = a.vazio ? cor.rotVazio : cor.rot;
            ajustar(ctx, rot, colW - wN - wP, tamRot, 900);
            ctx.fillText(rot, x + wN, yb);
        });
    }

    if (d.total) {
        const yLinha = base + 4;
        const lg = ctx.createLinearGradient(60, 0, W - 60, 0);
        lg.addColorStop(0, "rgba(255,201,40,0)");
        lg.addColorStop(.5, "rgba(255,201,40,.8)");
        lg.addColorStop(1, "rgba(255,201,40,0)");
        ctx.fillStyle = lg;
        ctx.fillRect(60, yLinha, W - 120, 2);

        ctx.textAlign = "center";
        const rotT = (d.totalRotulo || "TOTAL") + "  ", valT = d.total;
        ctx.font = `900 18px ${FONTE}`;
        const wR = ctx.measureText(rotT).width;
        ctx.font = `900 24px ${FONTE}`;
        const wV = ctx.measureText(valT).width;
        const x0 = W / 2 - (wR + wV) / 2;
        ctx.textAlign = "left";
        ctx.font = `900 18px ${FONTE}`;
        ctx.fillStyle = "#ffffff";
        ctx.fillText(rotT, x0, yLinha + 28);
        ctx.font = `900 24px ${FONTE}`;
        ctx.fillStyle = d.totalCor ? (COR_NOTA[d.totalCor] || OURO) : (d.totalNeg ? "#ef4444" : OURO);
        ctx.fillText(valT, x0 + wR, yLinha + 28);
    }
}

function desenharCardJogador(ctx, d, im) {
    const W = CARD_W, H = CARD_H;
    const ny = 488, nh = 62;   // faixa do nome (o miolo é listrado acima dela e sólido abaixo)
    molduraPadrao(ctx, ny);   // moldura vetorizada (borda tripla + miolo escuro)

    // área da foto (o atleta "em pé" na base dela, em cima da linha do horizonte da moldura)
    const fx = 178, fy = 120, fw = 284, fh = 354;   // começa abaixo dos ombros da moldura, ao lado da coluna posição/escudo
    ctx.save();
    ctx.beginPath();
    formaJogadorDentro(ctx, 16);   // a foto não passa da borda da moldura
    ctx.clip();
    ctx.beginPath();
    ctx.rect(0, 0, W, fy + fh);
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

    raiosJogador(ctx);   // por cima da foto e por baixo dos textos

    // coluna da esquerda: posição e escudo do time
    ctx.textAlign = "center";
    ctx.textBaseline = "alphabetic";
    if (d.pontosTopo) {
        // pontuação total (card de pontuação) ou nota (card com nota): só o número, sem moldura nem fundo;
        // embaixo, posição e escudo empilhados ao lado da foto
        ctx.save();
        ctx.fillStyle = d.topoCor ? (COR_NOTA[d.topoCor] || OURO) : (d.pontosNeg ? "#ef4444" : "#ffffff");
        ctx.shadowColor = "rgba(0,0,0,.6)";
        ctx.shadowBlur = 8;
        // coluna em x = 124: à direita dos raios dourados da moldura (eles ocupam a borda até x ~107)
        const cx = 124;
        ajustar(ctx, d.pontosTopo, 100, 44, 900);
        ctx.fillText(d.pontosTopo, cx, 192);
        ctx.restore();
        ctx.textAlign = "center";
        ctx.save();
        ctx.fillStyle = OURO;
        ctx.shadowColor = "rgba(0,0,0,.6)";
        ctx.shadowBlur = 6;
        ajustar(ctx, d.pos || "", 90, 40, 900);
        ctx.fillText(d.pos || "", cx + 6, 254);
        ctx.fillRect(cx - 18, 265, 48, 4);
        ctx.restore();
        blocoTime(ctx, im.escudo, d.time, cx + 4, 352, 80);
    } else {
        ctx.fillStyle = OURO;
        ajustar(ctx, d.pos || "", 100, 50, 900);
        ctx.fillText(d.pos || "", 76, 172);
        ctx.fillRect(52, 186, 48, 4);
        blocoTime(ctx, im.escudo, d.time, 76, 268, 104);   // time, logo abaixo da posição
    }

    // faixa do nome
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

    // lista de ações: no card de pontuação, os pontos de cada ação; no card com nota, quanto cada ação somou na nota
    caixaAcoes(ctx, d, W);

    // rodapé
    ctx.textAlign = "center";
    ctx.fillStyle = "rgba(255,255,255,.6)";
    ajustar(ctx, ("ÚLTIMA PARTIDA" + (d.data ? "  ·  " + d.data : "")), 170, 14, 700);
    ctx.fillText("ÚLTIMA PARTIDA" + (d.data ? "  ·  " + d.data : ""), W / 2, 720);   // acima da ponta da moldura

    assinatura(ctx, 14, H - 12, 15, true);   // canto inferior esquerdo (fora da moldura, com contorno para ler em qualquer fundo)
}

// ----- Prévia do card e editor de foto (antes de baixar) -----
// Abre por cima da tela: mostra o card pronto, com "Editar foto" para cada atleta com foto, "Baixar" e "Cancelar".
// O editor apaga sobras do recorte ou restaura partes do atleta. Vale só para este card: a foto salva não muda.

const ESTILO_BOTAO = "border:0;border-radius:10px;padding:.65rem 1rem;font:700 .95rem system-ui,sans-serif;cursor:pointer;";
function botao(texto, tipo = "sec") {
    const b = document.createElement("button");
    b.type = "button";
    b.textContent = texto;
    b.style.cssText = ESTILO_BOTAO + (tipo === "pri"
        ? "background:#ffc928;color:#1b1404;"
        : tipo === "on" ? "background:#ffffff;color:#111;" : "background:#2a2d35;color:#f1f1f1;");
    return b;
}

function sobreposicao() {
    const ov = document.createElement("div");
    ov.setAttribute("role", "dialog");
    ov.setAttribute("aria-modal", "true");
    ov.style.cssText = "position:fixed;inset:0;z-index:12000;background:rgba(5,6,8,.92);display:flex;flex-direction:column;" +
        "align-items:center;justify-content:center;gap:.8rem;padding:1rem;box-sizing:border-box;overflow:auto;color:#eee;" +
        "font:500 .95rem system-ui,sans-serif;";
    document.body.appendChild(ov);
    return ov;
}

function linhaBotoes() {
    const l = document.createElement("div");
    l.style.cssText = "display:flex;flex-wrap:wrap;justify-content:center;gap:.5rem;";
    return l;
}

// op = { canvas, redesenhar(), fotos[], nomes[], arquivo, titulo }. Resolve quando a pessoa baixa ou cancela.
function revisarCard(op) {
    return new Promise(resolve => {
        const ov = sobreposicao();
        const titulo = document.createElement("strong");
        titulo.textContent = "Prévia do card";
        titulo.style.cssText = "font-size:1.05rem;";
        const area = document.createElement("div");
        area.style.cssText = "display:flex;justify-content:center;max-width:100%;";
        op.canvas.style.cssText = "max-width:min(94vw, " + (op.canvas.width > op.canvas.height ? "1100px" : "440px") + ");" +
            "max-height:68vh;width:auto;height:auto;border-radius:8px;";
        area.appendChild(op.canvas);
        const msg = document.createElement("p");
        msg.style.cssText = "margin:0;min-height:1.2em;color:#ffb4b4;text-align:center;";
        const edicao = linhaBotoes();
        const acoes = linhaBotoes();
        ov.append(titulo, area, edicao, msg, acoes);

        const fechar = () => {
            document.removeEventListener("keydown", tecla);
            ov.remove();
            resolve();
        };
        const tecla = e => { if (e.key === "Escape" && ov.style.display !== "none") fechar(); };
        document.addEventListener("keydown", tecla);

        const montarEdicao = () => {
            edicao.replaceChildren();
            op.fotos.forEach((f, i) => {
                if (!f) return;
                const nome = op.fotos.length > 1 ? " · " + (op.nomes[i] || i + 1) : "";
                const b = botao("✏️ Editar foto" + nome);
                b.onclick = async () => {
                    msg.textContent = "";
                    ov.style.display = "none";
                    const novo = await editarFoto(f, op.nomes[i]);
                    ov.style.display = "flex";
                    if (novo === undefined) return;   // cancelou
                    op.fotos[i] = novo;               // null = apagou tudo: o card usa a silhueta
                    op.redesenhar();
                    montarEdicao();
                };
                edicao.appendChild(b);
            });
        };

        const celular = /Android|iPhone|iPad|iPod/i.test(navigator.userAgent);
        const baixar = botao(celular ? "Compartilhar" : "Baixar", "pri");
        const cancelar = botao("Cancelar");
        baixar.onclick = async () => {
            baixar.disabled = true;
            msg.textContent = "";
            try {
                await entregar(op.canvas, op.arquivo, op.titulo);
                fechar();
            } catch (e) {
                msg.textContent = "Não consegui gerar a imagem: " + (e && e.message ? e.message : e);
                baixar.disabled = false;
            }
        };
        cancelar.onclick = fechar;
        acoes.append(cancelar, baixar);

        op.redesenhar();
        montarEdicao();
    });
}

// Editor: a foto inteira (sem cortar) por cima de um xadrez, com o original apagadinho por baixo
// (mostra o que dá para restaurar). Resolve com a foto nova (ou null se ficou vazia), ou undefined se cancelou.
function editarFoto(foto, nome) {
    return new Promise(resolve => {
        // prepara: recorte cheio + original. Foto que não é recorte (o removedor de fundo falhou) começa inteira.
        let base, orig;
        try {
            if (foto._cheio) {
                base = clonar(foto._cheio);
                orig = foto._orig || clonar(foto._cheio);
            } else {
                base = lerPixels(foto, 768).cv;
                orig = clonar(base);
            }
            base.getContext("2d", { willReadFrequently: true }).getImageData(0, 0, 1, 1);   // falha aqui se a foto não deixa editar (CORS)
        } catch {
            const ov = sobreposicao();
            const p = document.createElement("p");
            p.textContent = "Não dá para editar essa foto (o servidor da imagem não permite).";
            const ok = botao("Voltar", "pri");
            ok.onclick = () => { ov.remove(); resolve(undefined); };
            ov.append(p, ok);
            return;
        }

        const W = base.width, H = base.height;
        const ctx = base.getContext("2d", { willReadFrequently: true });
        const ov = sobreposicao();
        ov.style.touchAction = "none";

        const titulo = document.createElement("strong");
        titulo.textContent = "Editar foto" + (nome ? " · " + nome : "");
        const dica = document.createElement("p");
        dica.style.cssText = "margin:0;color:#aaa;text-align:center;max-width:32rem;font-size:.85rem;";
        dica.textContent = "Passe o dedo/mouse por cima. Apagar tira sobras do fundo; Restaurar traz de volta partes do atleta (a foto original aparece apagadinha por baixo).";

        // tamanho na tela
        const maxW = Math.min(window.innerWidth - 32, 560), maxH = window.innerHeight * .58;
        const esc = Math.min(maxW / W, maxH / H, 2);
        const dw = Math.round(W * esc), dh = Math.round(H * esc);

        const pilha = document.createElement("div");
        pilha.style.cssText = `position:relative;width:${dw}px;height:${dh}px;border-radius:8px;overflow:hidden;flex:none;` +
            "background:repeating-conic-gradient(#3b3b3b 0% 25%, #262626 0% 50%) 50% / 18px 18px;";
        const fundo = clonar(orig);
        fundo.style.cssText = `position:absolute;inset:0;width:${dw}px;height:${dh}px;opacity:.28;`;
        base.style.cssText = `position:absolute;inset:0;width:${dw}px;height:${dh}px;cursor:none;touch-action:none;`;
        const anel = document.createElement("div");
        anel.style.cssText = "position:absolute;border:2px solid #fff;border-radius:50%;pointer-events:none;display:none;" +
            "box-shadow:0 0 0 1px rgba(0,0,0,.6);";
        pilha.append(fundo, base, anel);

        // ferramentas
        let modo = "apagar";
        const bApagar = botao("Apagar", "on"), bRestaurar = botao("Restaurar");
        const marcar = () => {
            bApagar.style.cssText = ESTILO_BOTAO + (modo === "apagar" ? "background:#fff;color:#111;" : "background:#2a2d35;color:#f1f1f1;");
            bRestaurar.style.cssText = ESTILO_BOTAO + (modo === "restaurar" ? "background:#fff;color:#111;" : "background:#2a2d35;color:#f1f1f1;");
        };
        bApagar.onclick = () => { modo = "apagar"; marcar(); };
        bRestaurar.onclick = () => { modo = "restaurar"; marcar(); };

        const rotTam = document.createElement("label");
        rotTam.style.cssText = "display:flex;align-items:center;gap:.5rem;";
        const tam = document.createElement("input");
        tam.type = "range";
        tam.min = "6";
        tam.max = "90";
        tam.value = "28";
        rotTam.append("Pincel", tam);

        const historico = [];
        const bDesfazer = botao("↶ Desfazer");
        bDesfazer.disabled = true;
        bDesfazer.onclick = () => {
            const ultimo = historico.pop();
            if (ultimo) ctx.putImageData(ultimo, 0, 0);
            bDesfazer.disabled = historico.length === 0;
        };

        const ferramentas = linhaBotoes();
        ferramentas.style.alignItems = "center";
        ferramentas.append(bApagar, bRestaurar, rotTam, bDesfazer);

        const acoes = linhaBotoes();
        const bCancelar = botao("Cancelar"), bPronto = botao("Concluir", "pri");
        acoes.append(bCancelar, bPronto);

        ov.append(titulo, pilha, ferramentas, dica, acoes);

        // pintura
        const raio = () => (+tam.value / 2) / esc;   // raio em pixels da foto
        const ponto = e => {
            const r = base.getBoundingClientRect();
            return { x: (e.clientX - r.left) / esc, y: (e.clientY - r.top) / esc, sx: e.clientX - r.left, sy: e.clientY - r.top };
        };
        const carimbo = (x, y) => {
            const r = raio();
            ctx.save();
            ctx.beginPath();
            ctx.arc(x, y, r, 0, Math.PI * 2);
            if (modo === "apagar") {
                ctx.globalCompositeOperation = "destination-out";
                ctx.fill();
            } else {
                ctx.clip();
                ctx.clearRect(x - r - 1, y - r - 1, r * 2 + 2, r * 2 + 2);
                ctx.drawImage(orig, 0, 0);
            }
            ctx.restore();
        };
        let ultimo = null;
        const traco = (a, b) => {
            const passo = Math.max(1, raio() / 3);
            const dist = Math.hypot(b.x - a.x, b.y - a.y);
            const n = Math.max(1, Math.ceil(dist / passo));
            for (let k = 1; k <= n; k++) carimbo(a.x + (b.x - a.x) * k / n, a.y + (b.y - a.y) * k / n);
        };
        const mostrarAnel = p => {
            const d = +tam.value;
            anel.style.display = "block";
            anel.style.width = anel.style.height = d + "px";
            anel.style.left = (p.sx - d / 2) + "px";
            anel.style.top = (p.sy - d / 2) + "px";
        };
        base.addEventListener("pointerdown", e => {
            e.preventDefault();
            base.setPointerCapture(e.pointerId);
            historico.push(ctx.getImageData(0, 0, W, H));
            if (historico.length > 20) historico.shift();
            bDesfazer.disabled = false;
            ultimo = ponto(e);
            carimbo(ultimo.x, ultimo.y);
            mostrarAnel(ultimo);
        });
        base.addEventListener("pointermove", e => {
            const p = ponto(e);
            mostrarAnel(p);
            if (!ultimo) return;
            traco(ultimo, p);
            ultimo = p;
        });
        const soltar = () => { ultimo = null; };
        base.addEventListener("pointerup", soltar);
        base.addEventListener("pointercancel", soltar);
        base.addEventListener("pointerleave", () => { if (!ultimo) anel.style.display = "none"; });

        const fechar = valor => {
            document.removeEventListener("keydown", tecla);
            ov.remove();
            resolve(valor);
        };
        const tecla = e => {
            if (e.key === "Escape") fechar(undefined);
            if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "z") { e.preventDefault(); bDesfazer.click(); }
        };
        document.addEventListener("keydown", tecla);
        bCancelar.onclick = () => fechar(undefined);
        bPronto.onclick = () => {
            base.style.cssText = "";
            fechar(cortarNoAlfa(base, orig));   // null se apagou tudo
        };
    });
}
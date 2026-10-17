// Converte foto HEIC/HEIF (padrão do iPhone) em JPEG dentro do navegador.
// Chrome, Edge e Firefox não abrem HEIC; o Safari abre. Por isso:
//   1) tenta abrir direto (funciona no Safari, sem baixar nada);
//   2) se não abrir, carrega o conversor (lib/heic-to/heic-to.js, ~3 MB) só nesse momento e converte.
// O JPEG sai reduzido (maior lado = max px), igual ao que o SeletorFoto já faz com as outras fotos.

let carregando = null;

function carregarConversor() {
    if (window.HeicTo) return Promise.resolve();
    if (!carregando) {
        carregando = new Promise((resolve, reject) => {
            const s = document.createElement("script");
            s.src = new URL("lib/heic-to/heic-to.js", document.baseURI).href;
            s.onload = () => (window.HeicTo ? resolve() : reject(new Error("Conversor de HEIC indisponível")));
            s.onerror = () => { carregando = null; reject(new Error("Falha ao carregar o conversor de HEIC")); };
            document.head.appendChild(s);
        });
    }
    return carregando;
}

async function abrirDireto(blob) {
    try { return await createImageBitmap(blob); } catch { return null; }
}

// stream: DotNetStreamReference com os bytes do arquivo. Devolve o JPEG como Uint8Array.
export async function paraJpeg(stream, max) {
    const buffer = await stream.arrayBuffer();
    const heic = new Blob([buffer], { type: "image/heic" });

    let bitmap = await abrirDireto(heic);
    if (!bitmap) {
        await carregarConversor();
        const jpeg = await window.HeicTo({ blob: heic, type: "image/jpeg", quality: 0.92 });
        bitmap = await createImageBitmap(jpeg);
    }

    const escala = Math.min(1, max / Math.max(bitmap.width, bitmap.height));
    const w = Math.max(1, Math.round(bitmap.width * escala));
    const h = Math.max(1, Math.round(bitmap.height * escala));

    const canvas = document.createElement("canvas");
    canvas.width = w;
    canvas.height = h;
    canvas.getContext("2d").drawImage(bitmap, 0, 0, w, h);
    if (bitmap.close) bitmap.close();

    const saida = await new Promise((ok, erro) =>
        canvas.toBlob(b => (b ? ok(b) : erro(new Error("Não foi possível gerar o JPEG"))), "image/jpeg", 0.9));
    return new Uint8Array(await saida.arrayBuffer());
}

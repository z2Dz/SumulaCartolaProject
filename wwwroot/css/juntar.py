"""Junta app.css e todos os @import (recursivo) em app.bundle.css, sem @import.
Uso: python3 juntar.py   (rode dentro de wwwroot/css)"""
import re, pathlib
base = pathlib.Path(__file__).parent
def ler(arq):
    txt = (base / arq).read_text(encoding="utf-8")
    return re.sub(r'@import url\("(.+?)"\);', lambda m: ler(m.group(1)), txt)
lista = re.findall(r'@import url\("(.+?)"\)', (base / "app.css").read_text(encoding="utf-8"))
(base / "app.bundle.css").write_text("\n".join((base / p).read_text(encoding="utf-8") for p in lista), encoding="utf-8")
print("app.bundle.css gerado com", len(lista), "arquivos")

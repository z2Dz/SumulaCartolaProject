namespace SumulaCartolaProject.Servicos;

/// <summary>
/// Estado do zoom de foto. Quem pede o zoom (FotoJogador) chama Abrir; o ZoomFotoHost, que fica uma única vez
/// no layout, desenha a foto ampliada por cima de tudo e só fecha quando o usuário pedir.
/// </summary>
public class ServicoZoomFoto
{
    // Depois de abrir, um toque no fundo dentro desse intervalo é ignorado: no celular, soltar o dedo
    // depois do "segurar" gera um clique que cairia no fundo recém-aberto e fecharia a foto na hora.
    private const int IgnorarToqueMs = 500;

    public string? Nome { get; private set; }
    public string? FotoUrl { get; private set; }
    public bool Aberto => !string.IsNullOrWhiteSpace(FotoUrl);
    private DateTime abertoEm;

    public event Action? Mudou;

    public void Abrir(string nome, string fotoUrl)
    {
        if (string.IsNullOrWhiteSpace(fotoUrl)) return;
        Nome = nome;
        FotoUrl = fotoUrl;
        abertoEm = DateTime.UtcNow;
        Mudou?.Invoke();
    }

    public void Fechar()
    {
        if (!Aberto) return;
        Nome = null;
        FotoUrl = null;
        Mudou?.Invoke();
    }

    /// <summary>Toque no fundo escuro. Fecha, exceto logo após abrir.</summary>
    public void FecharPorToque()
    {
        if ((DateTime.UtcNow - abertoEm).TotalMilliseconds < IgnorarToqueMs) return;
        Fechar();
    }
}

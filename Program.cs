using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SumulaCartolaProject;
using SumulaCartolaProject.Servicos;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Lê wwwroot/appsettings.json (só a chave ANON; nunca coloque a service_role aqui)
var urlSupabase = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException("Supabase:Url não configurada");
var chaveAnon = builder.Configuration["Supabase:AnonKey"]
    ?? throw new InvalidOperationException("Supabase:AnonKey não configurada");

// Persistência da sessão no localStorage (o usuário continua logado ao reabrir o PWA)
builder.Services.AddScoped<IGotrueSessionPersistence<Session>, PersistenciaSessaoLocalStorage>();

builder.Services.AddScoped(sp =>
{
    var opcoes = new SupabaseOptions
    {
        AutoRefreshToken = true,
        AutoConnectRealtime = false,   // ligue só se for usar Realtime
        SessionHandler = sp.GetRequiredService<IGotrueSessionPersistence<Session>>()
    };
    return new Supabase.Client(urlSupabase, chaveAnon, opcoes);
});

// Autorização: o estado de login vem da sessão do Supabase
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<ProvedorEstadoAutenticacao>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ProvedorEstadoAutenticacao>());

builder.Services.AddScoped<ServicoAutenticacao>();
builder.Services.AddScoped<ServicoPerfil>();
builder.Services.AddScoped<ServicoSumula>();
builder.Services.AddScoped<ServicoMercado>();
builder.Services.AddScoped<ServicoTorcida>();
builder.Services.AddScoped<ServicoEscalacao>();
builder.Services.AddScoped<ServicoHistorico>();
builder.Services.AddScoped<ServicoTemporada>();
builder.Services.AddScoped<ServicoZoomFoto>();

var host = builder.Build();

// Restaura a sessão salva antes de renderizar qualquer página
await host.Services.GetRequiredService<Supabase.Client>().InitializeAsync();

await host.RunAsync();

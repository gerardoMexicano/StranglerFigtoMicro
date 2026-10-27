using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// nginx termina TLS y llama al gateway por http. Sin esto YARP le diría a los destinos
// "X-Forwarded-Proto: http" aunque el navegador esté en https, y las apps legacy armarían
// URLs y cookies como si no lo estuviera.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;

    // El gateway no publica puertos: solo lo alcanza nginx por la red edge, cuya IP es dinámica
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// La raíz lleva a la app nueva (06_frontend). 302 y no 301: el navegador no la guarda en caché
// y la entrada se puede mover sin que los usuarios queden atados al destino viejo.
app.MapMethods("/", [HttpMethods.Get, HttpMethods.Head], () => Results.Redirect("/newapp/"));

app.MapReverseProxy();

app.Run();

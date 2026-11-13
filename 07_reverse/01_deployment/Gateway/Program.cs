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

// /legacy/AngularPage es Angular (06_frontend, Reservación de salas) aunque siga bajo /legacy:
// la ruta legacy-angularpage-route de appsettings.json la manda al frontend nuevo antes que a IIS.
// Los enlaces viejos a AngularPage.aspx (Menu.aspx) llegan a la misma pantalla sin tocar WebForms.
// Rollback: quitar esa ruta y esta línea; AngularPage.aspx sigue publicada en IIS.
app.MapMethods("/legacy/AngularPage.aspx", [HttpMethods.Get, HttpMethods.Head],
    () => Results.Redirect("/legacy/AngularPage"));

// /legacy/Usuarios: igual que AngularPage. La URL de la página migrada sigue existiendo bajo /legacy
// y la sirve el frontend nuevo (legacy-usuarios-route), con el Checador de Asistencia de Angular.
// Rollback: quitar esa ruta y esta línea; Usuarios.aspx en IIS redirige a /newapp/usuarios.
app.MapMethods("/legacy/Usuarios.aspx", [HttpMethods.Get, HttpMethods.Head],
    () => Results.Redirect("/legacy/Usuarios"));

app.MapReverseProxy();

app.Run();

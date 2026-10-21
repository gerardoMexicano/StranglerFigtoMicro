using Microsoft.Owin;
using Owin;

// Microsoft.Owin.Host.SystemWeb busca esta clase al arrancar la app en IIS.
// Sin ella, la app no inicia ("No assembly found containing an OwinStartupAttribute").
[assembly: OwinStartup(typeof(WebApp_MVC.Startup))]

namespace WebApp_MVC
{
    public class Startup
    {
        // Pipeline OWIN. Por ahora vacío: la autenticación la sigue haciendo
        // FormsAuthentication (Web.config). Aquí se agregarán el middleware de
        // cookies y el de OpenID Connect contra Keycloak.
        public void Configuration(IAppBuilder app)
        {
        }
    }
}

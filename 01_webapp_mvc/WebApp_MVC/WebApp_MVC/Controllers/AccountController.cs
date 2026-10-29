using System.Configuration;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OpenIdConnect;

namespace WebApp_MVC.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        // GET: Account/Login
        // Ya no pide contraseña: manda al usuario a Keycloak y al volver lo deja en returnUrl
        public ActionResult Login(string returnUrl)
        {
            // Solo rutas de esta app, para no convertir el login en un redirect abierto
            string destino = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Menu", "Home");

            if (User.Identity.IsAuthenticated)
            {
                return Redirect(destino);
            }

            HttpContext.GetOwinContext().Authentication.Challenge(
                new AuthenticationProperties { RedirectUri = destino },
                OpenIdConnectAuthenticationDefaults.AuthenticationType);

            // El 401 es la señal para el middleware OIDC: lo convierte en el redirect a Keycloak
            return new HttpUnauthorizedResult();
        }

        // Cierra las DOS sesiones: la cookie de esta app y la de Keycloak.
        // Si solo se borrara la cookie, el siguiente clic volvería a entrar por SSO sin pedir contraseña.
        public void Logout()
        {
            if (!User.Identity.IsAuthenticated)
            {
                Response.Redirect(Url.Content("~/"));
                return;
            }

            // El middleware OIDC convierte esto en el redirect al end_session_endpoint de Keycloak,
            // que al terminar regresa a oidc:PostLogoutRedirectUri
            HttpContext.GetOwinContext().Authentication.SignOut(
                OpenIdConnectAuthenticationDefaults.AuthenticationType,
                CookieAuthenticationDefaults.AuthenticationType);
        }

        // GET: Account/FrontchannelLogout?iss=...&sid=...
        // La llama Keycloak en un iframe cuando la sesión SSO se cierra desde otra app (ver CierreDesdeKeycloak).
        // Solo borra la cookie de esta app; siempre responde 200 vacío, sin caché.
        public ActionResult FrontchannelLogout(string iss, string sid)
        {
            if (CierreDesdeKeycloak.EsDeEstaSesion(User as ClaimsPrincipal, iss, sid,
                    ConfigurationManager.AppSettings["oidc:Authority"]))
            {
                HttpContext.GetOwinContext().Authentication.SignOut(CookieAuthenticationDefaults.AuthenticationType);
            }

            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            return new EmptyResult();
        }
    }
}

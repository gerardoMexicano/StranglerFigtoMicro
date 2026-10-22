using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Microsoft.Owin.Security;
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

        // POST: Account/Login
        [HttpPost]
        public ActionResult Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Debe ingresar usuario y contraseña.";
                return View();
            }

            if (ValidarCredenciales(username, password))
            {
                FormsAuthentication.SetAuthCookie(username, false);
                return RedirectToAction("Menu", "Home");
            }
            else
            {
                ViewBag.Error = "Credenciales incorrectas.";
                return View();
            }
        }

        // Hash: SHA-512( salt + UTF-16LE(password) ), igual que sqlserver/db/03_usuarios_hash_expand.sql
        private bool ValidarCredenciales(string usuario, string password)
        {
            string conexionString = ConfigurationManager.ConnectionStrings["ConexionDB"].ConnectionString;

            using (SqlConnection conexion = new SqlConnection(conexionString))
            {
                conexion.Open();

                int id;
                byte[] salt;
                byte[] hashGuardado;

                string query = "SELECT id, password_salt, password_hash FROM usuarios WHERE username = @usuario AND activo = 1";

                using (SqlCommand comando = new SqlCommand(query, conexion))
                {
                    comando.Parameters.AddWithValue("@usuario", usuario);

                    using (SqlDataReader lector = comando.ExecuteReader())
                    {
                        if (!lector.Read() || lector.IsDBNull(1) || lector.IsDBNull(2))
                        {
                            return false;
                        }

                        id = lector.GetInt32(0);
                        salt = (byte[])lector[1];
                        hashGuardado = (byte[])lector[2];
                    }
                }

                if (!SonIguales(CalcularHash(salt, password), hashGuardado))
                {
                    return false;
                }

                using (SqlCommand comando = new SqlCommand("UPDATE usuarios SET ultimo_login = SYSDATETIME() WHERE id = @id", conexion))
                {
                    comando.Parameters.AddWithValue("@id", id);
                    comando.ExecuteNonQuery();
                }

                return true;
            }
        }

        private static byte[] CalcularHash(byte[] salt, string password)
        {
            byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
            byte[] entrada = new byte[salt.Length + passwordBytes.Length];
            Buffer.BlockCopy(salt, 0, entrada, 0, salt.Length);
            Buffer.BlockCopy(passwordBytes, 0, entrada, salt.Length, passwordBytes.Length);

            using (SHA512 sha = SHA512.Create())
            {
                return sha.ComputeHash(entrada);
            }
        }

        // Comparación en tiempo constante (.NET Framework no trae CryptographicOperations.FixedTimeEquals)
        private static bool SonIguales(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            int diferencia = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diferencia |= a[i] ^ b[i];
            }

            return diferencia == 0;
        }

        [Authorize]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "Account");
        }
    }
}

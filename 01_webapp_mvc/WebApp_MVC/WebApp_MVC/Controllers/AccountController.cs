using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;
using System.Web.Security;

namespace WebApp_MVC.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        // GET: Account/Login
        public ActionResult Login()
        {
            return View();
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

        private bool ValidarCredenciales(string usuario, string password)
        {
            string conexionString = ConfigurationManager.ConnectionStrings["ConexionDB"].ConnectionString;

            using (SqlConnection conexion = new SqlConnection(conexionString))
            {
                string query = "SELECT COUNT(*) FROM Usuarios WHERE username = @usuario AND password = @password";

                using (SqlCommand comando = new SqlCommand(query, conexion))
                {
                    comando.Parameters.AddWithValue("@usuario", usuario);
                    comando.Parameters.AddWithValue("@password", password);

                    conexion.Open();
                    int resultado = Convert.ToInt32(comando.ExecuteScalar());

                    return resultado > 0;
                }
            }
        }

        [Authorize]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "Account");
        }
    }
}

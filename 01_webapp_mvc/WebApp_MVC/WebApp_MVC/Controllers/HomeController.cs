using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace WebApp_MVC.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        [AllowAnonymous]
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Menu()
        {
            return View();
        }

        public ActionResult PaginaAntigua()
        {
            return View();
        }

        public ActionResult PaginaAntiguaNuevoBackend()
        {
            return View();
        }

        public ActionResult PaginaAngular()
        {
            return View();
        }

        public ActionResult PaginaPrincipal()
        {
            return RedirectToAction("Menu");
        }
    }
}
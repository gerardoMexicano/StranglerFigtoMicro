Imports System.Web.Optimization

Public Class Global_asax
    Inherits HttpApplication

    Sub Application_Start(sender As Object, e As EventArgs)
        ' Fires when the application is started
        RouteConfig.RegisterRoutes(RouteTable.Routes)
        BundleConfig.RegisterBundles(BundleTable.Bundles)
    End Sub

    Sub Application_BeginRequest(sender As Object, e As EventArgs)
        ' La portada es pública, pero <location> del Web.config solo ve la URL: para "/" IIS aún no
        ' sabe que el documento por defecto es Default.aspx y <deny users="?"> la mandaría a Keycloak.
        ' Se reescribe por dentro (la URL del navegador no cambia) antes de revisar permisos.
        If Request.AppRelativeCurrentExecutionFilePath = "~/" Then
            Context.RewritePath("~/Default")
        End If
    End Sub
End Class
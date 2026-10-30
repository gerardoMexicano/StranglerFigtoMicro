Imports System.Configuration

' Strangler Fig: el Checador de Asistencia ya vive en Angular (/newapp/usuarios), que registra
' cada check-in en 05_backend (/api/attendance/check-ins). Esta página queda solo para que los
' enlaces y favoritos viejos a /legacy/Usuarios.aspx sigan llegando a la versión nueva.
Public Class Usuarios
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        ' 302: si la migración se revierte, el navegador no se quedó con el destino en caché
        Response.Redirect(ConfigurationManager.AppSettings("RutaNewApp") & "/usuarios", False)
        Context.ApplicationInstance.CompleteRequest()
    End Sub

End Class

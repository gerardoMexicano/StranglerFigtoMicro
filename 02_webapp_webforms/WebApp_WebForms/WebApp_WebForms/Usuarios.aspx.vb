Imports System.Configuration

' Strangler Fig: el Checador de Asistencia ya es Angular y registra cada check-in en 05_backend
' (/api/attendance/check-ins). Por el gateway esta página ni se alcanza: /legacy/Usuarios y
' /legacy/Usuarios.aspx los sirve el frontend nuevo (legacy-usuarios-route). Este redirect queda
' para quien entre directo al IIS, sin gateway.
Public Class Usuarios
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        ' 302: si la migración se revierte, el navegador no se quedó con el destino en caché
        Response.Redirect(ConfigurationManager.AppSettings("RutaNewApp") & "/usuarios", False)
        Context.ApplicationInstance.CompleteRequest()
    End Sub

End Class

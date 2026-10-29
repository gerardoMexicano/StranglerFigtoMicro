Imports System.Security.Claims

' Mismo código que WebApp_MVC/App_Start/CierreDesdeKeycloak.cs.
' Front-channel logout (logout global). Cuando el usuario cierra sesión en cualquier app (MVC,
' WebForms o /newapp), Keycloak cierra la sesión SSO y carga en un iframe, por cada app con sesión,
' su frontchannel.logout.url con ?iss=...&sid=... Aquí se decide si esa petición es de la sesión
' de este navegador: si lo es, se borra la cookie local (y con ella la sesión de SesionEnServidor).
' Keycloak ya cerró la suya, así que no se vuelve a llamar al end_session_endpoint.
Public Module CierreDesdeKeycloak

    Public Function EsDeEstaSesion(usuario As ClaimsPrincipal, iss As String, sid As String, authority As String) As Boolean
        If usuario Is Nothing OrElse usuario.Identity Is Nothing OrElse Not usuario.Identity.IsAuthenticated Then
            Return False
        End If

        ' Solo el Keycloak de esta app (oidc:Authority) puede pedir el cierre
        If Not String.Equals(If(iss, "").TrimEnd("/"c), If(authority, "").TrimEnd("/"c), StringComparison.Ordinal) Then
            Return False
        End If

        ' Katana guarda el "sid" del id_token como ClaimTypes.Sid (mapeo por defecto del handler JWT).
        ' Debe coincidir: así otra página no puede cerrar la sesión con un iframe sin conocer el sid.
        Dim sidDeLaSesion As Claim = If(usuario.FindFirst(ClaimTypes.Sid), usuario.FindFirst("sid"))
        If sidDeLaSesion Is Nothing Then
            ' Sesión sin sid (no debería pasar con Keycloak): sin con qué comparar, se cierra
            Return True
        End If

        Return String.Equals(sidDeLaSesion.Value, sid, StringComparison.Ordinal)
    End Function

End Module

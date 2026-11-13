<%@ Page Title="Menú Principal" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Menu.aspx.vb" Inherits="WebApp_WebForms.Menu" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">

    <div class="menu-wrapper">
        <div class="menu-header">
            <h2>Menú Principal</h2>
            <p>Bienvenido, <strong><asp:Label ID="lblUsuario" runat="server" /></strong></p>
        </div>

        <div class="menu-grid">
            <div class="menu-card">
                <div class="menu-icon">📋</div>
                <h4>Reportes</h4>
                <p>Consulta y genera reportes del sistema.</p>
                <a href="Reportes.aspx" class="menu-link">Ir a Reportes </a>
            </div>
            <div class="menu-card">
                <div class="menu-icon">👥</div>
                <h4>Usuarios</h4>
                <p>Administración de usuarios del sistema.</p>
                <%-- Migrada: el gateway sirve /legacy/Usuarios con Angular + backend nuevo (sin gateway, FriendlyUrls lleva a Usuarios.aspx) --%>
                <a href="Usuarios" class="menu-link">Ir a Usuarios</a>
            </div>
            <div class="menu-card">
                <div class="menu-icon">⚙️</div>
                <h4>Configuración</h4>
                <p>Ajustes generales de la aplicación.</p>
                <a href="Configuracion.aspx" class="menu-link">Ir a Configuración</a>
            </div>
            <div class="menu-card">
                <div class="menu-icon">📊</div>
                <h4>Dashboard</h4>
                <p>Vista general y estadísticas del sistema.</p>
                <a href="Dashboard.aspx" class="menu-link">Ir a Dashboard</a>
            </div>
            <div class="menu-card">
                <div class="menu-icon">🚀</div>
                <h4>Segunda Aplicacion</h4>
                <p>Aplicacion construida en C# 4.8.1 Representa Si ya Sabes.</p>
                <a href="<%= ConfigurationManager.AppSettings("RutaAppMVC") + "/Home/Menu" %>" class="menu-link">BrokenTaxes</a>
            </div>
            <div class="menu-card">
                <div class="menu-icon">⭐​</div>
                <h4>Angular</h4>
                <p>Aplicacion construida en Angular Lo nuevo Todo nuevo.</p>
                <a href="AngularPage.aspx" class="menu-link">Angular Nuevo</a>
            </div>
            <div class="menu-card">
                <div class="menu-icon">🥀​​</div>
                <h4>Frontend Antiguo backend nuevo</h4>
                <p>pagina construida en forma Nativa pero usa bakckend independiente asi seria la migracion.</p>
                <a href="LEgFrontNEWBACK.aspx" class="menu-link">Pagina Con  mas Potencia </a>
            </div>
        </div>

        <div class="menu-logout">
            <asp:Button ID="btnCerrarSesion" runat="server" Text="Cerrar Sesión" CssClass="btn-logout" OnClick="btnCerrarSesion_Click" />
        </div>
    </div>

    <style>
        .menu-wrapper {
            max-width: 900px;
            margin: 40px auto;
            padding: 0 20px;
        }
        .menu-header {
            text-align: center;
            margin-bottom: 36px;
        }
        .menu-header h2 {
            font-size: 2rem;
            font-weight: 700;
            color: #1a1a2e;
        }
        .menu-header p { color: #555; font-size: 1rem; }
        .menu-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 20px;
            margin-bottom: 36px;
        }
        .menu-card {
            background: #fff;
            border-radius: 12px;
            box-shadow: 0 2px 16px rgba(0,0,0,0.09);
            padding: 28px 20px;
            text-align: center;
            transition: transform 0.2s, box-shadow 0.2s;
        }
        .menu-card:hover {
            transform: translateY(-4px);
            box-shadow: 0 6px 24px rgba(0,0,0,0.13);
        }
        .menu-icon { font-size: 2.4rem; margin-bottom: 12px; }
        .menu-card h4 { font-size: 1.1rem; font-weight: 700; color: #1a1a2e; margin-bottom: 8px; }
        .menu-card p { color: #777; font-size: 0.88rem; margin-bottom: 14px; }
        .menu-link {
            display: inline-block;
            padding: 7px 18px;
            background: #16213e;
            color: #fff;
            border-radius: 6px;
            font-size: 0.88rem;
            text-decoration: none;
            transition: background 0.2s;
        }
        .menu-link:hover { background: #0f3460; color: #fff; text-decoration: none; }
        .menu-logout { text-align: center; }
        .btn-logout {
            padding: 10px 30px;
            background: #c0392b;
            color: #fff;
            border: none;
            border-radius: 7px;
            font-size: 0.95rem;
            cursor: pointer;
            transition: background 0.2s;
        }
        .btn-logout:hover { background: #922b21; }
    </style>

</asp:Content>

<%@ Page Title="Iniciar Sesión" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.vb" Inherits="WebApp_WebForms.Login" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">

    <div class="login-wrapper">
        <div class="login-card">
            <div class="login-header">
                <h2>Iniciar Sesión</h2>
                <p>Ingresa tus credenciales para continuar</p>
            </div>

            <asp:Label ID="lblError" runat="server" CssClass="login-error" Visible="false" />

            <div class="form-group">
                <label for="txtUsuario">Usuario</label>
                <asp:TextBox ID="txtUsuario" runat="server" CssClass="form-control" placeholder="Ingresa tu usuario" />
            </div>

            <div class="form-group">
                <label for="txtPassword">Contraseña</label>
                <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" TextMode="Password" placeholder="Ingresa tu contraseña" />
            </div>

            <asp:Button ID="btnLogin" runat="server" Text="Entrar" CssClass="btn-login" OnClick="btnLogin_Click" />

            <div class="login-footer">
                <a href="Default.aspx">← Volver al inicio</a>
            </div>
        </div>
    </div>

    <style>
        .login-wrapper {
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 70vh;
        }
        .login-card {
            background: #fff;
            border-radius: 12px;
            box-shadow: 0 4px 24px rgba(0,0,0,0.12);
            padding: 40px 48px;
            width: 100%;
            max-width: 420px;
        }
        .login-header {
            text-align: center;
            margin-bottom: 28px;
        }
        .login-header h2 {
            font-size: 1.8rem;
            font-weight: 700;
            color: #1a1a2e;
            margin-bottom: 6px;
        }
        .login-header p {
            color: #888;
            font-size: 0.95rem;
        }
        .form-group {
            margin-bottom: 18px;
        }
        .form-group label {
            display: block;
            font-weight: 600;
            color: #333;
            margin-bottom: 6px;
        }
        .form-control {
            width: 100%;
            padding: 10px 14px;
            border: 1.5px solid #ddd;
            border-radius: 7px;
            font-size: 1rem;
            box-sizing: border-box;
            transition: border-color 0.2s;
        }
        .form-control:focus {
            outline: none;
            border-color: #16213e;
        }
        .btn-login {
            width: 100%;
            padding: 12px;
            background: #16213e;
            color: #fff;
            border: none;
            border-radius: 8px;
            font-size: 1.05rem;
            font-weight: 600;
            cursor: pointer;
            margin-top: 8px;
            transition: background 0.2s;
        }
        .btn-login:hover {
            background: #0f3460;
        }
        .login-error {
            display: block;
            background: #ffeaea;
            color: #c0392b;
            border: 1px solid #e74c3c;
            border-radius: 6px;
            padding: 10px 14px;
            margin-bottom: 16px;
            font-size: 0.93rem;
        }
        .login-footer {
            text-align: center;
            margin-top: 20px;
        }
        .login-footer a {
            color: #888;
            font-size: 0.9rem;
            text-decoration: none;
        }
        .login-footer a:hover { color: #16213e; }
    </style>

</asp:Content>

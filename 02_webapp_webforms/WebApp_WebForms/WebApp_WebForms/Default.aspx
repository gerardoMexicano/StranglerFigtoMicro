<%@ Page Title="Inicio" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.vb" Inherits="WebApp_WebForms._Default" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">

    <div class="landing-hero">
        <div class="landing-content">
            <h1 class="landing-title">Bienvenido al Sistema</h1>
            <p class="landing-subtitle">Plataforma de gestión interna. Inicia sesión para acceder a tus funciones.</p>
            <a href="Login.aspx" class="btn btn-primary btn-lg landing-btn">
                <span>Iniciar Sesión</span>
                <i class="arrow-icon">→</i>
            </a>
        </div>
    </div>

    <style>
        .landing-hero {
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 70vh;
            text-align: center;
        }
        .landing-content {
            max-width: 600px;
            padding: 40px;
            background: #fff;
            border-radius: 12px;
            box-shadow: 0 4px 24px rgba(0,0,0,0.10);
        }
        .landing-title {
            font-size: 2.4rem;
            font-weight: 700;
            color: #1a1a2e;
            margin-bottom: 16px;
        }
        .landing-subtitle {
            font-size: 1.1rem;
            color: #555;
            margin-bottom: 32px;
        }
        .landing-btn {
            padding: 14px 36px;
            font-size: 1.1rem;
            border-radius: 8px;
            background: #16213e;
            border: none;
            color: #fff;
            text-decoration: none;
            display: inline-flex;
            align-items: center;
            gap: 10px;
            transition: background 0.2s;
        }
        .landing-btn:hover {
            background: #0f3460;
            color: #fff;
            text-decoration: none;
        }
        .arrow-icon { font-style: normal; font-size: 1.2rem; }
    </style>

</asp:Content>

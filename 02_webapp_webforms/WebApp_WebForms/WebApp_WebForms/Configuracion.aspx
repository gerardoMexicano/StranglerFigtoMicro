<%@ Page Language="vb" AutoEventWireup="false" CodeBehind="Configuracion.aspx.vb" Inherits="WebApp_WebForms.Configuracion" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Configuración</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #f4f7f6; margin: 0; padding: 0; display: flex; justify-content: center; align-items: center; height: 100vh; }
        .container { background: #ffffff; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.1); padding: 40px; max-width: 500px; text-align: center; animation: fadeIn 0.5s ease-out; }
        @keyframes fadeIn { from { opacity: 0; transform: translateY(-20px); } to { opacity: 1; transform: translateY(0); } }
        .icon { font-size: 4rem; margin-bottom: 20px; }
        h1 { color: #1a1a2e; font-size: 2rem; margin-bottom: 10px; font-weight: 600; }
        p { color: #555; font-size: 1.1rem; line-height: 1.6; margin-bottom: 30px; }
        .btn-back { display: inline-block; padding: 12px 24px; background: #16213e; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold; transition: all 0.3s ease; }
        .btn-back:hover { background: #0f3460; transform: translateY(-2px); box-shadow: 0 4px 10px rgba(0,0,0,0.15); text-decoration: none; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="container">
            <div class="icon">⚙️</div>
            <h1>Configuración</h1>
            <p>Panel de control para ajustar las preferencias y variables generales de la aplicación.</p>
            <a href="Menu.aspx" class="btn-back">⬅ Volver al Menú Principal</a>
        </div>
    </form>
</body>
</html>

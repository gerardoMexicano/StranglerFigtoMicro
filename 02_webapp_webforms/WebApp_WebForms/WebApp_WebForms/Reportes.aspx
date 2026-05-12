<%@ Page Language="vb" AutoEventWireup="false" CodeBehind="Reportes.aspx.vb" Inherits="WebApp_WebForms.Reportes" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Reportes</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #f4f7f6; margin: 0; padding: 0; display: flex; justify-content: center; align-items: center; height: 100vh; }
        .container { background: #ffffff; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.1); padding: 40px; max-width: 500px; text-align: center; animation: fadeIn 0.5s ease-out; }
        @keyframes fadeIn { from { opacity: 0; transform: translateY(-20px); } to { opacity: 1; transform: translateY(0); } }
        .icon { font-size: 4rem; margin-bottom: 20px; }
        h1 { color: #1a1a2e; font-size: 2rem; margin-bottom: 10px; font-weight: 600; }
        p { color: #555; font-size: 1.1rem; line-height: 1.6; margin-bottom: 30px; }
        .btn-back { display: inline-block; padding: 12px 24px; background: #16213e; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold; transition: all 0.3s ease; margin-top: 10px; }
        .btn-back:hover { background: #0f3460; transform: translateY(-2px); box-shadow: 0 4px 10px rgba(0,0,0,0.15); text-decoration: none; }
        .btn-action { display: inline-block; padding: 12px 24px; background: #e94560; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold; transition: all 0.3s ease; border: none; cursor: pointer; margin-bottom: 20px; font-size: 1rem; }
        .btn-action:hover { background: #d03050; transform: translateY(-2px); box-shadow: 0 4px 10px rgba(0,0,0,0.2); }
        .file-upload-wrapper { margin-bottom: 20px; text-align: left; background: #f8f9fa; padding: 15px; border-radius: 8px; border: 1px dashed #ccc; }
        .file-upload-wrapper input[type=file] { width: 100%; font-size: 1rem; color: #555; }
        .status-msg { margin-top: 20px; font-weight: bold; color: #0f3460; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="container">
            <div class="icon">📋</div>
            <h1>Reportes</h1>
                        <p>Módulo para la consulta, visualización y generación de reportes detallados del sistema.</p>
            <div class="file-upload-wrapper">
                <label for="fuXmlFiles" style="display:block; margin-bottom: 8px; font-weight: 600; color: #333;">Sube tus archivos XML:</label>
                <asp:FileUpload ID="fuXmlFiles" runat="server" AllowMultiple="true" accept=".xml" />
            </div>
            <asp:Button ID="btnGenerarPDFs" runat="server" Text="Generar PDFs (Proceso Pesado)" CssClass="btn-action" OnClick="btnGenerarPDFs_Click" />
            <br />
            <a href="Menu.aspx" class="btn-back">⬅ Volver al Menú Principal</a>
            <div class="status-msg">
                <asp:Literal ID="litStatus" runat="server"></asp:Literal>
            </div>
        </div>
    </form>
</body>
</html>

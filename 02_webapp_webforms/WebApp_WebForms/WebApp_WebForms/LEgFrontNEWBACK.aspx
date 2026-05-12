<%@ Page Language="vb" AutoEventWireup="false" CodeBehind="LEgFrontNEWBACK.aspx.vb"
    Inherits="WebApp_WebForms.LEgFrontNEWBACK" %>

    <!DOCTYPE html>
    <html xmlns="http://www.w3.org/1999/xhtml">

    <head runat="server">
        <title>Frontend Antiguo Backend Nuevo</title>
        <style>
            body {
                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                background: #f4f7f6;
                margin: 0;
                padding: 20px;
                display: flex;
                justify-content: center;
                align-items: flex-start;
                min-height: 100vh;
            }

            .container {
                background: #ffffff;
                border-radius: 12px;
                box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1);
                padding: 40px;
                width: 95%;
                max-width: 1400px;
                text-align: center;
                animation: fadeIn 0.5s ease-out;
            }

            @keyframes fadeIn {
                from {
                    opacity: 0;
                    transform: translateY(-20px);
                }

                to {
                    opacity: 1;
                    transform: translateY(0);
                }
            }

            .icon {
                font-size: 4rem;
                margin-bottom: 20px;
            }

            h1 {
                color: #1a1a2e;
                font-size: 2rem;
                margin-bottom: 10px;
                font-weight: 600;
            }

            p {
                color: #555;
                font-size: 1.1rem;
                line-height: 1.6;
                margin-bottom: 30px;
            }

            .btn-back {
                display: inline-block;
                padding: 12px 24px;
                background: #16213e;
                color: #ffffff;
                text-decoration: none;
                border-radius: 8px;
                font-weight: bold;
                transition: all 0.3s ease;
                margin-bottom: 20px;
            }

            .btn-back:hover {
                background: #0f3460;
                transform: translateY(-2px);
                box-shadow: 0 4px 10px rgba(0, 0, 0, 0.15);
                text-decoration: none;
            }

            .filter-section {
                margin-bottom: 30px;
                background: #f8f9fa;
                padding: 20px;
                border-radius: 8px;
                border: 1px solid #ddd;
                display: flex;
                justify-content: center;
                gap: 15px;
                align-items: center;
                flex-wrap: wrap;
            }

            .filter-section input[type="date"] {
                padding: 10px;
                border: 1px solid #ccc;
                border-radius: 5px;
                font-size: 1rem;
            }

            .btn-action {
                padding: 10px 25px;
                background: #e94560;
                color: #fff;
                border: none;
                border-radius: 5px;
                font-weight: bold;
                font-size: 1rem;
                cursor: pointer;
                transition: background 0.3s;
            }

            .btn-action:hover {
                background: #d03050;
            }

            .table-section {
                margin-bottom: 40px;
                text-align: left;
                overflow-x: auto;
            }

            .table-title {
                font-size: 1.5rem;
                color: #16213e;
                border-bottom: 2px solid #e94560;
                padding-bottom: 5px;
                margin-bottom: 15px;
                font-weight: bold;
            }

            .grid-view {
                width: 100%;
                border-collapse: collapse;
                margin-top: 10px;
                font-size: 0.9rem;
            }

            .grid-view th,
            .grid-view td {
                border: 1px solid #ddd;
                padding: 8px;
            }

            .grid-view th {
                background-color: #16213e;
                color: white;
                text-align: left;
            }

            .grid-view tr:nth-child(even) {
                background-color: #f2f2f2;
            }

            .grid-view tr:hover {
                background-color: #ddd;
            }

            .pager-style {
                background-color: #16213e;
                color: white;
                text-align: center;
            }

            .pager-style a,
            .pager-style span {
                padding: 5px 10px;
                color: white;
                text-decoration: none;
                font-weight: bold;
            }

            .pager-style span {
                background-color: #e94560;
                border-radius: 3px;
            }
        </style>
    </head>

    <body>
        <form id="form1" runat="server">
            <asp:ScriptManager ID="ScriptManager1" runat="server" />
            <div class="container">
                <div class="icon">🥀</div>
                <h1>Frontend Antiguo Backend Nuevo</h1>
                <p>Página que demuestra la migración progresiva, cargando tablas gigantes de forma síncrona y sin
                    optimizar para evidenciar la lentitud del proceso antiguo.</p>
                <a href="Menu.aspx" class="btn-back">⬅ Volver al Menú Principal</a>

                <div class="filter-section">
                    <label for="txtFechaInicio"><strong>Fecha Inicio:</strong></label>
                    <asp:TextBox ID="txtFechaInicio" runat="server" TextMode="Date"></asp:TextBox>

                    <label for="txtFechaFinal"><strong>Fecha Final:</strong></label>
                    <asp:TextBox ID="txtFechaFinal" runat="server" TextMode="Date"></asp:TextBox>

                    <input type="button" id="btnStart" value="Cargar Registros" class="btn-action"
                        onclick="startLoading();" />
                </div>

                <div class="table-section">
                    <div class="table-title">Customer Transactions <span id="lblStatus1"
                            style="font-size:1rem; color:red;"></span></div>
                    <asp:UpdatePanel ID="up1" runat="server" UpdateMode="Conditional">
                        <ContentTemplate>
                            <asp:Button ID="btnLoad1" runat="server" Text="Load 1" style="display:none;"
                                OnClick="btnLoad1_Click" />
                            <asp:GridView ID="gvCustomerTransactions" runat="server" CssClass="grid-view"
                                AutoGenerateColumns="true" AllowPaging="true" PageSize="50"
                                OnPageIndexChanging="gv1_PageIndexChanging" PagerStyle-CssClass="pager-style">
                            </asp:GridView>
                        </ContentTemplate>
                    </asp:UpdatePanel>
                </div>

                <div class="table-section">
                    <div class="table-title">Invoice Lines <span id="lblStatus2"
                            style="font-size:1rem; color:red;"></span></div>
                    <asp:UpdatePanel ID="up2" runat="server" UpdateMode="Conditional">
                        <ContentTemplate>
                            <asp:Button ID="btnLoad2" runat="server" Text="Load 2" style="display:none;"
                                OnClick="btnLoad2_Click" />
                            <asp:GridView ID="gvInvoiceLines" runat="server" CssClass="grid-view"
                                AutoGenerateColumns="true" AllowPaging="true" PageSize="50"
                                OnPageIndexChanging="gv2_PageIndexChanging" PagerStyle-CssClass="pager-style">
                            </asp:GridView>
                        </ContentTemplate>
                    </asp:UpdatePanel>
                </div>

                <div class="table-section">
                    <div class="table-title">Invoices <span id="lblStatus3" style="font-size:1rem; color:red;"></span>
                    </div>
                    <asp:UpdatePanel ID="up3" runat="server" UpdateMode="Conditional">
                        <ContentTemplate>
                            <asp:Button ID="btnLoad3" runat="server" Text="Load 3" style="display:none;"
                                OnClick="btnLoad3_Click" />
                            <asp:GridView ID="gvInvoices" runat="server" CssClass="grid-view" AutoGenerateColumns="true"
                                AllowPaging="true" PageSize="50" OnPageIndexChanging="gv3_PageIndexChanging"
                                PagerStyle-CssClass="pager-style">
                            </asp:GridView>
                        </ContentTemplate>
                    </asp:UpdatePanel>
                </div>

                <div class="table-section">
                    <div class="table-title">Order Lines <span id="lblStatus4"
                            style="font-size:1rem; color:red;"></span></div>
                    <asp:UpdatePanel ID="up4" runat="server" UpdateMode="Conditional">
                        <ContentTemplate>
                            <asp:Button ID="btnLoad4" runat="server" Text="Load 4" style="display:none;"
                                OnClick="btnLoad4_Click" />
                            <asp:GridView ID="gvOrderLines" runat="server" CssClass="grid-view"
                                AutoGenerateColumns="true" AllowPaging="true" PageSize="50"
                                OnPageIndexChanging="gv4_PageIndexChanging" PagerStyle-CssClass="pager-style">
                            </asp:GridView>
                        </ContentTemplate>
                    </asp:UpdatePanel>
                </div>

                <div class="table-section">
                    <div class="table-title">Orders <span id="lblStatus5" style="font-size:1rem; color:red;"></span>
                    </div>
                    <asp:UpdatePanel ID="up5" runat="server" UpdateMode="Conditional">
                        <ContentTemplate>
                            <asp:Button ID="btnLoad5" runat="server" Text="Load 5" style="display:none;"
                                OnClick="btnLoad5_Click" />
                            <asp:GridView ID="gvOrders" runat="server" CssClass="grid-view" AutoGenerateColumns="true"
                                AllowPaging="true" PageSize="50" OnPageIndexChanging="gv5_PageIndexChanging"
                                PagerStyle-CssClass="pager-style">
                            </asp:GridView>
                        </ContentTemplate>
                    </asp:UpdatePanel>
                </div>
            </div>

            <script type="text/javascript">
                var currentTable = 0;
                var isSequentialLoading = false;

                function startLoading() {
                    // Bloquear botón
                    document.getElementById('btnStart').disabled = true;

                    // Reiniciar estados
                    for (var i = 1; i <= 5; i++) {
                        document.getElementById('lblStatus' + i).innerText = '';
                    }

                    currentTable = 1;
                    isSequentialLoading = true;
                    triggerNextTable();
                }

                function triggerNextTable() {
                    if (currentTable == 1) {
                        document.getElementById('lblStatus1').innerText = '(Cargando...)';
                        document.getElementById('<%= btnLoad1.ClientID %>').click();
                    } else if (currentTable == 2) {
                        document.getElementById('lblStatus1').innerText = '(Terminado)';
                        document.getElementById('lblStatus2').innerText = '(Cargando...)';
                        document.getElementById('<%= btnLoad2.ClientID %>').click();
                    } else if (currentTable == 3) {
                        document.getElementById('lblStatus2').innerText = '(Terminado)';
                        document.getElementById('lblStatus3').innerText = '(Cargando...)';
                        document.getElementById('<%= btnLoad3.ClientID %>').click();
                    } else if (currentTable == 4) {
                        document.getElementById('lblStatus3').innerText = '(Terminado)';
                        document.getElementById('lblStatus4').innerText = '(Cargando...)';
                        document.getElementById('<%= btnLoad4.ClientID %>').click();
                    } else if (currentTable == 5) {
                        document.getElementById('lblStatus4').innerText = '(Terminado)';
                        document.getElementById('lblStatus5').innerText = '(Cargando...)';
                        document.getElementById('<%= btnLoad5.ClientID %>').click();
                    } else {
                        document.getElementById('lblStatus5').innerText = '(Terminado)';
                        document.getElementById('btnStart').disabled = false;
                        isSequentialLoading = false;
                    }
                }

                // Engancharse al final del request de UpdatePanel
                Sys.WebForms.PageRequestManager.getInstance().add_endRequest(function (sender, args) {
                    if (isSequentialLoading) {
                        // Hace scroll a la tabla que acaba de terminar de cargar
                        var element = document.getElementById('lblStatus' + currentTable);
                        if (element) {
                            element.scrollIntoView({ behavior: 'smooth', block: 'center' });
                        }

                        if (currentTable <= 5) {
                            currentTable++;
                            triggerNextTable();
                        }
                    }
                });
            </script>
            </div>
        </form>
    </body>

    </html>
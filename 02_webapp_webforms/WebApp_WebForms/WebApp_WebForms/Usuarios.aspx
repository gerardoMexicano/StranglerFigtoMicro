<%@ Page Language="vb" AutoEventWireup="false" CodeBehind="Usuarios.aspx.vb" Inherits="WebApp_WebForms.Usuarios" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Usuarios - Checador</title>
    <!-- Leaflet CSS -->
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" crossorigin=""/>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #f4f7f6; margin: 0; padding: 0; display: flex; justify-content: center; align-items: center; min-height: 100vh; }
        .container { background: #ffffff; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.1); padding: 40px; width: 90%; max-width: 800px; text-align: center; animation: fadeIn 0.5s ease-out; }
        @keyframes fadeIn { from { opacity: 0; transform: translateY(-20px); } to { opacity: 1; transform: translateY(0); } }
        .icon { font-size: 3rem; margin-bottom: 10px; }
        h1 { color: #1a1a2e; font-size: 2rem; margin-bottom: 10px; font-weight: 600; }
        p { color: #555; font-size: 1.1rem; line-height: 1.6; margin-bottom: 20px; }
        .btn-back { display: inline-block; padding: 10px 20px; background: #16213e; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold; transition: all 0.3s ease; margin-top: 20px; }
        .btn-back:hover { background: #0f3460; transform: translateY(-2px); box-shadow: 0 4px 10px rgba(0,0,0,0.15); text-decoration: none; }
        
        .btn-checkin { display: inline-flex; align-items: center; gap: 8px; padding: 12px 24px; background: #4caf50; color: #ffffff; border: none; font-size: 1.1rem; border-radius: 8px; font-weight: bold; cursor: pointer; transition: all 0.3s ease; margin-bottom: 20px; }
        .btn-checkin:hover { background: #45a049; transform: translateY(-2px); box-shadow: 0 4px 10px rgba(76, 175, 80, 0.3); }
        .btn-checkin:disabled { background: #9e9e9e; cursor: not-allowed; transform: none; box-shadow: none; }

        #map { height: 400px; width: 100%; border-radius: 8px; border: 2px solid #eaeaea; margin-bottom: 20px; z-index: 1; }

        /* Custom Notification Toast */
        .toast-container { position: fixed; top: 20px; right: 20px; z-index: 9999; display: flex; flex-direction: column; gap: 10px; }
        .toast { background: #333; color: #fff; padding: 16px 24px; border-radius: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); display: flex; align-items: center; gap: 12px; font-weight: 500; transform: translateX(120%); transition: transform 0.3s cubic-bezier(0.68, -0.55, 0.265, 1.55); }
        .toast.show { transform: translateX(0); }
        .toast.success { background: #4caf50; }
        .toast.error { background: #f44336; }
    </style>
</head>
<body>
    <div class="toast-container" id="toastContainer"></div>

    <form id="form1" runat="server">
        <div class="container">
            <div class="icon">📍</div>
            <h1>Checador de Asistencia</h1>
            <p>Registra tu ubicación actual para confirmar tu asistencia.</p>

            <button type="button" id="btnCheckin" class="btn-checkin">
                <span>🎯</span> Obtener mi ubicación
            </button>

            <div id="map"></div>

            <a href="Menu.aspx" class="btn-back">⬅ Volver al Menú Principal</a>
        </div>
    </form>

    <!-- Leaflet JS -->
    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js" crossorigin=""></script>
    <script>
        document.addEventListener('DOMContentLoaded', function() {
            // Inicializar el mapa, centrado en México por defecto
            var map = L.map('map').setView([23.6345, -102.5528], 5);
            var currentMarker = null;

            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; OpenStreetMap contributors',
                maxZoom: 19
            }).addTo(map);

            const btnCheckin = document.getElementById('btnCheckin');

            btnCheckin.addEventListener('click', function() {
                if (!navigator.geolocation) {
                    showToast('Tu navegador no soporta geolocalización', 'error');
                    return;
                }

                btnCheckin.disabled = true;
                btnCheckin.innerHTML = '<span>⏳</span> Obteniendo ubicación...';

                navigator.geolocation.getCurrentPosition(
                    function(position) {
                        const lat = position.coords.latitude;
                        const lng = position.coords.longitude;
                        const accuracy = position.coords.accuracy;

                        // Centrar mapa
                        map.setView([lat, lng], 16);

                        // Remover marcador anterior si existe
                        if (currentMarker) {
                            map.removeLayer(currentMarker);
                        }

                        // Añadir nuevo marcador
                        currentMarker = L.marker([lat, lng]).addTo(map)
                            .bindPopup('Tu ubicación actual').openPopup();
                        
                        L.circle([lat, lng], { radius: accuracy, color: '#4caf50', fillOpacity: 0.1 }).addTo(map);

                        // Mostrar notificación Toast
                        showToast('ubicacion actual'+lat+''+lng, 'success');

                        // Enviar push notification nativa
                        sendNativePushNotification();

                        // Restaurar botón
                        btnCheckin.disabled = false;
                        btnCheckin.innerHTML = '<span>🎯</span> Actualizar mi ubicación';
                    },
                    function(error) {
                        console.error(error);
                        let msg = 'Error al obtener la ubicación';
                        if(error.code === 1) msg = 'Permiso de ubicación denegado';
                        showToast(msg, 'error');
                        
                        btnCheckin.disabled = false;
                        btnCheckin.innerHTML = '<span>🎯</span> Intentar nuevamente';
                    },
                    { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 }
                );
            });

            // Función para Push Notifications nativas del navegador
            function sendNativePushNotification() {
                if (!("Notification" in window)) {
                    console.log("Este navegador no soporta notificaciones de escritorio");
                    return;
                }

                if (Notification.permission === "granted") {
                    new Notification("Checador", {
                        body: "ubicacion actual",
                        icon: "https://cdn-icons-png.flaticon.com/512/684/684908.png"
                    });
                } else if (Notification.permission !== "denied") {
                    Notification.requestPermission().then(function (permission) {
                        if (permission === "granted") {
                            new Notification("Checador", {
                                body: "ubicacion actual",
                                icon: "https://cdn-icons-png.flaticon.com/512/684/684908.png"
                            });
                        }
                    });
                }
            }

            // Función para Toasts (UI Notifications)
            function showToast(message, type = 'success') {
                const container = document.getElementById('toastContainer');
                const toast = document.createElement('div');
                toast.className = `toast ${type}`;
                
                const icon = type === 'success' ? '✅' : '❌';
                toast.innerHTML = `<span>${icon}</span> <span>${message}</span>`;
                
                container.appendChild(toast);
                
                setTimeout(() => toast.classList.add('show'), 10);
                
                setTimeout(() => {
                    toast.classList.remove('show');
                    setTimeout(() => toast.remove(), 300);
                }, 4000);
            }
            
            // Pedir permisos de notificación al cargar (opcional)
            if ("Notification" in window && Notification.permission === "default") {
                Notification.requestPermission();
            }
        });
    </script>
</body>
</html>

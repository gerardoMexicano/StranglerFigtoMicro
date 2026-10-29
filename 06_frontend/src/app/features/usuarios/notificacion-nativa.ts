import { Injectable } from '@angular/core';

/**
 * La notificación de escritorio que mandaba Usuarios.aspx al ubicar al usuario. El permiso se
 * pide al registrar, no al abrir la página: el navegador lo pide por un clic del usuario.
 */
@Injectable({ providedIn: 'root' })
export class NotificacionNativa {
  async avisar(cuerpo: string): Promise<void> {
    if (!('Notification' in window) || Notification.permission === 'denied') {
      return;
    }

    const permiso =
      Notification.permission === 'granted' ? 'granted' : await Notification.requestPermission();
    if (permiso === 'granted') {
      new Notification('Checador', { body: cuerpo });
    }
  }
}

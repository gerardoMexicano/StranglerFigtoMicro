import { Injectable } from '@angular/core';

export interface Ubicacion {
  latitud: number;
  longitud: number;
  /** Radio en metros en el que está la ubicación real */
  precisionMetros: number;
}

export class GeolocalizacionError extends Error {
  override name = 'GeolocalizacionError';
}

/** Mismas opciones que Usuarios.aspx: GPS si lo hay, 10 s de espera y nada de caché. */
const OPCIONES: PositionOptions = { enableHighAccuracy: true, timeout: 10_000, maximumAge: 0 };

/**
 * navigator.geolocation como Promise. Es un servicio para que las pruebas no dependan del
 * navegador; los mensajes son los de la página WebForms.
 */
@Injectable({ providedIn: 'root' })
export class Geolocalizacion {
  obtener(): Promise<Ubicacion> {
    if (!('geolocation' in navigator)) {
      return Promise.reject(new GeolocalizacionError('Tu navegador no soporta geolocalización'));
    }

    return new Promise((resolve, reject) =>
      navigator.geolocation.getCurrentPosition(
        ({ coords }) =>
          resolve({
            latitud: coords.latitude,
            longitud: coords.longitude,
            precisionMetros: coords.accuracy,
          }),
        (error) => reject(new GeolocalizacionError(mensajeDeError(error))),
        OPCIONES,
      ),
    );
  }
}

export function mensajeDeError(error: GeolocationPositionError): string {
  switch (error.code) {
    case error.PERMISSION_DENIED:
      return 'Permiso de ubicación denegado';
    case error.TIMEOUT:
      return 'Se agotó el tiempo para obtener la ubicación';
    default:
      return 'Error al obtener la ubicación';
  }
}

import { Geolocalizacion, GeolocalizacionError, mensajeDeError } from './geolocalizacion';

const CODIGOS = { PERMISSION_DENIED: 1, POSITION_UNAVAILABLE: 2, TIMEOUT: 3 };

function errorDeGeolocalizacion(code: number): GeolocationPositionError {
  return { code, message: '', ...CODIGOS } as GeolocationPositionError;
}

describe('Geolocalizacion', () => {
  const original = Object.getOwnPropertyDescriptor(navigator, 'geolocation');

  function simular(getCurrentPosition: Geolocation['getCurrentPosition']) {
    Object.defineProperty(navigator, 'geolocation', {
      configurable: true,
      value: { getCurrentPosition },
    });
  }

  afterEach(() => {
    if (original) {
      Object.defineProperty(navigator, 'geolocation', original);
    } else {
      delete (navigator as { geolocation?: Geolocation }).geolocation;
    }
  });

  it('devuelve latitud, longitud y precisión con las opciones de Usuarios.aspx', async () => {
    let opciones: PositionOptions | undefined;
    simular((exito, _error, o) => {
      opciones = o;
      exito({
        coords: { latitude: 19.43, longitude: -99.13, accuracy: 15 },
      } as GeolocationPosition);
    });

    await expect(new Geolocalizacion().obtener()).resolves.toEqual({
      latitud: 19.43,
      longitud: -99.13,
      precisionMetros: 15,
    });
    expect(opciones).toEqual({ enableHighAccuracy: true, timeout: 10_000, maximumAge: 0 });
  });

  it('rechaza con el mensaje de la página WebForms si se niega el permiso', async () => {
    simular((_exito, error) => error?.(errorDeGeolocalizacion(1)));

    const resultado = new Geolocalizacion().obtener();

    await expect(resultado).rejects.toBeInstanceOf(GeolocalizacionError);
    await expect(resultado).rejects.toThrow('Permiso de ubicación denegado');
  });

  // jsdom no trae navigator.geolocation: es justo un navegador sin soporte
  it('sin soporte de geolocalización avisa en lugar de fallar', async () => {
    expect('geolocation' in navigator).toBe(false);

    await expect(new Geolocalizacion().obtener()).rejects.toThrow(
      'Tu navegador no soporta geolocalización',
    );
  });
});

describe('mensajeDeError', () => {
  it.each([
    [1, 'Permiso de ubicación denegado'],
    [2, 'Error al obtener la ubicación'],
    [3, 'Se agotó el tiempo para obtener la ubicación'],
  ])('código %i → %s', (codigo, mensaje) => {
    expect(mensajeDeError(errorDeGeolocalizacion(codigo))).toBe(mensaje);
  });
});

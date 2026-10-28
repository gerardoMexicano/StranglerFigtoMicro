import { AppConfigError, loadAppConfig, parseAppConfig } from './app-config';

const completo = {
  oidc: {
    authority: 'http://192.168.50.10/auth/realms/laboratorio',
    clientId: 'frontend-spa',
    scope: 'openid profile email',
  },
  apiBaseUrl: '/api',
  apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
};

function mensajeDe(fn: () => unknown): string {
  try {
    fn();
  } catch (e) {
    expect(e).toBeInstanceOf(AppConfigError);
    return (e as Error).message;
  }
  throw new Error('se esperaba AppConfigError');
}

describe('parseAppConfig', () => {
  it('acepta una configuración completa', () => {
    expect(parseAppConfig(completo)).toEqual(completo);
  });

  it('sin scope usa "openid profile"', () => {
    const { scope: _, ...oidc } = completo.oidc;

    expect(parseAppConfig({ ...completo, oidc }).oidc.scope).toBe('openid profile');
  });

  it('si faltan valores obligatorios avisa de todos a la vez', () => {
    const mensaje = mensajeDe(() =>
      parseAppConfig({ oidc: { clientId: 'frontend-spa' }, apps: { legacyUrl: '/legacy/Menu' } }),
    );

    expect(mensaje).toContain('oidc.authority');
    expect(mensaje).toContain('apiBaseUrl');
    expect(mensaje).toContain('apps.taxesUrl');
    expect(mensaje).not.toContain('oidc.clientId');
    expect(mensaje).not.toContain('apps.legacyUrl');
  });

  it('un valor vacío cuenta como faltante (envsubst deja "" si la variable no existe)', () => {
    const mensaje = mensajeDe(() =>
      parseAppConfig({ ...completo, oidc: { ...completo.oidc, authority: '  ' } }),
    );

    expect(mensaje).toContain('oidc.authority');
  });

  it.each([[null], ['texto'], [[]], [42]])('rechaza algo que no es un objeto (%j)', (raw) => {
    expect(() => parseAppConfig(raw)).toThrow(AppConfigError);
  });
});

describe('loadAppConfig', () => {
  const respuesta = (status: number, cuerpo: string) => new Response(cuerpo, { status });

  it('lee config.json sin caché y lo valida', async () => {
    const fetchFn = vi.fn().mockResolvedValue(respuesta(200, JSON.stringify(completo)));

    await expect(loadAppConfig('/newapp/config.json', fetchFn)).resolves.toEqual(completo);
    expect(fetchFn).toHaveBeenCalledWith(
      '/newapp/config.json',
      expect.objectContaining({ cache: 'no-store' }),
    );
  });

  it('si el servidor no lo entrega, el error dice qué URL y qué estado', async () => {
    const fetchFn = vi.fn().mockResolvedValue(respuesta(404, 'Not Found'));

    const error = loadAppConfig('/newapp/config.json', fetchFn);

    await expect(error).rejects.toBeInstanceOf(AppConfigError);
    await expect(error).rejects.toThrow(/\/newapp\/config\.json.*404/);
  });

  it('si no es JSON válido, lo dice', async () => {
    const fetchFn = vi.fn().mockResolvedValue(respuesta(200, '<html>'));

    await expect(loadAppConfig('/newapp/config.json', fetchFn)).rejects.toThrow(AppConfigError);
  });

  it('si la red falla, también responde con AppConfigError', async () => {
    const fetchFn = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'));

    await expect(loadAppConfig('/newapp/config.json', fetchFn)).rejects.toThrow(AppConfigError);
  });

  it('si el JSON está incompleto, propaga el mensaje de parseAppConfig', async () => {
    const fetchFn = vi
      .fn()
      .mockResolvedValue(respuesta(200, JSON.stringify({ apiBaseUrl: '/api' })));

    await expect(loadAppConfig('/newapp/config.json', fetchFn)).rejects.toThrow(/oidc\.authority/);
  });
});

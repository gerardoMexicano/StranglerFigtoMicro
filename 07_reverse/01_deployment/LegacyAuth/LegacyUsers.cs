using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace LegacyAuth;

public sealed record UsuarioLegacy(int Id, string Username, string Rol, bool Activo);

/// <summary>
/// Acceso de solo lectura a Laboratory.dbo.usuarios (login app_lector).
/// </summary>
public sealed class LegacyUserRepository(string connectionString)
{
    public async Task<UsuarioLegacy?> BuscarAsync(string username, CancellationToken ct)
    {
        const string query = "SELECT id, username, rol, activo FROM dbo.usuarios WHERE username = @username";

        await using var conexion = new SqlConnection(connectionString);
        await using var comando = new SqlCommand(query, conexion);
        comando.Parameters.Add("@username", SqlDbType.NVarChar, 50).Value = username;

        await conexion.OpenAsync(ct);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        return await lector.ReadAsync(ct) ? LeerUsuario(lector) : null;
    }

    public async Task<IReadOnlyList<UsuarioLegacy>> ListarAsync(CancellationToken ct)
    {
        const string query = "SELECT id, username, rol, activo FROM dbo.usuarios ORDER BY id";

        await using var conexion = new SqlConnection(connectionString);
        await using var comando = new SqlCommand(query, conexion);

        await conexion.OpenAsync(ct);
        await using var lector = await comando.ExecuteReaderAsync(ct);

        var usuarios = new List<UsuarioLegacy>();
        while (await lector.ReadAsync(ct))
        {
            usuarios.Add(LeerUsuario(lector));
        }

        return usuarios;
    }

    /// <summary>
    /// Valida la contraseña con el mismo algoritmo que el legacy
    /// (ver sqlserver/db/03_usuarios_hash_expand.sql). Solo usuarios activos.
    /// </summary>
    public async Task<bool> ValidarPasswordAsync(string username, string password, CancellationToken ct)
    {
        const string query = "SELECT password_salt, password_hash FROM dbo.usuarios WHERE username = @username AND activo = 1";

        await using var conexion = new SqlConnection(connectionString);
        await using var comando = new SqlCommand(query, conexion);
        comando.Parameters.Add("@username", SqlDbType.NVarChar, 50).Value = username;

        await conexion.OpenAsync(ct);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        if (!await lector.ReadAsync(ct) || lector.IsDBNull(0) || lector.IsDBNull(1))
        {
            return false;
        }

        var salt = (byte[])lector[0];
        var hashGuardado = (byte[])lector[1];

        return CryptographicOperations.FixedTimeEquals(CalcularHash(salt, password), hashGuardado);
    }

    // SHA-512( salt + UTF-16LE(password) )
    private static byte[] CalcularHash(byte[] salt, string password) =>
        SHA512.HashData([.. salt, .. Encoding.Unicode.GetBytes(password)]);

    private static UsuarioLegacy LeerUsuario(SqlDataReader lector) =>
        new(lector.GetInt32(0), lector.GetString(1), lector.GetString(2), lector.GetBoolean(3));
}

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Assembler.RemoteTooling;

/// <summary>
/// Authenticates as a GitHub App installation so the daemon's issue comments / close / label calls come
/// from the app's bot identity (e.g. "Game Generator Bot [bot]") instead of the ambient <c>gh auth login</c>
/// user. It signs a short-lived JWT with the app's private key and exchanges it for an installation access
/// token (valid ~1h), caching the token and refreshing a few minutes before expiry.
///
/// The token is handed to <c>gh</c> via the <c>GH_TOKEN</c> environment variable, which gh prefers over its
/// stored login — so no call site changes shape, only which credential gh authenticates with.
///
/// Configured via three env vars (all required to enable it; if any is unset the daemon stays on the gh login):
/// <list type="bullet">
///   <item><c>ASSEMBLER_GH_APP_ID</c> — the app's numeric App ID</item>
///   <item><c>ASSEMBLER_GH_APP_INSTALLATION_ID</c> — the installation ID on the store repo's account</item>
///   <item><c>ASSEMBLER_GH_APP_KEY</c> — path to the app's private-key <c>.pem</c></item>
/// </list>
/// </summary>
public sealed class GitHubApp
{
	private readonly string _appId;
	private readonly string _installationId;
	private readonly RSA _key;
	private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.github.com/") };

	private readonly object _gate = new();
	private string? _token;
	private DateTimeOffset _expiresAt;

	private GitHubApp(string appId, string installationId, RSA key)
	{
		_appId = appId;
		_installationId = installationId;
		_key = key;
	}

	/// <summary>
	/// Build from the <c>ASSEMBLER_GH_APP_*</c> env vars, or return <c>null</c> if they aren't all set (the
	/// daemon then keeps using the ambient <c>gh</c> login). Throws if configured but the key file is
	/// missing or unreadable, so a half-configured app fails loudly at startup rather than silently falling back.
	/// </summary>
	public static GitHubApp? FromConfig()
	{
		var (appId, installationId, keyPath) =
			(Config.GitHubAppId, Config.GitHubAppInstallationId, Config.GitHubAppKeyPath);
		if (appId is null || installationId is null || keyPath is null)
		{
			return null;
		}

		if (!File.Exists(keyPath))
		{
			throw new AppException($"GitHub App private key not found: {keyPath} (ASSEMBLER_GH_APP_KEY)");
		}

		var rsa = RSA.Create();
		try
		{
			rsa.ImportFromPem(File.ReadAllText(keyPath));
		}
		catch (Exception ex)
		{
			rsa.Dispose();
			throw new AppException($"could not read GitHub App private key {keyPath}: {ex.Message}");
		}

		return new GitHubApp(appId, installationId, rsa);
	}

	/// <summary>
	/// A currently-valid installation access token, minting or refreshing one if the cached token is missing
	/// or within five minutes of expiry. Thread-safe: daemon workers comment concurrently.
	/// </summary>
	public string Token(DateTimeOffset now)
	{
		lock (_gate)
		{
			if (_token is not null && now < _expiresAt - TimeSpan.FromMinutes(5))
			{
				return _token;
			}

			var (token, expiresAt) = Mint(now);
			_token = token;
			_expiresAt = expiresAt;
			return token;
		}
	}

	private (string Token, DateTimeOffset ExpiresAt) Mint(DateTimeOffset now)
	{
		using var request = new HttpRequestMessage(
			HttpMethod.Post, $"app/installations/{_installationId}/access_tokens");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Jwt(now));
		request.Headers.Accept.ParseAdd("application/vnd.github+json");
		request.Headers.UserAgent.ParseAdd("assembler-generation-daemon");
		request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

		using var response = _http.Send(request);
		using var reader = new StreamReader(response.Content.ReadAsStream());
		var payload = reader.ReadToEnd();
		if (!response.IsSuccessStatusCode)
		{
			throw new AppException(
				$"minting GitHub App installation token failed ({(int)response.StatusCode}): {payload}");
		}

		using var doc = JsonDocument.Parse(payload);
		var token = doc.RootElement.GetProperty("token").GetString()
			?? throw new AppException("installation token response had no 'token'");
		var expiresAt = doc.RootElement.GetProperty("expires_at").GetDateTimeOffset();
		return (token, expiresAt);
	}

	// A GitHub App JWT: RS256 over "{header}.{payload}", iss = App ID, valid 9 minutes with iat backdated
	// 60s to tolerate clock skew between us and GitHub (GitHub caps the lifetime at 10 minutes).
	private string Jwt(DateTimeOffset now)
	{
		var iat = now.ToUnixTimeSeconds() - 60;
		var exp = now.ToUnixTimeSeconds() + 540;
		var header = Base64Url("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"u8);
		var payload = Base64Url(Encoding.UTF8.GetBytes($"{{\"iat\":{iat},\"exp\":{exp},\"iss\":\"{_appId}\"}}"));
		var signingInput = $"{header}.{payload}";
		var signature = _key.SignData(
			Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		return $"{signingInput}.{Base64Url(signature)}";
	}

	private static string Base64Url(ReadOnlySpan<byte> bytes) =>
		Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

using System;
using System.Collections;
using UnityEngine.Networking;

namespace Assembler.Remote
{
	/// <summary>
	/// Fetches text bodies (the manifest JSON, a descriptor's YAML) over HTTP. Coroutine-based on purpose:
	/// the project has no runtime async/await infrastructure, and <c>async void</c> on a MonoBehaviour is a
	/// known iOS pitfall, so the call is an <see cref="IEnumerator"/> the shelf drives with
	/// <c>StartCoroutine</c>. Failures are reported through an <c>onError</c> callback (see
	/// <see cref="RemoteError"/>) rather than thrown across the yield boundary. Parsing and caching are the
	/// caller's concern — this stays a thin transport.
	/// </summary>
	public sealed class RemoteGameClient
	{
		private readonly int _timeoutSeconds;

		public RemoteGameClient(int timeoutSeconds = 15) => _timeoutSeconds = timeoutSeconds;

		/// <summary>GET a URL and hand the body to <paramref name="onText"/> on success, or a
		/// <see cref="RemoteError"/> to <paramref name="onError"/> on any transport failure.</summary>
		public IEnumerator FetchText(string url, Action<string> onText, Action<RemoteError> onError)
		{
			using var request = UnityWebRequest.Get(url);
			request.timeout = _timeoutSeconds;

			yield return request.SendWebRequest();

			switch (request.result)
			{
				case UnityWebRequest.Result.Success:
					onText(request.downloadHandler.text);
					break;

				case UnityWebRequest.Result.ProtocolError:
					onError(new RemoteError(RemoteErrorKind.Http, request.responseCode, request.error));
					break;

				case UnityWebRequest.Result.DataProcessingError:
					onError(new RemoteError(RemoteErrorKind.Parse, request.responseCode, request.error));
					break;

				// UnityWebRequest reports a timed-out request as a ConnectionError; tease it apart by message
				// so the UI can say "timed out" vs "offline".
				case UnityWebRequest.Result.ConnectionError when IsTimeout(request.error):
					onError(new RemoteError(RemoteErrorKind.Timeout, 0, request.error));
					break;

				default:
					onError(new RemoteError(RemoteErrorKind.Offline, 0, request.error));
					break;
			}
		}

		private static bool IsTimeout(string? error) =>
			!string.IsNullOrEmpty(error) && error!.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
	}
}

namespace Assembler.Remote
{
	/// <summary>How a remote fetch failed. Lets the UI distinguish "you're offline" from "that game is
	/// gone (404)" from "the list was corrupt", and decide whether a Retry is worthwhile.</summary>
	public enum RemoteErrorKind
	{
		/// <summary>No connection / DNS / TLS failure — the request never reached a server.</summary>
		Offline,

		/// <summary>The server replied with a non-success status (<see cref="RemoteError.HttpStatus"/>).</summary>
		Http,

		/// <summary>The request exceeded its timeout.</summary>
		Timeout,

		/// <summary>The body arrived but could not be parsed into the expected shape.</summary>
		Parse,
	}

	/// <summary>
	/// A failed remote fetch, carried as a value rather than thrown — the coroutine-based client reports
	/// failures through an <c>onError</c> callback so callers never have to wrap a <c>yield</c> in try/catch.
	/// </summary>
	public sealed record RemoteError(RemoteErrorKind Kind, long HttpStatus, string Message)
	{
		public override string ToString() =>
			Kind == RemoteErrorKind.Http ? $"{Kind} {HttpStatus}: {Message}" : $"{Kind}: {Message}";
	}
}

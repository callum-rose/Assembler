namespace Assembler.Remote
{
	/// <summary>
	/// A failed remote fetch, carried as a value rather than thrown — the coroutine-based client reports
	/// failures through an <c>onError</c> callback so callers never have to wrap a <c>yield</c> in try/catch.
	/// A closed discriminated union: each case carries only the data relevant to it (e.g. an HTTP status
	/// exists only on <see cref="Http"/>). Consumers <c>switch</c> on the concrete case.
	/// </summary>
	public abstract record RemoteError(string Message)
	{
		/// <summary>No connection / DNS / TLS failure — the request never reached a server.</summary>
		public sealed record Offline(string Message) : RemoteError(Message);

		/// <summary>The request exceeded its timeout.</summary>
		public sealed record Timeout(string Message) : RemoteError(Message);

		/// <summary>The server replied with a non-success status.</summary>
		public sealed record Http(long Status, string Message) : RemoteError(Message);

		/// <summary>The body arrived but could not be processed into the expected shape.</summary>
		public sealed record Parse(string Message) : RemoteError(Message);
	}
}

using System.Collections.Generic;

namespace Assembler.Deserialisation.Dtos
{
	public sealed record LocalisationDto
	{
		public string? DefaultLocale { get; init; }

		/// <summary>locale -&gt; (key -&gt; template string)</summary>
		public Dictionary<string, Dictionary<string, string>>? Locales { get; init; }
	}
}

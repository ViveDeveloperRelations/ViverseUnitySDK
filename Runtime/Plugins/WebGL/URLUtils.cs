using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ViverseWebGLAPI
{
	public static class URLUtils
	{
		public class URLParts
		{
			public string Protocol { get; set; }
			public string Hostname { get; set; }
			public string Port { get; set; }
			public string Pathname { get; set; }
			public string Search { get; set; }
			public string Fragment { get; set; }
			public Dictionary<string, string> Parameters { get; set; }

			public string GetFullHostname()
			{
				return string.IsNullOrEmpty(Port) ? Hostname : $"{Hostname}:{Port}";
			}

			public string ReconstructURL()
			{
				var url = $"{Protocol}://{GetFullHostname()}{Pathname}";

				// Add search parameters
				if (Parameters?.Any() == true)
				{
					url += (Pathname.EndsWith("?") ? "" : "?") + string.Join("&", Parameters.Select(p =>
						$"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
				}

				// Add fragment
				if (!string.IsNullOrEmpty(Fragment))
				{
					url += "#" + Fragment;
				}

				return url;
			}

			public override string ToString() =>
				$"Protocol: {Protocol}, Hostname: {Hostname}, Pathname: {Pathname}, Search: {Search}, Fragment: {Fragment}, Parameters: {string.Join(", ", Parameters.Select(p => $"{p.Key}={p.Value}"))} Reconstructed url: {ReconstructURL()}";
		}

		public static URLParts ParseURL(string url)
		{
			try
			{
				var uri = new Uri(url);
				var parts = new URLParts
				{
					Protocol = uri.Scheme,
					Hostname = uri.Host,
					Port = uri.Port != 80 && uri.Port != 443
						? uri.Port.ToString()
						: "", //TODO: check to see if port is correct, in theory you could run ssl on 80 and http on 443, even though that would be non-standard, ideally we'd leave it off if it's not specified in the url
					Pathname = uri.AbsolutePath,
					Search = uri.Query,
					Fragment = uri.Fragment.TrimStart('#'),
					Parameters = new Dictionary<string, string>()
				};

				if (!string.IsNullOrEmpty(uri.Query))
				{
					var parameters = uri.Query.TrimStart('?')
						.Split('&')
						.Select(p => p.Split('='))
						.Where(p => p.Length == 2)
						.Select(p => new
						{
							Key = Uri.UnescapeDataString(p[0]),
							Value = Uri.UnescapeDataString(p[1])
						});

					// Check for duplicate keys
					var duplicateKeys = parameters
						.GroupBy(p => p.Key)
						.Where(g => g.Count() > 1)
						.Select(g => new
						{
							Key = g.Key,
							Values = g.Select(p => p.Value).ToList()
						})
						.ToList();

					if (duplicateKeys.Any())
					{
						foreach (var dup in duplicateKeys)
						{
							Debug.LogError(
								$"URL parameter '{dup.Key}' has multiple values: {string.Join(", ", dup.Values)}. Using first value: {dup.Values.First()}");
						}
					}

					// Use first value for each key
					parts.Parameters = parameters
						.GroupBy(p => p.Key)
						.ToDictionary(
							g => g.Key,
							g => g.First().Value
						);
				}

				return parts;
			}
			catch (Exception e)
			{
				Debug.LogError($"Failed to parse URL: {e.Message}");
				return null;
			}
		}

		public static (string protocol, string hostname, string pathname, string fragment, Dictionary<string, string>
			parameters) GetURLParts(string inputURL)
		{
			var parts = ParseURL(inputURL);
			if (parts == null) return default;
			return (parts.Protocol, parts.Hostname, parts.Pathname, parts.Fragment, parts.Parameters);
		}
	}
}

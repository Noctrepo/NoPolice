using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace NoPolice;

public static class BlockListManager
{
    public static async Task RefreshBlockList(IDalamudPluginInterface pi, Configuration cfg, IPluginLog logger, CancellationTokenSource ct)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("NoPolice (+https://github.com/Noctrepo/NoPolice)");
            
            // Minimum of 15 minutes between checks
            var now = DateTime.UtcNow;
            if (cfg.LastFetchedUtc != default && (now - cfg.LastFetchedUtc) < TimeSpan.FromMinutes(15))
            {
                logger.Info("Skipping list version check");
                return;
            }

            var metaUrl = $"https://api.github.com/repos/Noctrepo/NoPolice/contents/NoPolice-data/blocklist.txt?ref=master";
            using var metaResponse = await http.GetAsync(metaUrl, ct.Token).ConfigureAwait(false);

            if (!metaResponse.IsSuccessStatusCode)
            {
                logger.Info($"Github responded with: {await metaResponse.Content.ReadAsStringAsync()}");
                return;
            }
            var meta = await metaResponse.Content.ReadFromJsonAsync<BlocklistMetaData>(ct.Token);
            
            if (meta is null || string.IsNullOrWhiteSpace(meta.sha)) return;
            
            // Cached sha is current: return cached names
            if (!string.IsNullOrEmpty(cfg.CachedSha) && cfg.CachedSha == meta.sha)
            {
                cfg.LastFetchedUtc = DateTime.UtcNow;
                pi.SavePluginConfig(cfg);
                
                logger.Info($"Using cached blocklist.txt with sha {meta.sha}");
                return;
            }
            
            var rawUrl = !string.IsNullOrWhiteSpace(meta.download_url) ? meta.download_url : $"https://raw.githubusercontent.com/Noctrepo/NoPolice/master/NoPolice-data/blocklist.txt";

            using var listResp = await http.GetAsync(rawUrl, ct.Token).ConfigureAwait(false);
            if (!listResp.IsSuccessStatusCode) return;

            var listText = (await listResp.Content.ReadAsStringAsync(ct.Token).ConfigureAwait(false)).Split("\n");

            var returnedNames = listText.Where(bannedListLine => !bannedListLine.StartsWith("#")).ToHashSet();
            if (returnedNames.Count == 0) return;

            cfg.BlocklistNames = returnedNames.ToHashSet();
            cfg.CachedSha = meta.sha;
            cfg.LastFetchedUtc = DateTime.UtcNow;
            pi.SavePluginConfig(cfg);
            logger.Info($"Cached blocklist.txt with sha {meta.sha}");
        }
        catch(Exception e)
        {
            logger.Info(e.Message);
        }
    }

    private sealed class BlocklistMetaData
    {
        public string? sha { get; set; }
        public string? download_url { get; set; }
    }
}
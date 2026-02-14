using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TmdlStudio.Models;

namespace TmdlStudio.Services
{
    /// <summary>
    /// Persistent cache for CLI authentication state.
    /// Stores non-secret auth details in ~/.timdle/auth.json.
    /// </summary>
    public static class TokenCacheService
    {
        private const string CacheDirectoryName = ".timdle";
        private const string CacheFileName = "auth.json";
        private const string LogicalMapFileName = "logical-id-map.json";

        /// <summary>
        /// Gets the absolute path to the cache file.
        /// </summary>
        public static string GetCacheFilePath()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, CacheDirectoryName, CacheFileName);
        }

        /// <summary>
        /// Gets the logical-id mapping cache file path.
        /// </summary>
        public static string GetLogicalMapFilePath()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, CacheDirectoryName, LogicalMapFileName);
        }

        /// <summary>
        /// Loads cached authentication config.
        /// </summary>
        public static AuthConfig Load()
        {
            var filePath = GetCacheFilePath();
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<AuthConfig>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Saves authentication config to cache.
        /// Service principal secrets are intentionally not persisted.
        /// </summary>
        public static void Save(AuthConfig config)
        {
            if (config == null)
            {
                return;
            }

            var safeConfig = new AuthConfig
            {
                Mode = config.Mode,
                WorkspaceUrl = config.WorkspaceUrl,
                AccessToken = config.AccessToken,
                AccessTokenExpiresOn = config.AccessTokenExpiresOn,
                AccountUsername = config.AccountUsername,
                ModelName = config.ModelName,
                PreviousModelName = config.PreviousModelName,
                ClientId = config.ClientId,
                TenantId = config.TenantId,
                ClientSecret = null
            };

            var filePath = GetCacheFilePath();
            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(safeConfig, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            File.WriteAllText(filePath, json);

            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }
            catch
            {
                // Ignore on unsupported platforms.
            }
        }

        /// <summary>
        /// Gets cached Fabric item id by workspace/logicalId pair.
        /// </summary>
        public static string GetMappedItemId(string workspaceId, string logicalId)
        {
            var map = LoadLogicalMap();
            if (map == null)
            {
                return null;
            }

            var key = BuildLogicalMapKey(workspaceId, logicalId);
            return map.TryGetValue(key, out var itemId) ? itemId : null;
        }

        /// <summary>
        /// Stores workspace/logicalId to Fabric item id mapping.
        /// </summary>
        public static void SetMappedItemId(string workspaceId, string logicalId, string itemId)
        {
            if (string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(logicalId) || string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            var map = LoadLogicalMap() ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            map[BuildLogicalMapKey(workspaceId, logicalId)] = itemId;
            SaveLogicalMap(map);
        }

        private static string BuildLogicalMapKey(string workspaceId, string logicalId)
        {
            return $"{workspaceId}:{logicalId}";
        }

        private static Dictionary<string, string> LoadLogicalMap()
        {
            var filePath = GetLogicalMapFilePath();
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void SaveLogicalMap(Dictionary<string, string> map)
        {
            var filePath = GetLogicalMapFilePath();
            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(map, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(filePath, json);

            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }
            catch
            {
                // Ignore on unsupported platforms.
            }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.AnalysisServices.Tabular.Tmdl;
using TmdlStudio.Models;
using STJ = System.Text.Json;

namespace TmdlStudio.Services
{
    /// <summary>
    /// Service for deploying TMDL models using Microsoft Fabric REST API.
    /// Cross-platform alternative to XMLA that works on Windows, macOS, and Linux.
    /// </summary>
    public class FabricApiService
    {
        private const string FabricApiBase = "https://api.fabric.microsoft.com/v1";

        private class WorkspaceItem
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public string Type { get; set; }
        }

        /// <summary>
        /// Deploys a TMDL model to a Fabric workspace by creating/updating a semantic model item.
        /// </summary>
        public static async Task<DeployResult> DeployAsync(string tmdlPath, AuthConfig authConfig)
        {
            try
            {
                // Extract workspace ID from URL
                string workspaceId = ExtractWorkspaceId(authConfig.WorkspaceUrl);
                if (string.IsNullOrEmpty(workspaceId))
                {
                    return DeployResult.Error("Could not extract workspace ID from URL. Expected format: https://api.fabric.microsoft.com/v1/workspaces/{workspaceId} or similar");
                }

                // Load the TMDL model
                var database = TmdlSerializer.DeserializeDatabaseFromFolder(tmdlPath);
                string platformName = TryReadDisplayNameFromPlatform(tmdlPath);
                string platformLogicalId = TryReadLogicalIdFromPlatform(tmdlPath);
                string databaseName = database?.Name;
                string itemName = ResolveItemName(tmdlPath, authConfig?.ModelName, databaseName, platformName);
                authConfig.ModelName = itemName;

                var semanticModels = await ListItemsByTypeAsync(workspaceId, "SemanticModel", authConfig.AccessToken);

                // Prefer identity by logicalId when available.
                string existingItemId = null;
                string existingDisplayName = null;
                bool renamed = false;

                if (!string.IsNullOrWhiteSpace(platformLogicalId))
                {
                    // Fast path: use cached mapping first, then verify it still exists in the workspace.
                    var cachedItemId = TokenCacheService.GetMappedItemId(workspaceId, platformLogicalId);
                    if (!string.IsNullOrWhiteSpace(cachedItemId) &&
                        semanticModels.Any(item => string.Equals(item.Id, cachedItemId, StringComparison.OrdinalIgnoreCase)))
                    {
                        existingItemId = cachedItemId;
                        existingDisplayName = semanticModels
                            .FirstOrDefault(item => string.Equals(item.Id, cachedItemId, StringComparison.OrdinalIgnoreCase))
                            ?.DisplayName;
                    }

                    // Deterministic mapping from .platform logicalId to Fabric item id.
                    if (string.IsNullOrWhiteSpace(existingItemId))
                    {
                        var mappedItemId = TryMapLogicalIdToItemId(platformLogicalId);
                        if (!string.IsNullOrWhiteSpace(mappedItemId))
                        {
                            var mappedItem = semanticModels
                                .FirstOrDefault(item => string.Equals(item.Id, mappedItemId, StringComparison.OrdinalIgnoreCase));

                            if (mappedItem != null)
                            {
                                existingItemId = mappedItem.Id;
                                existingDisplayName = mappedItem.DisplayName;
                                TokenCacheService.SetMappedItemId(workspaceId, platformLogicalId, existingItemId);
                            }
                        }
                    }

                    // If caller requested another name, keep same model identity but rename display name.
                    if (!string.IsNullOrWhiteSpace(existingItemId) &&
                        !string.IsNullOrWhiteSpace(itemName) &&
                        !string.Equals(existingDisplayName, itemName, StringComparison.OrdinalIgnoreCase))
                    {
                        var renameResult = await RenameItemAsync(workspaceId, existingItemId, itemName, authConfig.AccessToken);
                        if (!renameResult.success)
                        {
                            return DeployResult.Error($"Found semantic model by logicalId '{platformLogicalId}' but failed to rename it to '{itemName}': {renameResult.error}");
                        }

                        authConfig.PreviousModelName = existingDisplayName;
                        renamed = true;
                    }
                }

                // Fallback by name if logicalId lookup didn't find an existing item.
                if (string.IsNullOrWhiteSpace(existingItemId))
                {
                    var desiredNameMatch = semanticModels
                        .FirstOrDefault(item => string.Equals(item.DisplayName, itemName, StringComparison.OrdinalIgnoreCase));
                    if (desiredNameMatch != null)
                    {
                        existingItemId = desiredNameMatch.Id;
                        existingDisplayName = desiredNameMatch.DisplayName;
                        if (!string.IsNullOrWhiteSpace(platformLogicalId))
                        {
                            TokenCacheService.SetMappedItemId(workspaceId, platformLogicalId, existingItemId);
                        }
                    }
                }

                // If not found by desired name, try fallback candidates and rename.
                if (string.IsNullOrEmpty(existingItemId))
                {
                    var fallbackNames = new[] { authConfig?.PreviousModelName, platformName, databaseName };
                    foreach (var fallbackName in fallbackNames)
                    {
                        if (string.IsNullOrWhiteSpace(fallbackName) ||
                            string.Equals(fallbackName, itemName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var fallbackItem = semanticModels
                            .FirstOrDefault(item => string.Equals(item.DisplayName, fallbackName, StringComparison.OrdinalIgnoreCase));
                        if (fallbackItem != null)
                        {
                            var renameResult = await RenameItemAsync(workspaceId, fallbackItem.Id, itemName, authConfig.AccessToken);
                            if (!renameResult.success)
                            {
                                return DeployResult.Error($"Found existing semantic model '{fallbackName}' but failed to rename it to '{itemName}': {renameResult.error}");
                            }

                            existingItemId = fallbackItem.Id;
                            existingDisplayName = fallbackName;
                            authConfig.PreviousModelName = fallbackName;
                            if (!string.IsNullOrWhiteSpace(platformLogicalId))
                            {
                                TokenCacheService.SetMappedItemId(workspaceId, platformLogicalId, existingItemId);
                            }
                            renamed = true;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(existingItemId))
                {
                    // Create new semantic model
                    authConfig.PreviousModelName = itemName;
                    return await CreateSemanticModelAsync(workspaceId, itemName, tmdlPath, authConfig.AccessToken);
                }
                else
                {
                    // Update existing semantic model
                    if (!renamed && !string.IsNullOrWhiteSpace(existingDisplayName))
                    {
                        authConfig.PreviousModelName = existingDisplayName;
                    }

                    return await UpdateSemanticModelAsync(workspaceId, existingItemId, itemName, tmdlPath, authConfig.AccessToken, renamed);
                }
            }
            catch (Exception ex)
            {
                return DeployResult.Error($"Fabric API deployment failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Resolves semantic model display name with fallback order:
        /// explicit name (--name) -> .platform metadata.displayName -> database name -> interactive prompt.
        /// In CI, throws if name cannot be resolved without prompts.
        /// </summary>
        private static string ResolveItemName(string tmdlPath, string explicitName, string databaseName, string platformName = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitName))
            {
                return explicitName.Trim();
            }

            platformName ??= TryReadDisplayNameFromPlatform(tmdlPath);
            if (!string.IsNullOrWhiteSpace(platformName))
            {
                return platformName;
            }

            if (!string.IsNullOrWhiteSpace(databaseName))
            {
                return databaseName.Trim();
            }

            if (TokenService.IsCiEnvironment())
            {
                throw new InvalidOperationException(
                    "Could not resolve semantic model name. Provide --name, set metadata.displayName in .platform, or add a database name in database.tmdl.");
            }

            return ConsolePrompter.PromptRequired("Semantic model name");
        }

        /// <summary>
        /// Tries to read metadata.displayName from a .platform file at the model root.
        /// </summary>
        private static string TryReadDisplayNameFromPlatform(string tmdlPath)
        {
            try
            {
                var platformFilePath = Path.Combine(tmdlPath, ".platform");
                if (!File.Exists(platformFilePath))
                {
                    return null;
                }

                var json = File.ReadAllText(platformFilePath);
                using var document = JsonDocument.Parse(json);

                if (!document.RootElement.TryGetProperty("metadata", out var metadata))
                {
                    return null;
                }

                if (!metadata.TryGetProperty("displayName", out var displayNameElement))
                {
                    return null;
                }

                var displayName = displayNameElement.GetString();
                return string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tries to read config.logicalId from a .platform file at the model root.
        /// </summary>
        private static string TryReadLogicalIdFromPlatform(string tmdlPath)
        {
            try
            {
                var platformFilePath = Path.Combine(tmdlPath, ".platform");
                if (!File.Exists(platformFilePath))
                {
                    return null;
                }

                var json = File.ReadAllText(platformFilePath);
                using var document = JsonDocument.Parse(json);

                if (!document.RootElement.TryGetProperty("config", out var config))
                {
                    return null;
                }

                if (!config.TryGetProperty("logicalId", out var logicalIdElement))
                {
                    return null;
                }

                var logicalId = logicalIdElement.GetString();
                if (string.IsNullOrWhiteSpace(logicalId))
                {
                    return null;
                }

                return Guid.TryParse(logicalId, out _) ? logicalId : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Maps .platform logicalId to Fabric semantic model item id.
        /// Fabric item id uses reversed byte ordering of logicalId bytes.
        /// </summary>
        private static string TryMapLogicalIdToItemId(string logicalId)
        {
            if (!Guid.TryParse(logicalId, out var logicalGuid))
            {
                return null;
            }

            var bytes = logicalGuid.ToByteArray();
            Array.Reverse(bytes);
            var itemGuid = new Guid(bytes);
            return itemGuid.ToString();
        }

        /// <summary>
        /// Extracts workspace ID from various URL formats.
        /// </summary>
        private static string ExtractWorkspaceId(string workspaceUrl)
        {
            // Try to extract from different URL formats:
            // https://api.fabric.microsoft.com/v1/workspaces/{workspaceId}
            // https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}
            // powerbi://api.powerbi.com/v1.0/myorg/WorkspaceName (would need to resolve name to ID)

            if (string.IsNullOrEmpty(workspaceUrl))
                return null;

            // Handle GUID format in URL
            var parts = workspaceUrl.Split('/');
            foreach (var part in parts)
            {
                if (Guid.TryParse(part, out _))
                {
                    return part;
                }
            }

            return null;
        }

        /// <summary>
        /// Lists all workspace items for a given type, following Fabric pagination.
        /// </summary>
        private static async Task<System.Collections.Generic.List<WorkspaceItem>> ListItemsByTypeAsync(
            string workspaceId,
            string itemType,
            string accessToken)
        {
            var results = new System.Collections.Generic.List<WorkspaceItem>();

            using var httpClient = new HttpClient();
            string nextUrl = $"{FabricApiBase}/workspaces/{workspaceId}/items?type={Uri.EscapeDataString(itemType)}";

            while (!string.IsNullOrWhiteSpace(nextUrl))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, nextUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    break;
                }

                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("value", out var valueElement))
                {
                    foreach (var item in valueElement.EnumerateArray())
                    {
                        var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                        var displayName = item.TryGetProperty("displayName", out var nameElement) ? nameElement.GetString() : null;
                        var type = item.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            results.Add(new WorkspaceItem
                            {
                                Id = id,
                                DisplayName = displayName,
                                Type = type
                            });
                        }
                    }
                }

                if (doc.RootElement.TryGetProperty("continuationUri", out var continuationUriElement))
                {
                    var continuationUri = continuationUriElement.GetString();
                    nextUrl = string.IsNullOrWhiteSpace(continuationUri) ? null : continuationUri;
                }
                else if (doc.RootElement.TryGetProperty("continuationToken", out var continuationTokenElement))
                {
                    var continuationToken = continuationTokenElement.GetString();
                    nextUrl = string.IsNullOrWhiteSpace(continuationToken)
                        ? null
                        : $"{FabricApiBase}/workspaces/{workspaceId}/items?type={Uri.EscapeDataString(itemType)}&continuationToken={Uri.EscapeDataString(continuationToken)}";
                }
                else
                {
                    nextUrl = null;
                }
            }

            return results;
        }

        /// <summary>
        /// Gets the ID of an item by name and type.
        /// </summary>
        private static async Task<string> GetItemIdAsync(string workspaceId, string itemName, string itemType, string accessToken)
        {
            try
            {
                var items = await ListItemsByTypeAsync(workspaceId, itemType, accessToken);
                var match = items.FirstOrDefault(item => string.Equals(item.DisplayName, itemName, StringComparison.OrdinalIgnoreCase));
                return match?.Id;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a new semantic model item in the workspace.
        /// </summary>
        private static async Task<DeployResult> CreateSemanticModelAsync(
            string workspaceId, 
            string itemName, 
            string tmdlPath, 
            string accessToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                string url = $"{FabricApiBase}/workspaces/{workspaceId}/items";

                // Create the semantic model item
                var definitionParts = BuildDefinitionPayload(tmdlPath);
                var createPayload = new
                {
                    displayName = itemName,
                    type = "SemanticModel",
                    definition = new
                    {
                        parts = definitionParts
                    }
                };

                string jsonPayload = STJ.JsonSerializer.Serialize(createPayload);

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return DeployResult.Error($"Failed to create semantic model: {response.StatusCode} - {responseContent}");
                }

                // Parse the created item ID (response body can be null/empty on some API variants)
                string itemId = TryExtractItemId(responseContent);
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    // Fallback: locate the item by name after creation.
                    itemId = await GetItemIdAsync(workspaceId, itemName, "SemanticModel", accessToken);
                }

                if (string.IsNullOrWhiteSpace(itemId))
                {
                    // Definition may already be applied via create payload.
                    return DeployResult.Success("Successfully created semantic model");
                }

                // Some API versions require updateDefinition after create, others accept definition in create.
                // Keep update for compatibility.
                return await UpdateItemDefinitionAsync(workspaceId, itemId, tmdlPath, accessToken, true);
            }
            catch (Exception ex)
            {
                return DeployResult.Error($"Failed to create semantic model: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates an existing semantic model with new TMDL content.
        /// </summary>
        private static async Task<DeployResult> UpdateSemanticModelAsync(
            string workspaceId, 
            string itemId, 
            string itemName,
            string tmdlPath, 
            string accessToken,
            bool renamed)
        {
            return await UpdateItemDefinitionAsync(workspaceId, itemId, tmdlPath, accessToken, false, renamed);
        }

        /// <summary>
        /// Tries to extract item id from create-item response body.
        /// </summary>
        private static string TryExtractItemId(string responseContent)
        {
            if (string.IsNullOrWhiteSpace(responseContent))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(responseContent);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                if (!doc.RootElement.TryGetProperty("id", out var idElement))
                {
                    return null;
                }

                var id = idElement.GetString();
                return string.IsNullOrWhiteSpace(id) ? null : id;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Renames an existing semantic model item.
        /// </summary>
        private static async Task<(bool success, string error)> RenameItemAsync(
            string workspaceId,
            string itemId,
            string newName,
            string accessToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                string url = $"{FabricApiBase}/workspaces/{workspaceId}/items/{itemId}";

                var payload = new
                {
                    displayName = newName
                };

                using var request = new HttpRequestMessage(new HttpMethod("PATCH"), url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(STJ.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"{response.StatusCode} - {content}");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Updates the definition of a semantic model item with TMDL content.
        /// </summary>
        private static async Task<DeployResult> UpdateItemDefinitionAsync(
            string workspaceId, 
            string itemId, 
            string tmdlPath, 
            string accessToken,
            bool isNew,
            bool renamed = false)
        {
            try
            {
                using var httpClient = new HttpClient();
                string url = $"{FabricApiBase}/workspaces/{workspaceId}/items/{itemId}/updateDefinition";

                // Build definition payload with all TMDL files
                var definitionParts = BuildDefinitionPayload(tmdlPath);

                var updatePayload = new
                {
                    definition = new
                    {
                        parts = definitionParts
                    }
                };

                string jsonPayload = STJ.JsonSerializer.Serialize(updatePayload, new STJ.JsonSerializerOptions
                {
                    PropertyNamingPolicy = STJ.JsonNamingPolicy.CamelCase
                });

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == HttpStatusCode.Accepted)
                {
                    var lroResult = await WaitForOperationCompletionAsync(httpClient, response, accessToken);
                    if (!lroResult.success)
                    {
                        return DeployResult.Error($"Failed to update definition: {lroResult.error}");
                    }
                }
                else if (!response.IsSuccessStatusCode)
                {
                    return DeployResult.Error($"Failed to update definition: {response.StatusCode} - {responseContent}");
                }

                string message = isNew 
                    ? $"Successfully created semantic model"
                    : renamed
                        ? $"Successfully renamed and updated semantic model"
                        : $"Successfully updated semantic model";
                
                return DeployResult.Success(message);
            }
            catch (Exception ex)
            {
                return DeployResult.Error($"Failed to update definition: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads operation result body for APIs that may return long-running operation responses.
        /// </summary>
        private static async Task<string> ReadOperationResultBodyAsync(HttpClient httpClient, HttpResponseMessage response, string accessToken)
        {
            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                var lroResult = await WaitForOperationCompletionAsync(httpClient, response, accessToken);
                if (!lroResult.success)
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(lroResult.operationId))
                {
                    return null;
                }

                using var resultRequest = new HttpRequestMessage(HttpMethod.Get, $"{FabricApiBase}/operations/{lroResult.operationId}/result");
                resultRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var resultResponse = await httpClient.SendAsync(resultRequest);
                if (!resultResponse.IsSuccessStatusCode)
                {
                    return null;
                }

                return await resultResponse.Content.ReadAsStringAsync();
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Waits for Fabric long-running operation completion.
        /// </summary>
        private static async Task<(bool success, string operationId, string error)> WaitForOperationCompletionAsync(
            HttpClient httpClient,
            HttpResponseMessage acceptedResponse,
            string accessToken)
        {
            var operationId = acceptedResponse.Headers.TryGetValues("x-ms-operation-id", out var operationIdValues)
                ? operationIdValues.FirstOrDefault()
                : null;

            if (string.IsNullOrWhiteSpace(operationId) && acceptedResponse.Headers.Location != null)
            {
                var location = acceptedResponse.Headers.Location.ToString();
                operationId = TryExtractOperationId(location);
            }

            if (string.IsNullOrWhiteSpace(operationId))
            {
                return (false, null, "Operation accepted but operation id was not returned by API.");
            }

            var statusUrl = $"{FabricApiBase}/operations/{operationId}";
            for (int attempt = 0; attempt < 60; attempt++)
            {
                using var statusRequest = new HttpRequestMessage(HttpMethod.Get, statusUrl);
                statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var statusResponse = await httpClient.SendAsync(statusRequest);
                var statusContent = await statusResponse.Content.ReadAsStringAsync();

                if (!statusResponse.IsSuccessStatusCode)
                {
                    return (false, operationId, $"Failed to query operation state: {statusResponse.StatusCode} - {statusContent}");
                }

                using var statusDoc = JsonDocument.Parse(statusContent);
                var status = statusDoc.RootElement.TryGetProperty("status", out var statusElement)
                    ? statusElement.GetString()
                    : null;

                if (string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, operationId, null);
                }

                if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    var errorMessage = "Operation failed.";
                    if (statusDoc.RootElement.TryGetProperty("error", out var errorElement) &&
                        errorElement.TryGetProperty("message", out var messageElement))
                    {
                        errorMessage = messageElement.GetString();
                    }

                    return (false, operationId, errorMessage);
                }

                int retryDelaySeconds = 2;
                if (statusResponse.Headers.TryGetValues("Retry-After", out var retryAfterValues))
                {
                    var retryAfter = retryAfterValues.FirstOrDefault();
                    if (int.TryParse(retryAfter, out var parsed) && parsed > 0)
                    {
                        retryDelaySeconds = Math.Min(parsed, 10);
                    }
                }

                await Task.Delay(retryDelaySeconds * 1000);
            }

            return (false, operationId, "Timed out waiting for operation completion.");
        }

        private static string TryExtractOperationId(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            var marker = "/operations/";
            var markerIndex = location.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return null;
            }

            var tail = location[(markerIndex + marker.Length)..];
            var slashIndex = tail.IndexOf('/');
            var opId = slashIndex >= 0 ? tail[..slashIndex] : tail;
            return string.IsNullOrWhiteSpace(opId) ? null : opId;
        }

        /// <summary>
        /// Builds the definition payload from TMDL files.
        /// </summary>
        private static System.Collections.Generic.List<object> BuildDefinitionPayload(string folderPath)
        {
            var parts = new System.Collections.Generic.List<object>();
            
            // Add all TMDL files
            var tmdlFiles = Directory.GetFiles(folderPath, "*.tmdl", SearchOption.AllDirectories);
            
            foreach (var filePath in tmdlFiles)
            {
                if (!IsTextFile(filePath))
                {
                    Console.WriteLine($"Skipping binary file: {filePath}");
                    continue;
                }

                string relativePath = Path.GetRelativePath(folderPath, filePath).Replace("\\", "/");
                string fileContent = File.ReadAllText(filePath);
                byte[] contentBytes = Encoding.UTF8.GetBytes(fileContent);
                string base64Content = Convert.ToBase64String(contentBytes);
                
                parts.Add(new
                {
                    path = relativePath,
                    payload = base64Content,
                    payloadType = "InlineBase64"
                });
            }

            // Add other necessary files
            var otherExtensions = new[] { ".platform", ".pbism", ".json", ".xml", ".txt", ".md" };
            var otherFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".tmdl"))
                .Where(f => otherExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

            foreach (var filePath in otherFiles)
            {
                if (!IsTextFile(filePath))
                {
                    Console.WriteLine($"Skipping binary file: {filePath}");
                    continue;
                }

                string relativePath = Path.GetRelativePath(folderPath, filePath).Replace("\\", "/");
                string fileContent = File.ReadAllText(filePath);
                byte[] contentBytes = Encoding.UTF8.GetBytes(fileContent);
                string base64Content = Convert.ToBase64String(contentBytes);
                
                parts.Add(new
                {
                    path = relativePath,
                    payload = base64Content,
                    payloadType = "InlineBase64"
                });
            }

            return parts;
        }

        /// <summary>
        /// Checks if a file is a text file by looking for null bytes (which indicate binary content).
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <returns>True if the file appears to be a text file.</returns>
        private static bool IsTextFile(string filePath)
        {
            try
            {
                // Read first 8KB of file to check for null bytes
                byte[] buffer = new byte[8192];
                using (var stream = File.OpenRead(filePath))
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    for (int i = 0; i < bytesRead; i++)
                    {
                        if (buffer[i] == 0)
                        {
                            return false; // Null byte found, likely binary
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false; // If we can't read the file, skip it
            }
        }

    }
}

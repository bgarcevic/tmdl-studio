using System;
using System.IO;
using System.Linq;
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
                string itemName = database.Name;

                // Check if item already exists
                string existingItemId = await GetItemIdAsync(workspaceId, itemName, "SemanticModel", authConfig.AccessToken);

                if (string.IsNullOrEmpty(existingItemId))
                {
                    // Create new semantic model
                    return await CreateSemanticModelAsync(workspaceId, itemName, tmdlPath, authConfig.AccessToken);
                }
                else
                {
                    // Update existing semantic model
                    return await UpdateSemanticModelAsync(workspaceId, existingItemId, itemName, tmdlPath, authConfig.AccessToken);
                }
            }
            catch (Exception ex)
            {
                return DeployResult.Error($"Fabric API deployment failed: {ex.Message}");
            }
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
        /// Gets the ID of an item by name and type.
        /// </summary>
        private static async Task<string> GetItemIdAsync(string workspaceId, string itemName, string itemType, string accessToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                string url = $"{FabricApiBase}/workspaces/{workspaceId}/items";
                
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                using var doc = JsonDocument.Parse(content);
                var items = doc.RootElement.GetProperty("value");

                foreach (var item in items.EnumerateArray())
                {
                    string name = item.GetProperty("displayName").GetString();
                    string type = item.GetProperty("type").GetString();
                    
                    if (name == itemName && type == itemType)
                    {
                        return item.GetProperty("id").GetString();
                    }
                }

                return null;
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
                var createPayload = new
                {
                    displayName = itemName,
                    type = "SemanticModel"
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

                // Parse the created item ID
                using var doc = JsonDocument.Parse(responseContent);
                string itemId = doc.RootElement.GetProperty("id").GetString();

                // Now update the definition with TMDL content
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
            string accessToken)
        {
            return await UpdateItemDefinitionAsync(workspaceId, itemId, tmdlPath, accessToken, false);
        }

        /// <summary>
        /// Updates the definition of a semantic model item with TMDL content.
        /// </summary>
        private static async Task<DeployResult> UpdateItemDefinitionAsync(
            string workspaceId, 
            string itemId, 
            string tmdlPath, 
            string accessToken,
            bool isNew)
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

                if (response.IsSuccessStatusCode)
                {
                    string message = isNew 
                        ? $"Successfully created semantic model"
                        : $"Successfully updated semantic model";
                    
                    return DeployResult.Success(message, Array.Empty<DeployChange>());
                }
                else
                {
                    return DeployResult.Error($"Failed to update definition: {response.StatusCode} - {responseContent}");
                }
            }
            catch (Exception ex)
            {
                return DeployResult.Error($"Failed to update definition: {ex.Message}");
            }
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

        /// <summary>
        /// Tests connection to the Fabric API.
        /// </summary>
        public static async Task<bool> TestConnectionAsync(string workspaceUrl, string accessToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                string workspaceId = ExtractWorkspaceId(workspaceUrl);
                if (string.IsNullOrEmpty(workspaceId))
                    return false;

                string url = $"{FabricApiBase}/workspaces/{workspaceId}";
                
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lists all workspaces the user has access to.
        /// </summary>
        public static async Task<WorkspaceInfo[]> ListWorkspacesAsync(string accessToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                string url = $"{FabricApiBase}/workspaces";
                
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return Array.Empty<WorkspaceInfo>();
                }

                using var doc = JsonDocument.Parse(content);
                var workspaces = doc.RootElement.GetProperty("value");
                var result = new System.Collections.Generic.List<WorkspaceInfo>();

                foreach (var workspace in workspaces.EnumerateArray())
                {
                    result.Add(new WorkspaceInfo
                    {
                        Id = workspace.GetProperty("id").GetString(),
                        Name = workspace.GetProperty("displayName").GetString(),
                        Description = workspace.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                        Type = workspace.TryGetProperty("type", out var type) ? type.GetString() : "Workspace"
                    });
                }

                return result.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing workspaces: {ex.Message}");
                return Array.Empty<WorkspaceInfo>();
            }
        }
    }

    /// <summary>
    /// Information about a Fabric workspace.
    /// </summary>
    public class WorkspaceInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
    }
}

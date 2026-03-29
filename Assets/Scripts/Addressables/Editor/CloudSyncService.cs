using UnityEngine.Networking;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.IO;

public enum UploadResult { Success, Skipped, Failed }

public static class CloudSyncService
{
    private static string ACCESS_KEY => AddressablesBuilderKeys.ACCESS_KEY;
    private static string SECRET_KEY => AddressablesBuilderKeys.SECRET_KEY;
    
    private const string BUCKET = "data-2073";
    private const string REGION = "sfo3"; 

    // Now accepts a list of existing files to check for skipping
    public static async Task<UploadResult> UploadFile(string localPath, string remotePath, List<string> existingRemoteFiles)
    {
        string fileName = Path.GetFileName(localPath);
        
        // Metadata (json/hash) is NEVER skipped. Bundles are skipped if they exist.
        bool isMetadata = fileName.EndsWith(".json") || fileName.EndsWith(".hash");
        if (existingRemoteFiles.Contains(remotePath) && !isMetadata) 
        {
            return UploadResult.Skipped;
        }

        byte[] data = File.ReadAllBytes(localPath);
        string contentType = GetContentType(fileName);
        
        bool success = await PutObject(remotePath, data, contentType);
        return success ? UploadResult.Success : UploadResult.Failed;
    }

    public static Task<List<string>> ListObjects(string prefix)
    {
        // (Keep existing implementation)
        TaskCompletionSource<List<string>> tcs = new TaskCompletionSource<List<string>>();
        string date = DateTime.UtcNow.ToString("r");
        string resource = $"/{BUCKET}/";
        string signature = Sign($"GET\n\n\n{date}\n{resource}", SECRET_KEY);
        string url = $"https://{BUCKET}.{REGION}.digitaloceanspaces.com/?prefix={prefix}/";

        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Date", date);
        request.SetRequestHeader("Authorization", $"AWS {ACCESS_KEY}:{signature}");

        UnityWebRequestAsyncOperation op = request.SendWebRequest();
        op.completed += (_) => 
        {
            List<string> files = new List<string>();
            if (request.result == UnityWebRequest.Result.Success)
            {
                try {
                    XmlDocument xml = new XmlDocument();
                    xml.LoadXml(request.downloadHandler.text);
                    foreach (XmlNode node in xml.GetElementsByTagName("Contents"))
                    {
                        string key = node["Key"].InnerText;
                        if (!key.EndsWith("/")) files.Add(key);
                    }
                } catch {}
            }
            tcs.SetResult(files);
            request.Dispose();
        };
        return tcs.Task;
    }

    public static async Task<bool> DeleteObject(string path)
    {
        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        string date = DateTime.UtcNow.ToString("r");
        string resource = $"/{BUCKET}/{path}";
        string signature = Sign($"DELETE\n\n\n{date}\n{resource}", SECRET_KEY);
        string url = $"https://{BUCKET}.{REGION}.digitaloceanspaces.com/{path}";

        UnityWebRequest request = new UnityWebRequest(url, "DELETE");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Date", date);
        request.SetRequestHeader("Authorization", $"AWS {ACCESS_KEY}:{signature}");

        UnityWebRequestAsyncOperation op = request.SendWebRequest();
        op.completed += (_) => { tcs.SetResult(request.result == UnityWebRequest.Result.Success); request.Dispose(); };
        return await tcs.Task;
    }

    private static Task<bool> PutObject(string path, byte[] data, string contentType)
    {
        // (Keep existing implementation)
        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        string date = DateTime.UtcNow.ToString("r");
        string resource = $"/{BUCKET}/{path}";
        
        string cacheControl = "public, max-age=31536000"; 
        if (path.EndsWith(".hash") || path.EndsWith(".json") || path.EndsWith(".txt")) 
            cacheControl = "no-cache, no-store, must-revalidate";

        string signature = Sign($"PUT\n\n{contentType}\n{date}\nx-amz-acl:public-read\n{resource}", SECRET_KEY);
        string url = $"https://{BUCKET}.{REGION}.digitaloceanspaces.com/{path}";
    
        UnityWebRequest request = new UnityWebRequest(url, "PUT");
        request.uploadHandler = new UploadHandlerRaw(data);
        request.downloadHandler = new DownloadHandlerBuffer();
    
        request.SetRequestHeader("Date", date);
        request.SetRequestHeader("Content-Type", contentType);
        request.SetRequestHeader("x-amz-acl", "public-read");
        request.SetRequestHeader("Cache-Control", cacheControl); 
        request.SetRequestHeader("Authorization", $"AWS {ACCESS_KEY}:{signature}");

        UnityWebRequestAsyncOperation op = request.SendWebRequest();
        op.completed += (_) => { tcs.SetResult(request.result == UnityWebRequest.Result.Success); request.Dispose(); };
        return tcs.Task;
    }

    private static string Sign(string data, string key)
    {
        using (HMACSHA1 hmac = new HMACSHA1(Encoding.UTF8.GetBytes(key)))
        {
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
        }
    }

    private static string GetContentType(string fileName)
    {
        if (fileName.EndsWith(".bundle")) return "application/octet-stream";
        if (fileName.EndsWith(".json")) return "application/json";
        if (fileName.EndsWith(".hash") || fileName.EndsWith(".txt")) return "text/plain";
        return "application/octet-stream";
    }
}
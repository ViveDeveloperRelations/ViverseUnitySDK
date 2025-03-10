using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.IO;
using System.Net.Http;

public class HttpsServerTest
{
    private HttpListener listener;
    private const string ServerUrl = "https://localhost:3000/";
    private const string ExpectedResponse = "hello world";
    private Task serverTask;
    private CancellationTokenSource cancellationTokenSource;
    
    private async Task WaitForServerReady(int timeoutMs = 5000)
    {
        var start = DateTime.Now;
        while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
        {
            if (listener.IsListening)
            {
                try
                {
                    // Try to make a test connection to verify the server is ready
                    using (var testHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                    })
                    using (var testClient = new HttpClient(testHandler))
                    {
                        testClient.Timeout = TimeSpan.FromMilliseconds(500);
                        try
                        {
                            await testClient.GetAsync(ServerUrl);
                            return; // Server is ready
                        }
                        catch (HttpRequestException)
                        {
                            // Keep trying
                        }
                    }
                }
                catch
                {
                    // Keep trying
                }
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("Server failed to start within timeout period");
    }

    [Test]
    public async Task CanLoadCert()
    {
        string scriptPath = GetTestScriptPath();
        string certPath = Path.Combine(Path.GetDirectoryName(scriptPath), "certificate.pfx");
        
        if (!File.Exists(certPath))
        {
            throw new FileNotFoundException($"Certificate not found at {certPath}. Please ensure certificate.pfx is in the same directory as the test script.");
        }
        
        var certificate = new X509Certificate2(await File.ReadAllBytesAsync(certPath), "");
        return;
    }
    
    //[OneTimeSetUp]
    public async Task Setup()
    {
        Debug.Log("Starting server setup...");
        //SetupAsync().GetAwaiter().GetResult();
        cancellationTokenSource = new CancellationTokenSource();
        
        // Get the directory where the test script is located
        string scriptPath = GetTestScriptPath();
        string certPath = Path.Combine(Path.GetDirectoryName(scriptPath), "certificate.pfx");
        
        if (!File.Exists(certPath))
        {
            throw new FileNotFoundException($"Certificate not found at {certPath}. Please ensure certificate.pfx is in the same directory as the test script.");
        }
        
        var certificate = new X509Certificate2(File.ReadAllBytes(certPath), "");
        
        // Create and start the HTTPS server
        listener = new HttpListener();
        listener.Prefixes.Add(ServerUrl);
        
        // Add the certificate to the listener
        listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
        
        try 
        {
            Debug.Log("Starting listener...");
            listener.Start();
            Debug.Log("Listener started, waiting for server to be ready...");
            
            // Start listening for requests asynchronously
            serverTask = Task.Run(async () =>
            {
                try
                {
                    while (!cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var context = await listener.GetContextAsync();
                            var response = context.Response;

                            byte[] buffer = Encoding.UTF8.GetBytes(ExpectedResponse);
                            response.ContentLength64 = buffer.Length;
                            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                            response.Close();
                        }
                        catch (HttpListenerException)
                        {
                            if (cancellationTokenSource.Token.IsCancellationRequested)
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

            }, cancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start server: {e.Message}");
            throw;
        }

        // Wait for the server to be fully ready
        await WaitForServerReady();
        Debug.Log("Server is ready!");
    }

    //need to validate all the ones available for the test server
    //private static int[] validPortsForServer = { 5173, 3000, 8000, 50077, 80, 8080 }; // valid callback ports are defined as a part of settign up your app, so only a defined list of those will be valid for a callback from SSO. These are the ones defined in the test app.
    private static int[] validPortsForServer = { 5173, 3000 }; // valid callback ports are defined as a part of settign up your app, so only a defined list of those will be valid for a callback from SSO. These are the ones defined in the test app.
    
    [Test]
    public async Task TestHttpsServer()
    {
        // Configure HttpClient to accept all certificates for testing
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        using (var client = new HttpClient(handler))
        {
            try
            {
                // Make the HTTPS request
                var response = await client.GetStringAsync(ServerUrl);
                
                // Verify the response
                Assert.AreEqual(ExpectedResponse, response);
            }
            catch (Exception e)
            {
                Debug.LogError($"Request failed: {e.Message}");
                throw;
            }
        }
    }

    private string GetTestScriptPath()
    {
        // Get the current stack trace to find this test class's source file
        var stackTrace = new System.Diagnostics.StackTrace(true);
        
        foreach (var frame in stackTrace.GetFrames())
        {
            var fileName = frame.GetFileName();
            if (!string.IsNullOrEmpty(fileName) && fileName.EndsWith("HttpsServerTest.cs"))
            {
                return fileName;
            }
        }
        
        // Fallback to searching in the Assets folder if we can't find it in stack trace
        string[] guids = UnityEditor.AssetDatabase.FindAssets("HttpsServerTest t:Script");
        if (guids.Length > 0)
        {
            return UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
        }
        
        throw new Exception("Could not locate test script path.");
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        CleanupAsync().GetAwaiter().GetResult();
    }

    private async Task CleanupAsync()
    {
        // Cancel the server task
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
        }

        // Stop and clean up the listener
        if (listener != null && listener.IsListening)
        {
            listener.Stop();
            listener.Close();
        }

        // Wait for server task to complete
        if (serverTask != null)
        {
            try
            {
                await serverTask;
            }
            catch (OperationCanceledException)
            {
                // This is expected when we cancel the task
            }
        }

        cancellationTokenSource?.Dispose();
    }
}
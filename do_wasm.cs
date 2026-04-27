using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Net;

static void ForceDeleteDirectory(string path)
{
    // On windows, it's a bit annoying to delete directories containing read-only files, 
    // so we have to make sure to clear the read-only flag on all files before deleting.
    if (!Directory.Exists(path)) return;
    foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        File.SetAttributes(f, FileAttributes.Normal);
    Directory.Delete(path, true);
}

// get args flags for the script
bool doClean = false;
bool doServe = false;
foreach (String arg in args)
{
    switch (arg)
    {
        case "serve":
            doServe = true;
            break;
    }
}


if (doClean)
{
    Console.WriteLine("Cleaning up...");
    ForceDeleteDirectory("FNAWasm");
    Console.WriteLine("Finished cleaning up");
}

String branch = "26.04";
var wasmClone = new Process();
wasmClone.StartInfo.FileName = "git";
wasmClone.StartInfo.Arguments = $"clone https://github.com/FNA-XNA/FNA --recursive -b {branch} FNAWasm";
wasmClone.Start();
wasmClone.WaitForExit();
Console.WriteLine("Finished cloning FNA");
Console.WriteLine("Now applying patches...");
var wasmPatch = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "git",
        Arguments = "apply ../FNAWasm.patch",
        WorkingDirectory = "FNAWasm",
    }
};
wasmPatch.Start();
wasmPatch.WaitForExit();
Console.WriteLine("Finished applying patches");

static void PatchFile(string filePath, string oldText, string newText)
{
    var content = File.ReadAllText(filePath);
    if (!content.Contains(oldText))
    {
        Console.WriteLine($"Pattern not found in {Path.GetFileName(filePath)} (skipped)");
        return;
    }

    content = content.Replace(oldText, newText);
    File.WriteAllText(filePath, content);
    Console.WriteLine($"Patched {Path.GetFileName(filePath)}");
}
// Publish the project to get the latest framework files
var publishProcess = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = "publish -c Release -v d",
        WorkingDirectory = "FNAWasmRunner",
    }
};

publishProcess.Start();
publishProcess.WaitForExit();

Console.WriteLine("Finished publishing project");
Console.WriteLine("Now patching framework files...");

// Copy Content folder + json into publish output for static hosting
{
    var sourceRoot = Path.GetFullPath("Content");
    var destRoot = Path.Combine("FNAWasmRunner", "bin", "Release", "net10.0", "publish", "wwwroot", "Content");
    var jsonPath = Path.Combine("FNAWasmRunner", "bin", "Release", "net10.0", "publish", "wwwroot", "content-files.json");
    var json = "[";
    if (Directory.Exists(sourceRoot))
    {
        foreach (var file in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, file);
            var destPath = Path.Combine(destRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(file, destPath, true);
            json += "\"" + relativePath.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",";
        }
        Console.WriteLine($"Copied Content folder to {destRoot}");
    }
    json += "]";
    File.WriteAllText(jsonPath, json);
}

string frameworkDir = Path.Combine("FNAWasmRunner", "bin", "Release", "net10.0", "publish", "wwwroot", "_framework");
Console.WriteLine("1) dotnet.runtime.*.js patch");
Console.WriteLine("fixes mono init with -sWASMFS enabled");
var runtimeFile = Directory.GetFiles(frameworkDir, "dotnet.runtime.*.js").FirstOrDefault();
if (runtimeFile is not null)
{
    /*
    https://github.com/dotnet/runtime/blob/19d0c6e4593d9b2ffd516b854662e53d4ef95d83/src/mono/browser/runtime/startup.ts#L312
    if you have -sWASMFS enabled it throws an error (i think it's like no such file or directory?) on this call and that's the fix
    */
    PatchFile(
        runtimeFile,
        "FS_createPath(\"/\",\"usr/share\",!0,!0)",
        "FS_createPath(\"/usr\",\"share\",!0,!0)"
    );
}
Console.WriteLine("2) dotnet.native.*.js patch");
Console.WriteLine("automatically forces transfer of canvas matching selector `.canvas` (class canvas) to deputy thread (c# managed main thread)");
var nativeFile = Directory.GetFiles(frameworkDir, "dotnet.native.*.js").FirstOrDefault();
if (nativeFile is not null)
{
    /*
    this is a hack to give control of the game canvas to the c# main thread (dotnet-worker-001) 
    because we don't control when the dotnet runtime creates the c# main thread so we can't use the emscripten api that does the same thing (emscripten_pthread_attr_settransferredcanvases)
    it just forces a transfer of .canvas to the first thread created which happens to be c# main thread, patching emscripten pthread_create's js side 
    see: https://github.com/emscripten-core/emscripten/blob/3.1.56/src/library_pthread.js#L782
    */
    PatchFile(
        nativeFile,
        "var offscreenCanvases={};",
        "var offscreenCanvases={};if(globalThis.window&&!window.TRANSFERRED_CANVAS){transferredCanvasNames=[\".canvas\"];window.TRANSFERRED_CANVAS=true;}"
    );
}

if (doServe)
{
    var port = 5000;

    static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".htm" => "text/html; charset=utf-8",
        ".js" => "application/javascript",
        ".mjs" => "application/javascript",
        ".wasm" => "application/wasm",
        ".css" => "text/css; charset=utf-8",
        ".json" => "application/json",
        ".png" => "image/png",
        ".jpg" => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        _ => "application/octet-stream"
    };

    static string? ResolvePath(string urlPath)
    {
        var path = WebUtility.UrlDecode(urlPath);
        if (string.IsNullOrEmpty(path))
        {
            path = "/";
        }

        // default to index.html for directory paths
        if (path.EndsWith('/'))
        {
            path += "index.html";
        }

        // special logic for framework files to serve from the publish output instead of wwwroot
        var normalized = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);

        if (path.StartsWith("/_framework", StringComparison.Ordinal))
        {
            var suffix = path["/_framework".Length..].TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine("FNAWasmRunner", "bin", "Release", "net10.0", "publish", "wwwroot", "_framework", suffix);
        }

        if (path == "/content-files.json")
        {
            return Path.Combine("FNAWasmRunner", "bin", "Release", "net10.0", "publish", "wwwroot", "content-files.json");
        }

        // serve content files from the Content directory (should be copied to output on build)
        if (path.StartsWith("/Content", StringComparison.Ordinal))
        {
            var suffix = path["/Content".Length..].TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine("Content", suffix);
        }

        return Path.Combine("FNAWasmRunner", "wwwroot", normalized);
    }

    // Generate content-files.json as a static file in wwwroot before serving
    {
        var root = Path.GetFullPath("Content");
        var assets = Directory.Exists(root)
            ? Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                .ToArray()
            : Array.Empty<string>();
        // we put it in the release folder since we don't actually want to commit this file since its a build artifact
        var jsonPath = Path.Combine("FNAWasmRunner", "bin", "Release", "net10.0", "publish", "wwwroot", "content-files.json");
        var json = "[" + string.Join(",", assets.Select(a => "\"" + a.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"")) + "]";
        File.WriteAllText(jsonPath, json);
        Console.WriteLine($"Generated {jsonPath} ({assets.Length} file(s))");
    }

    using var listener = new HttpListener();
    listener.Prefixes.Add($"http://localhost:{port}/");
    listener.Start();

    Console.WriteLine($"Serving on http://localhost:{port}");
    Console.WriteLine("Press Ctrl+C to stop.");

    while (true)
    {
        var context = await listener.GetContextAsync();
        var response = context.Response;
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
        response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";

        var requestPath = context.Request.Url?.AbsolutePath ?? "/";
        Console.WriteLine($"[REQ] {context.Request.HttpMethod} {requestPath}");

        var filePath = ResolvePath(requestPath);
        if (filePath is null || !File.Exists(filePath))
        {
            response.StatusCode = 404;
            response.Close();
            continue;
        }

        var extension = Path.GetExtension(filePath);
        response.ContentType = GetContentType(extension);

        await using var stream = File.OpenRead(filePath);
        response.ContentLength64 = stream.Length;
        await stream.CopyToAsync(response.OutputStream);
        response.OutputStream.Close();
    }
}

Console.WriteLine("Done! Press any key to exit...");
Console.ReadKey();
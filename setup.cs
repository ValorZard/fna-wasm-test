using System.Diagnostics;
using System.Net.Http;

static void ForceDeleteDirectory(string path)
{
    // On windows, it's a bit annoying to delete directories containing read-only files, 
    // so we have to make sure to clear the read-only flag on all files before deleting.
    if (!Directory.Exists(path)) return;
    foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        File.SetAttributes(f, FileAttributes.Normal);
    Directory.Delete(path, true);
}

bool doClean = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "clean")
    {
        doClean = true;
        break;
    }
}

if (doClean)
{
    Console.WriteLine("Cleaning up...");
    ForceDeleteDirectory("FNANative");
    ForceDeleteDirectory("FNAWasm");
    ForceDeleteDirectory("FontStashSharp");
    Console.WriteLine("Finished cleaning up");
}

String branch = "26.04";
var nativeClone = new Process();
var wasmClone = new Process();
nativeClone.StartInfo.FileName = "git";
nativeClone.StartInfo.Arguments = $"clone https://github.com/FNA-XNA/FNA --recursive -b {branch} FNANative";
nativeClone.Start();
wasmClone.StartInfo.FileName = "git";
wasmClone.StartInfo.Arguments = $"clone https://github.com/FNA-XNA/FNA --recursive -b {branch} FNAWasm";
wasmClone.Start();
nativeClone.WaitForExit();
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

Console.WriteLine("Now downloading dependencies...");
Console.WriteLine("Downloading FontStashSharp...");
var fontStashRev = "1ce5d237f024e3f179e344d993f76353cbccb17e";
var fontStashClone = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "git",
        Arguments = "clone https://github.com/FontStashSharp/FontStashSharp.git --single-branch --depth 1 --recursive --revision " + fontStashRev,
    }
};
fontStashClone.Start();
fontStashClone.WaitForExit();
Console.WriteLine("Finished downloading FontStashSharp");
Console.WriteLine("Now applying FontStashSharp patches...");
var fontStashPatch = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "git",
        Arguments = "apply ../FontStashSharp.patch",
        WorkingDirectory = "FontStashSharp",
    }
};
fontStashPatch.Start();
fontStashPatch.WaitForExit();
Console.WriteLine("Finished applying FontStashSharp patches");

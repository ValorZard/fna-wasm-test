# FNA Native Wasm Combo
This project shows off a way to have FNA compile to both Native and Wasm.

However, the big catch is that we need to clone FNA twice, and then have one version of FNA be patched in order to compile to WASM.

We also show how to import external dependencies that require FNA (like [FontStashSharp](https://github.com/FontStashSharp/FontStashSharp)) by patching them to choose what FNA to use on compile time.

Note: You may get errors if you aren't on a new enough git (git 2.49 or newer)

## Initial setup for both wasm and native
run ``dotnet run setup.cs``

## running the native version:
- First, grab the FNA binaries from [fnalibs-dailies](https://github.com/FNA-XNA/fnalibs-dailies), and put the ones that fit the architecture you are on right next to where your executable will be.
- (For example, if you are on x64, copy the binaries in the x64 folder and put them in ``.\FNANativeRunner\bin\Debug\net10.0``)
- Then, do ``dotnet run --project FNANativeRunner``

## running the web version
- do ``dotnet run do_wasm.cs serve``
- (if you need to clean, you can do ``dotnet run do_wasm.cs serve clean``)
- If this is your first time building a Wasm project with dotnet, you'll get this error
```
error NETSDK1147:
  To build this project, the following workloads must be installed: wasm-tools
  To install these workloads, run the following command: dotnet workload restore
    Determining projects to restore...
```
- To fix it, just run ``dotnet workload restore`` to fix it.
- Also, if this is the first time running this script (or if you want a ``clean`` build), it will fetch the patched FNA binaries from [FNA-WASM-Build](https://github.com/r58Playz/FNA-WASM-Build) and put them in ``FNAWasmRunner/statics``
- The ones it will grab are:
    - ``FNA3D.a``
    - ``FAudio.a``
    - ``SDL3.a`` 
    - and ``libmojoshader.a``
- Note: sometimes the script might fail with an error like this
```
CSC : error CS2012: Cannot open 'C:\workspace\fna-game\FNAWasm\obj_core\Release\net8.0\FNA.dll' for writing -- The process cannot access the file 'C:\workspace\fna-game\FNAWasm\obj_core\Release\net8.0\FNA.dll' because it is being used by another process.; file may be locked by 'VBCSCompiler' (54356)
```
- Just retry the script again and it should work
- The web version will exist on ``http://localhost:5000/``

# Uploading WASM to itch.io
- zip up the wwwroot folder inside of the publish folder (on my computer that's inside of ``FNAWasmRunner\bin\Release\net10.0\publish\wwwroot``)
- upload it to itch.io (you want to make sure that ``index.html`` is root inside the zip)
- Also, you want to make sure that the SharedArrayBuffer option is turned on

## Notes
Thanks to @r58Playz for the inspiration/borrowed code from [FNA-Wasm-Threads](https://github.com/r58Playz/fna-wasm-threads) to make this work
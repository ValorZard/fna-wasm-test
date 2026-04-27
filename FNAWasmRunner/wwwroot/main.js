
const wasm = await eval(`import("./_framework/dotnet.js")`);
const dotnet = wasm.dotnet;

const preloadContentAssets = async (module) => {
	// Fetch the file list dynamically from the server
	const listResponse = await fetch("./content-files.json");
	if (!listResponse.ok) {
		throw new Error(`Failed to fetch /content-files.json: ${listResponse.status}`);
	}
	const assets = await listResponse.json();
	console.debug(`Content preload: ${assets.length} file(s) to load`);

	if (typeof module.FS_createPath !== "function" || typeof module.FS_createDataFile !== "function") {
		console.warn("WASM FS helpers are unavailable; content preload skipped.");
		return;
	}

	module.FS_createPath("/", "Content", true, true);

	for (const asset of assets) {
		const parts = asset.replace(/\\/g, "/").split("/");
		const fileName = parts[parts.length - 1];

		// Create any nested subdirectories
		let dir = "/Content";
		for (let i = 0; i < parts.length - 1; i++) {
			module.FS_createPath(dir, parts[i], true, true);
			dir += "/" + parts[i];
		}

		const url = "./Content/" + parts.map(encodeURIComponent).join("/");
		const response = await fetch(url);
		if (!response.ok) {
			throw new Error(`Failed to fetch ${url}: ${response.status} ${response.statusText}`);
		}

		const bytes = new Uint8Array(await response.arrayBuffer());
		module.FS_createDataFile(dir, fileName, bytes, true, false, false);
	}
};

console.debug("initializing dotnet");
const runtime = await dotnet.withConfig({
}).create();

const config = runtime.getConfig();
const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
const canvas = document.getElementById("canvas");
dotnet.instance.Module.canvas = canvas;

self.wasm = {
	Module: dotnet.instance.Module,
	dotnet,
	runtime,
	config,
	exports,
	canvas,
};

console.debug("PreInit...");
await runtime.runMain();
await exports.Program.PreInit();
await preloadContentAssets(dotnet.instance.Module);
console.debug("dotnet initialized");

console.debug("Init...");
await exports.Program.Init();

console.debug("MainLoop...");
const main = async () => {
	const ret = await exports.Program.MainLoop();

	if (!ret) {
		console.debug("Cleanup...");
		await exports.Program.Cleanup();
		return;
	}

	requestAnimationFrame(main);
}
requestAnimationFrame(main);

import { createRequire } from "node:module";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const outputDir = resolve(__dirname, "../Runtime/Resources/HeliosDebugger/Icons");
const require = createRequire(process.env.RESVG_REQUIRE_BASE ?? import.meta.url);
const { Resvg } = require("@resvg/resvg-js");

const icons = {
  "activity": '<path d="M22 12h-4l-3 9L9 3l-3 9H2"/>',
  "alert-circle": '<circle cx="12" cy="12" r="10"/><line x1="12" x2="12" y1="8" y2="12"/><line x1="12" x2="12.01" y1="16" y2="16"/>',
  "alert-triangle": '<path d="m21.73 18-8-14a2 2 0 0 0-3.46 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"/><path d="M12 9v4"/><path d="M12 17h.01"/>',
  "bug": '<path d="m8 2 1.88 1.88"/><path d="M14.12 3.88 16 2"/><path d="M9 7.13v-1a3 3 0 1 1 6 0v1"/><path d="M12 20c-3.3 0-6-2.7-6-6v-3a4 4 0 0 1 4-4h4a4 4 0 0 1 4 4v3c0 3.3-2.7 6-6 6"/><path d="M12 20v-9"/><path d="M6.53 9C4.6 8.8 3 7.1 3 5"/><path d="M17.47 9C19.4 8.8 21 7.1 21 5"/><path d="M6 13H2"/><path d="M18 13h4"/><path d="M7 17H3"/><path d="M17 17h4"/>',
  "camera": '<path d="M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3Z"/><circle cx="12" cy="13" r="3"/>',
  "check": '<path d="M20 6 9 17l-5-5"/>',
  "chevron-left": '<path d="m15 18-6-6 6-6"/>',
  "chevron-right": '<path d="m9 18 6-6-6-6"/>',
  "copy": '<rect width="14" height="14" x="8" y="8" rx="2"/><path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2"/>',
  "cpu": '<rect width="16" height="16" x="4" y="4" rx="2"/><rect width="6" height="6" x="9" y="9" rx="1"/><path d="M15 2v2"/><path d="M15 20v2"/><path d="M2 15h2"/><path d="M2 9h2"/><path d="M20 15h2"/><path d="M20 9h2"/><path d="M9 2v2"/><path d="M9 20v2"/>',
  "download": '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" x2="12" y1="15" y2="3"/>',
  "filter": '<polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3"/>',
  "info": '<circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/>',
  "pause": '<rect width="4" height="16" x="6" y="4"/><rect width="4" height="16" x="14" y="4"/>',
  "pin": '<path d="M12 17v5"/><path d="M9 10.76 5.24 7a2 2 0 0 1 0-2.83l.93-.93a2 2 0 0 1 2.83 0L12.76 7"/><path d="m14 4 6 6"/><path d="M7.5 12.5 2 18"/><path d="M12.76 7 17 11.24"/><path d="M17 11.24 11.24 17 7 12.76 12.76 7Z"/>',
  "play": '<polygon points="5 3 19 12 5 21 5 3"/>',
  "refresh": '<path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><path d="M3 3v5h5"/><path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16"/><path d="M16 16h5v5"/>',
  "search": '<circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/>',
  "sliders": '<line x1="21" x2="14" y1="4" y2="4"/><line x1="10" x2="3" y1="4" y2="4"/><line x1="21" x2="12" y1="12" y2="12"/><line x1="8" x2="3" y1="12" y2="12"/><line x1="21" x2="16" y1="20" y2="20"/><line x1="12" x2="3" y1="20" y2="20"/><line x1="14" x2="14" y1="2" y2="6"/><line x1="8" x2="8" y1="10" y2="14"/><line x1="16" x2="16" y1="18" y2="22"/>',
  "terminal": '<polyline points="4 17 10 11 4 5"/><line x1="12" x2="20" y1="19" y2="19"/>',
  "trash": '<path d="M3 6h18"/><path d="M8 6V4c0-1 .9-2 2-2h4c1.1 0 2 .9 2 2v2"/><path d="M19 6 18 20c0 1.1-.9 2-2 2H8c-1.1 0-2-.9-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/>',
  "x": '<path d="M18 6 6 18"/><path d="m6 6 12 12"/>',
  "zap": '<polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/>'
};

mkdirSync(outputDir, { recursive: true });

for (const [name, body] of Object.entries(icons)) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round">${body}</svg>`;
  const png = new Resvg(svg, {
    fitTo: { mode: "width", value: 128 },
    background: "rgba(0,0,0,0)"
  }).render().asPng();
  writeFileSync(resolve(outputDir, `${name}.png`), png);
}

console.log(`Generated ${Object.keys(icons).length} Helios icons in ${outputDir}`);

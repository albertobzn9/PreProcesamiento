// Format conversion only: keep the approved artwork unchanged.
// Run on macOS: node scripts/build-product-icons.mjs
import { execFileSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const source = resolve(root, 'src/VideoBatchProcessor.App/Assets/Branding/product-icon.png');
const output = resolve(root, 'src/VideoBatchProcessor.App/Assets/Packaging');
const temp = mkdtempSync(join(tmpdir(), 'vbp-icons-'));
const resize = (size, path) => execFileSync('/usr/bin/sips',
  ['-z', String(size), String(size), source, '--out', path], { stdio: 'ignore' });

try {
  const iconset = join(temp, 'VideoBatchProcessor.iconset');
  mkdirSync(iconset);
  for (const size of [16, 32, 128, 256, 512]) {
    resize(size, join(iconset, `icon_${size}x${size}.png`));
    resize(size * 2, join(iconset, `icon_${size}x${size}@2x.png`));
  }
  execFileSync('/usr/bin/iconutil', ['-c', 'icns', iconset, '-o', join(output, 'VideoBatchProcessor.icns')]);

  // Windows ICO directory followed by PNG payloads, one per supported size.
  const sizes = [16, 32, 48, 64, 128, 256];
  const header = Buffer.alloc(6 + sizes.length * 16);
  header.writeUInt16LE(1, 2);
  header.writeUInt16LE(sizes.length, 4);
  let offset = header.length;
  const images = sizes.map((size, index) => {
    const path = join(temp, `${size}.png`);
    resize(size, path);
    const png = readFileSync(path);
    const entry = 6 + index * 16;
    header[entry] = header[entry + 1] = size === 256 ? 0 : size;
    header.writeUInt16LE(1, entry + 4);
    header.writeUInt16LE(32, entry + 6);
    header.writeUInt32LE(png.length, entry + 8);
    header.writeUInt32LE(offset, entry + 12);
    offset += png.length;
    return png;
  });
  writeFileSync(join(output, 'VideoBatchProcessor.ico'), Buffer.concat([header, ...images]));
  console.log('Updated macOS and Windows icons from product-icon.png.');
} finally {
  rmSync(temp, { recursive: true, force: true });
}

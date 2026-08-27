// Rasterises the atlas and writes its slice table. Geometry lives in atlas.data.mjs.

import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { SHEET_W, SHEET_H, PPU, SPRITES } from './atlas.data.mjs';

const here = dirname(fileURLToPath(import.meta.url));

// ---------------------------------------------------------------------------
// emit
// ---------------------------------------------------------------------------
const body = SPRITES.map((s) => s.draw(s)).join('\n  ');

const sheet =
  `<svg xmlns="http://www.w3.org/2000/svg" width="${SHEET_W}" height="${SHEET_H}" ` +
  `viewBox="0 0 ${SHEET_W} ${SHEET_H}" shape-rendering="geometricPrecision">\n  ${body}\n</svg>\n`;

writeFileSync(join(here, 'UIAtlas.svg'), sheet);

// The fragment the design canvas embeds: same geometry, no <svg> wrapper duties.
writeFileSync(join(here, 'atlas-plain.frag.svg'), body);

const noted = SPRITES.map((s) => {
  return (
    `<rect x="${s.x - 0.5}" y="${s.y - 0.5}" width="${s.w + 1}" height="${s.h + 1}" fill="none" ` +
    `stroke="#e8574a" stroke-width="1" stroke-dasharray="3 3" opacity=".62"/>` +
    `<text x="${s.x}" y="${s.y + s.h + 15}" font-family="ui-monospace, Menlo, monospace" font-size="9.5" ` +
    `letter-spacing=".03em" fill="#b5aa96">${s.name}</text>` +
    `<text x="${s.x}" y="${s.y + s.h + 28}" font-family="ui-monospace, Menlo, monospace" font-size="9.5" ` +
    `fill="#7b7263">${s.w}x${s.h}</text>`
  );
}).join('\n  ');

writeFileSync(join(here, 'atlas-noted.frag.svg'), `${body}\n  ${noted}`);

writeFileSync(
  join(here, 'UIAtlas.slices.json'),
  `${JSON.stringify(
    {
      sheet: `${SHEET_W}x${SHEET_H}`,
      pixelsPerUnit: PPU,
      note:
        'Border is [left, bottom, right, top] in sheet pixels, matching UnityEditor.SpriteMetaData.border. ' +
        'Import as Sprite (2D and UI), Multiple, Mesh Type Full Rect, Alpha Is Transparency on, ' +
        'Filter Bilinear, Compression None, Pixels Per Unit 4.',
      sprites: SPRITES.map(({ name, x, y, w, h, border, mode, role, use }) => ({
        name, rect: { x, y, w, h }, border, mode, colorRole: role, usedBy: use
      }))
    },
    null,
    2
  )}\n`
);

// A tiny HTML shell so headless Chrome can rasterise the sheet with a real alpha channel.
writeFileSync(
  join(here, 'raster.html'),
  `<!doctype html><meta charset="utf-8">` +
    `<style>html,body{margin:0;padding:0;background:transparent}svg{display:block}</style>` +
    sheet
);

console.log(`atlas: ${SPRITES.length} sprites on ${SHEET_W}x${SHEET_H} @ PPU ${PPU}`);

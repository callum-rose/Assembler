// Writes the design-canvas artboards from the same data the atlas is drawn from,
// so the sheet, the slice table and the icon spec can never drift apart.

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { SHEET_W, SHEET_H, PPU, SPRITES, ICONS, ICON_GRID, strokeAttrs } from './atlas.data.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const read = (f) => readFileSync(join(here, f), 'utf8');

const noted = read('atlas-noted.frag.svg');
const plain = read('atlas-plain.frag.svg');

// --- the shell's own palette, transcribed from ShellAssetBuilder.cs -----------
const C = {
  paper: '#faf6ee', surface: '#fffdf8', sunk: '#efe9dd',
  ink: '#17130d', ink2: '#4f483d', ink3: '#948b7c',
  rule: '#d8cfbe', ruleHard: '#17130d',
  accent: '#b8121b', accent2: '#8a7248', onAccent: '#fffdf8',
  good: '#1d7a45', bad: '#b8121b',
  darkPaper: '#13100b', darkInk: '#f8f2e5', darkInk3: '#7b7263', darkRule: '#312a1f',
  darkAccent: '#e8574a'
};

const SERIF = `'Newsreader', Georgia, 'Times New Roman', serif`;
const MONO = `'IBM Plex Mono', ui-monospace, Menlo, 'SFMono-Regular', monospace`;

const FONTS =
  `<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Newsreader:ital,opsz,wght@0,6..72,400;0,6..72,600;0,6..72,700;0,6..72,800;1,6..72,400&amp;family=IBM+Plex+Mono:wght@400;500;600&amp;display=swap">`;

// Every artboard is a static Design Component: no holes, so no logic script.
const doc = (helmetStyle, bodyMarkup) =>
  `<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  <script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
  ${FONTS}
  <style>
    body { margin: 0; font-family: ${SERIF}; -webkit-font-smoothing: antialiased; }
    a { color: ${C.accent}; text-decoration: none; }
    a:hover { color: ${C.ink}; }
    ${helmetStyle}
  </style>
</helmet>
${bodyMarkup}
</x-dc>
</body>
</html>
`;

// --- shared chrome: every artboard opens with a masthead, as the app does -----
const masthead = (title, standfirst, opts = {}) => {
  const dark = opts.dark === true;
  const inkC = dark ? C.darkInk : C.ink;
  const metaC = dark ? C.darkInk3 : C.ink3;
  const accentC = dark ? C.darkAccent : C.accent;
  const hardC = dark ? C.darkInk : C.ruleHard;
  return `<header style="display: flex; flex-direction: column; gap: 0; margin-bottom: 26px">
    <div style="display: flex; align-items: baseline; justify-content: space-between; gap: 24px; border-bottom: 3px solid ${hardC}; padding-bottom: 9px">
      <h1 style="margin: 0; font-family: ${SERIF}; font-size: 27px; font-weight: 700; letter-spacing: -0.03em; color: ${inkC}">The Daily <span style="color: ${accentC}">Build</span></h1>
      <span style="font-family: ${MONO}; font-size: 10px; letter-spacing: 0.14em; text-transform: uppercase; color: ${metaC}">Shell UI atlas &middot; ${SHEET_W}&times;${SHEET_H} &middot; PPU ${PPU}</span>
    </div>
    <div style="display: flex; align-items: center; justify-content: space-between; gap: 24px; border-bottom: 1px solid ${hardC}; padding: 7px 0 8px; font-family: ${SERIF}; font-size: 10px; font-weight: 600; letter-spacing: 0.13em; text-transform: uppercase; color: ${dark ? C.darkInk3 : C.ink2}">
      <span>${title}</span>
      <span style="color: ${metaC}">${standfirst}</span>
    </div>
  </header>`;
};

const sectionHead = (text, dark = false) =>
  `<div style="display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 0 0 7px; border-bottom: 4px double ${dark ? C.darkInk : C.ruleHard}; margin: 0 0 16px">
    <h4 style="margin: 0; font-family: ${SERIF}; font-size: 10.5px; font-weight: 800; letter-spacing: 0.2em; text-transform: uppercase; color: ${dark ? C.darkInk : C.ink}">${text}</h4>
  </div>`;

// =============================================================================
// 1. Main.dc.html — the sheet itself
// =============================================================================
const GUT_L = 42;
const GUT_T = 26;

const gridLines = () => {
  const out = [];
  for (let x = 0; x <= SHEET_W; x += 64) {
    out.push(`<path d="M${x} 0 L${x} ${SHEET_H}" stroke="${C.darkRule}" stroke-width="1" opacity="${x % 256 === 0 ? 0.95 : 0.4}"/>`);
  }
  for (let y = 0; y <= SHEET_H; y += 64) {
    out.push(`<path d="M0 ${y} L${SHEET_W} ${y}" stroke="${C.darkRule}" stroke-width="1" opacity="${y % 256 === 0 ? 0.95 : 0.4}"/>`);
  }
  // Ticks live in the gutter, outside the sheet, so a number never reads as art.
  for (let x = 0; x <= SHEET_W; x += 128) {
    out.push(`<text x="${x}" y="-9" text-anchor="middle" font-family="${MONO}" font-size="10" fill="${C.darkInk3}">${x}</text>`);
  }
  for (let y = 0; y <= SHEET_H; y += 128) {
    out.push(`<text x="-12" y="${y + 4}" text-anchor="end" font-family="${MONO}" font-size="10" fill="${C.darkInk3}">${y}</text>`);
  }
  return out.join('\n      ');
};

const legend = [
  ['White on transparent', 'Nothing in the atlas carries a colour. Every graphic takes one from a <b>ThemeColor</b> role binder (UIPLAN 5.2), which is what makes dark mode (#575) a second theme asset and not a second sheet.'],
  ['Authored at 4&times;', 'A 44-unit hit target ships as 176 px, a 24-unit icon as 96 px &mdash; the 3&ndash;4&times; raster rule (UIPLAN 2.3). Import at <b>Pixels Per Unit 4</b> and Set Native Size lands on the unit sizes the prototype uses.'],
  ['Nine sprites stretch', 'The plates, frames, field, chip, pill and sheet are 9-sliced; their borders are in the slice table. The icons, the disc and Fill are Simple.'],
  ['Origin is top-left', 'Rects below are top-left origin, as the Sprite Editor shows them. <code>UIAtlas.slices.json</code> ships the same numbers as <code>[left, bottom, right, top]</code> borders, matching <code>SpriteMetaData.border</code>.']
];

writeFileSync(
  join(here, 'Main.dc.html'),
  doc(
    `.legend b { color: ${C.darkInk}; font-weight: 700; }
    .legend code { font-family: ${MONO}; font-size: 11px; color: ${C.darkInk}; }`,
    `<div style="background: ${C.darkPaper}; min-height: 100%; padding: 34px 40px 40px">
  ${masthead('The sprite sheet', '23 sprites &middot; white on transparent', { dark: true })}

  <div style="margin-bottom: 30px">
    <svg viewBox="${-GUT_L} ${-GUT_T} ${SHEET_W + GUT_L + 30} ${SHEET_H + GUT_T + 34}" width="${SHEET_W + GUT_L + 30}" height="${SHEET_H + GUT_T + 34}" xmlns="http://www.w3.org/2000/svg" style="display: block">
      <rect x="0" y="0" width="${SHEET_W}" height="${SHEET_H}" fill="#0d0b07"/>
      ${gridLines()}
      ${noted}
    </svg>
  </div>

  ${sectionHead('How to read it', true)}
  <div class="legend" style="display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 18px 34px; font-family: ${SERIF}; font-size: 13px; line-height: 1.55; color: ${C.darkInk3}">
    ${legend
      .map(
        ([h, p]) => `<div style="display: flex; flex-direction: column; gap: 5px">
      <span style="font-family: ${MONO}; font-size: 10px; font-weight: 600; letter-spacing: 0.12em; text-transform: uppercase; color: ${C.darkAccent}">${h}</span>
      <span>${p}</span>
    </div>`
      )
      .join('\n    ')}
  </div>
</div>`
  )
);

// =============================================================================
// 2. Icons.dc.html — the icon spec, drawn on its own grid
// =============================================================================
const iconNames = Object.keys(ICONS);
const iconMeta = Object.fromEntries(
  SPRITES.filter((s) => s.icon).map((s) => [s.icon, s])
);

const iconCard = (name) => {
  const s = iconMeta[name];
  const box = 168; // 24u grid drawn at 7x
  const k = box / ICON_GRID;
  const ticks = [];
  for (let i = 0; i <= ICON_GRID; i += 4) {
    ticks.push(`<path d="M${i * k} 0 L${i * k} ${box}" stroke="${C.rule}" stroke-width="1" opacity="${i % 12 === 0 ? 0.95 : 0.5}"/>`);
    ticks.push(`<path d="M0 ${i * k} L${box} ${i * k}" stroke="${C.rule}" stroke-width="1" opacity="${i % 12 === 0 ? 0.95 : 0.5}"/>`);
  }
  // The safe area: 2u of padding all round, which is what keeps a 24u glyph from
  // touching the edge of the sprite when it sits in a 44u hit target.
  const safe = `<rect x="${2 * k}" y="${2 * k}" width="${20 * k}" height="${20 * k}" fill="none" stroke="${C.accent}" stroke-width="1" stroke-dasharray="4 4" opacity=".45"/>`;
  return `<div style="display: flex; flex-direction: column; gap: 9px">
    <svg viewBox="0 0 ${box} ${box}" width="${box}" height="${box}" xmlns="http://www.w3.org/2000/svg" style="display: block; background: ${C.surface}; border: 1px solid ${C.rule}">
      ${ticks.join('')}
      ${safe}
      <g transform="scale(${k})" style="color: ${C.ink}">${ICONS[name].replace(/#fff/g, C.ink)}</g>
    </svg>
    <div style="display: flex; flex-direction: column; gap: 3px">
      <span style="font-family: ${MONO}; font-size: 11px; font-weight: 600; color: ${C.ink}">Icon${name}</span>
      <span style="font-family: ${SERIF}; font-size: 11.5px; line-height: 1.45; color: ${C.ink3}">${s.use}</span>
      <span style="font-family: ${MONO}; font-size: 9.5px; letter-spacing: 0.08em; text-transform: uppercase; color: ${C.accent}">role ${s.role}</span>
    </div>
  </div>`;
};

writeFileSync(
  join(here, 'Icons.dc.html'),
  doc(
    '',
    `<div style="background: ${C.paper}; min-height: 100%; padding: 34px 40px 44px">
  ${masthead('The icons', '24-unit grid &middot; 2-unit stroke')}

  <p style="margin: 0 0 24px; font-family: ${SERIF}; font-size: 14px; line-height: 1.6; color: ${C.ink2}; max-width: 62ch; text-wrap: pretty">
    Each glyph is drawn on a 24-unit grid inside a 96&nbsp;px cell &mdash; 24 units at PPU&nbsp;4 &mdash; with a 2-unit stroke, round caps and round joins. The dashed square is the 2-unit safe area. Pause, play and the sparkle are filled rather than stroked: a 2-unit stroke closes up on a glyph this small, and all three only ever appear at badge size.
  </p>

  ${sectionHead('Nine glyphs')}
  <div style="display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 30px 34px">
    ${iconNames.map(iconCard).join('\n    ')}
  </div>

  <div style="margin-top: 34px; padding-top: 16px; border-top: 1px solid ${C.rule}; font-family: ${SERIF}; font-size: 12.5px; line-height: 1.6; color: ${C.ink3}; max-width: 74ch">
    <b style="color: ${C.ink}; font-weight: 700">Nothing raycasts except things named HitTarget</b> (UIPLAN 7.4). An icon Image sets <code style="font-family: ${MONO}; font-size: 11px">raycastTarget = false</code> and sits inside a stationary 44&times;44-unit HitTarget that never animates.
  </div>
</div>`
  )
);

// =============================================================================
// 3. SliceTable.dc.html — the assembly key
// =============================================================================
const bandOf = (s) => (s.y < 180 ? 'Plates and frames' : s.y < 370 ? 'Surfaces, pills and rules' : 'Icons');
const bands = [...new Set(SPRITES.map(bandOf))];

const th = `text-align: left; font-family: ${MONO}; font-size: 9px; font-weight: 600; letter-spacing: 0.12em; text-transform: uppercase; color: ${C.ink3}; padding: 0 10px 7px 0; border-bottom: 1px solid ${C.ruleHard}; white-space: nowrap;`;
const td = `font-family: ${MONO}; font-size: 11px; color: ${C.ink}; padding: 9px 10px 9px 0; border-bottom: 1px solid ${C.rule}; vertical-align: top; white-space: nowrap;`;
const tdWrap = `font-family: ${SERIF}; font-size: 12px; line-height: 1.45; color: ${C.ink2}; padding: 9px 0; border-bottom: 1px solid ${C.rule}; vertical-align: top; white-space: normal; min-width: 250px;`;

const rows = (band) =>
  SPRITES.filter((s) => bandOf(s) === band)
    .map(
      (s) => `<tr>
      <td style="${td} font-weight: 600">${s.name}</td>
      <td style="${td}">${s.x}, ${s.y}</td>
      <td style="${td}">${s.w} &times; ${s.h}</td>
      <td style="${td}">${(s.w / PPU).toFixed(s.w % PPU ? 1 : 0)} &times; ${(s.h / PPU).toFixed(s.h % PPU ? 1 : 0)}</td>
      <td style="${td} color: ${s.mode === 'Sliced' ? C.accent : C.ink3}">${s.mode}</td>
      <td style="${td}">${s.border.join(', ')}</td>
      <td style="${td} color: ${C.accent2}">${s.role}</td>
      <td style="${tdWrap}">${s.use}</td>
    </tr>`
    )
    .join('\n    ');

const importSettings = [
  ['Texture Type', 'Sprite (2D and UI)'],
  ['Sprite Mode', 'Multiple'],
  ['Pixels Per Unit', '4'],
  ['Mesh Type', 'Full Rect &mdash; required, or the 9-slices tear'],
  ['Alpha Is Transparency', 'On'],
  ['Wrap Mode', 'Clamp'],
  ['Filter Mode', 'Bilinear'],
  ['Compression', 'None &mdash; the sheet is 22 KB'],
  ['Generate Mip Maps', 'Off &mdash; UI is drawn at one depth'],
  ['sRGB (Color Texture)', 'On']
];

writeFileSync(
  join(here, 'SliceTable.dc.html'),
  doc(
    `code { font-family: ${MONO}; font-size: 11px; color: ${C.ink}; }`,
    `<div style="background: ${C.paper}; min-height: 100%; padding: 34px 36px 44px">
  ${masthead('The slice table', 'Rects, borders and roles')}

  <p style="margin: 0 0 22px; font-family: ${SERIF}; font-size: 14px; line-height: 1.6; color: ${C.ink2}; max-width: 78ch; text-wrap: pretty">
    Rect origin is top-left, as the Sprite Editor reads it. <b>Border is left, bottom, right, top</b> &mdash; the order <code>SpriteMetaData.border</code> takes &mdash; and the same numbers ship in <code>UIAtlas.slices.json</code> beside the sheet. Units are canvas units on the 390-unit short axis (UIPLAN 2.2), so they are the prototype's CSS pixels 1:1.
  </p>

  ${bands
    .map(
      (band) => `${sectionHead(band)}
  <table style="width: 100%; border-collapse: collapse; margin-bottom: 30px">
    <thead><tr>
      <th style="${th}">Sprite</th><th style="${th}">x, y</th><th style="${th}">px</th>
      <th style="${th}">units</th><th style="${th}">Mode</th><th style="${th}">Border</th>
      <th style="${th}">Colour role</th><th style="${th}">Used by</th>
    </tr></thead>
    <tbody>
    ${rows(band)}
    </tbody>
  </table>`
    )
    .join('\n  ')}

  ${sectionHead('Import settings')}
  <div style="display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0 40px">
    ${importSettings
      .map(
        ([k, v]) => `<div style="display: flex; align-items: baseline; justify-content: space-between; gap: 14px; padding: 8px 0; border-bottom: 1px solid ${C.rule}">
      <span style="font-family: ${MONO}; font-size: 11px; color: ${C.ink}">${k}</span>
      <span style="font-family: ${SERIF}; font-size: 12.5px; color: ${C.ink3}; text-align: right">${v}</span>
    </div>`
      )
      .join('\n    ')}
  </div>

  <div style="margin-top: 26px; padding: 14px 16px 15px; background: ${C.surface}; border: 1px solid ${C.rule}; border-left: 3px solid ${C.accent}">
    <h5 style="margin: 0 0 9px; font-family: ${SERIF}; font-size: 10px; font-weight: 800; letter-spacing: 0.16em; text-transform: uppercase; color: ${C.ink3}">Two sprites with a floor</h5>
    <ul style="margin: 0; padding: 0; list-style: none; display: flex; flex-direction: column; gap: 7px; font-family: ${SERIF}; font-size: 13px; line-height: 1.45; color: ${C.ink}">
      <li><b style="color: ${C.accent}; font-weight: 800">Pill</b> is 31 px of border either side, so it cannot render below 16 units tall. The toggle track is 28 &mdash; fine. Anything thinner takes <b>PillSmall</b>.</li>
      <li><b style="color: ${C.accent}; font-weight: 800">RuleDouble</b> stretches horizontally only. Its 1&thinsp;/&thinsp;2&thinsp;/&thinsp;1-unit bands are baked at natural height, so pin it to 4 units tall and it reproduces the prototype's <code>4px double</code> exactly.</li>
    </ul>
  </div>
</div>`
  )
);

// =============================================================================
// 4. Assembly.dc.html — how the sprites compose into UIPLAN 7.2's inventory
// =============================================================================
const chip = (text, tone = 'ink') => {
  const bg = { ink: C.ink, accent: C.accent, quiet: C.sunk, good: C.good }[tone];
  const fg = tone === 'quiet' ? C.ink2 : C.onAccent;
  return `<span style="font-family: ${MONO}; font-size: 9px; font-weight: 600; letter-spacing: 0.08em; text-transform: uppercase; background: ${bg}; color: ${fg}; padding: 3px 6px; border-radius: 2px; white-space: nowrap">${text}</span>`;
};

const layer = (sprite, role, note) =>
  `<li style="display: flex; align-items: baseline; gap: 9px; padding: 5px 0; border-bottom: 1px solid ${C.rule}">
    <span style="font-family: ${MONO}; font-size: 10.5px; font-weight: 600; color: ${C.ink}; flex: none; min-width: 104px">${sprite}</span>
    <span style="font-family: ${MONO}; font-size: 9.5px; color: ${C.accent2}; flex: none; min-width: 92px">${role}</span>
    <span style="font-family: ${SERIF}; font-size: 12px; line-height: 1.4; color: ${C.ink2}">${note}</span>
  </li>`;

const recipe = (title, tag, preview, layers, foot) =>
  `<section style="display: flex; flex-direction: column; gap: 13px; background: ${C.surface}; border: 1px solid ${C.rule}; padding: 17px 18px 18px">
    <div style="display: flex; align-items: center; justify-content: space-between; gap: 12px; padding-bottom: 8px; border-bottom: 4px double ${C.ruleHard}">
      <h4 style="margin: 0; font-family: ${SERIF}; font-size: 11px; font-weight: 800; letter-spacing: 0.18em; text-transform: uppercase; color: ${C.ink}">${title}</h4>
      ${tag}
    </div>
    <div style="display: flex; align-items: center; justify-content: center; min-height: 108px; padding: 14px 10px; background: ${C.paper}; border: 1px solid ${C.rule}">${preview}</div>
    <ul style="margin: 0; padding: 0; list-style: none; display: flex; flex-direction: column">${layers.join('')}</ul>
    ${foot ? `<p style="margin: 0; font-family: ${SERIF}; font-size: 12px; line-height: 1.5; color: ${C.ink3}">${foot}</p>` : ''}
  </section>`;

// --- previews, built from the same geometry the sprites are ------------------
const glyph = (name, size, color) =>
  `<svg viewBox="0 0 ${ICON_GRID} ${ICON_GRID}" width="${size}" height="${size}" xmlns="http://www.w3.org/2000/svg" style="display: block; flex: none">${ICONS[name].replace(/#fff/g, color)}</svg>`;

const letterpressPreview = `<div style="position: relative; width: 232px; height: 52px">
  <div style="position: absolute; left: 4px; top: 4px; width: 228px; height: 48px; background: ${C.accent}; border-radius: 2px"></div>
  <div style="position: absolute; left: 0; top: 0; width: 228px; height: 48px; background: ${C.ink}; border-radius: 2px; display: flex; align-items: center; justify-content: center">
    <span style="font-family: ${SERIF}; font-size: 13px; font-weight: 800; letter-spacing: 0.14em; text-transform: uppercase; color: ${C.paper}">Play today's</span>
  </div>
</div>`;

const pressedPreview = `<div style="display: flex; align-items: center; gap: 26px">
  <div style="position: relative; width: 118px; height: 46px">
    <div style="position: absolute; left: 4px; top: 4px; width: 114px; height: 42px; background: ${C.rule}; border-radius: 2px"></div>
    <div style="position: absolute; left: 0; top: 0; width: 114px; height: 42px; background: ${C.surface}; border: 1.5px solid ${C.ruleHard}; border-radius: 2px; display: flex; align-items: center; justify-content: center">
      <span style="font-family: ${SERIF}; font-size: 11px; font-weight: 800; letter-spacing: 0.14em; text-transform: uppercase; color: ${C.ink}">Archive</span>
    </div>
  </div>
  <div style="position: relative; width: 118px; height: 46px">
    <div style="position: absolute; left: 4px; top: 4px; width: 114px; height: 42px; background: ${C.ink}; border-radius: 2px; display: flex; align-items: center; justify-content: center">
      <span style="font-family: ${SERIF}; font-size: 11px; font-weight: 800; letter-spacing: 0.14em; text-transform: uppercase; color: ${C.paper}">Pressed</span>
    </div>
  </div>
</div>`;

const playedPreview = `<div style="position: relative; width: 148px; height: 92px; background: ${C.sunk}; overflow: hidden">
  <svg viewBox="0 0 100 62" width="148" height="92" xmlns="http://www.w3.org/2000/svg" style="display: block; opacity: .5; filter: grayscale(1)">
    <rect width="100" height="62" fill="#9aa2ad"/><circle cx="30" cy="24" r="13" fill="#5c6672"/><rect x="52" y="16" width="26" height="34" rx="2" fill="#767f8b"/>
  </svg>
  <div style="position: absolute; left: 6px; top: 6px; width: 22px; height: 22px; border-radius: 50%; background: ${C.ink}; display: flex; align-items: center; justify-content: center">
    ${glyph('Tick', 15, C.paper)}
  </div>
</div>`;

const stampPreview = `<div style="position: relative; width: 168px; height: 84px; background: ${C.sunk}; overflow: hidden">
  <svg viewBox="0 0 100 50" width="168" height="84" xmlns="http://www.w3.org/2000/svg" style="display: block">
    <rect width="100" height="50" fill="#7d6f56"/><circle cx="26" cy="20" r="11" fill="#c0a87f"/><rect x="48" y="12" width="24" height="28" rx="2" fill="#a08e6d"/>
  </svg>
  <div style="position: absolute; right: 10px; bottom: 10px; transform: rotate(-8deg); border: 2.5px solid ${C.accent}; border-radius: 3px; padding: 5px 10px">
    <span style="font-family: ${MONO}; font-size: 11px; font-weight: 600; letter-spacing: 0.18em; color: ${C.accent}">PLAY</span>
  </div>
</div>`;

const searchPreview = `<div style="display: flex; align-items: center; gap: 8px; width: 246px; background: ${C.sunk}; border-radius: 8px; padding: 10px 12px">
  ${glyph('Search', 17, C.ink3)}
  <span style="flex: 1; font-family: ${SERIF}; font-size: 15px; color: ${C.ink3}">Search the archive</span>
  <span style="width: 18px; height: 18px; border-radius: 50%; background: ${C.ink3}; display: flex; align-items: center; justify-content: center; flex: none">${glyph('Close', 11, C.paper)}</span>
</div>`;

const togglePreview = `<div style="display: flex; align-items: center; gap: 30px">
  <div style="width: 46px; height: 28px; border-radius: 999px; background: ${C.good}; position: relative; flex: none">
    <div style="position: absolute; top: 3px; left: 21px; width: 22px; height: 22px; border-radius: 50%; background: ${C.paper}"></div>
  </div>
  <div style="display: flex; gap: 0; background: ${C.sunk}; border-radius: 6px; padding: 2px">
    ${['Light', 'Dark', 'Auto']
      .map(
        (t, i) => `<span style="font-family: ${SERIF}; font-size: 11px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; padding: 6px 10px; border-radius: 4px; background: ${i === 0 ? C.paper : 'transparent'}; color: ${i === 0 ? C.ink : C.ink3}">${t}</span>`
      )
      .join('')}
  </div>
</div>`;

const slipPreview = `<div style="position: relative; width: 200px; height: 118px">
  <div style="position: absolute; left: 8px; top: 8px; width: 192px; height: 110px; background: ${C.accent}; border-radius: 2px"></div>
  <div style="position: absolute; left: 0; top: 0; width: 192px; height: 110px; background: ${C.surface}; border: 1px solid ${C.ruleHard}; border-radius: 2px; display: flex; flex-direction: column; align-items: center; gap: 0; padding: 12px 14px">
    <span style="font-family: ${SERIF}; font-size: 8.5px; letter-spacing: 0.22em; text-transform: uppercase; color: ${C.ink3}">Final edition</span>
    <div style="width: 100%; height: 4px; border-bottom: 4px double ${C.ruleHard}; margin: 8px 0 11px"></div>
    <span style="display: inline-block; border: 3px solid ${C.good}; color: ${C.good}; border-radius: 3px; padding: 4px 13px; transform: rotate(-4deg); font-family: ${SERIF}; font-size: 16px; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase">Won</span>
    <span style="margin-top: 12px; font-family: ${SERIF}; font-size: 26px; font-weight: 700; color: ${C.ink}">12,480</span>
  </div>
</div>`;

const sheetPreview = `<div style="width: 236px; background: ${C.surface}; border-top: 1px solid ${C.rule}; border-radius: 16px 16px 0 0; padding: 8px 16px 20px; display: flex; flex-direction: column; align-items: center; gap: 0">
  <div style="width: 38px; height: 4px; border-radius: 4px; background: ${C.rule}; margin: 4px 0 14px"></div>
  <span style="font-family: ${SERIF}; font-size: 19px; font-weight: 700; color: ${C.ink}">Paused</span>
  <span style="margin-top: 3px; font-family: ${SERIF}; font-size: 10px; letter-spacing: 0.12em; text-transform: uppercase; color: ${C.ink3}">Spot Kick</span>
</div>`;

const stripPreview = `<div style="width: 250px; background: #0d1018; padding: 12px 14px; display: flex; align-items: center; gap: 12px">
  <span style="width: 34px; height: 34px; border-radius: 8px; background: rgba(255,255,255,.12); display: flex; align-items: center; justify-content: center; flex: none">${glyph('Pause', 17, '#ffffff')}</span>
  <span style="font-family: ${MONO}; font-size: 12px; letter-spacing: 0.1em; color: rgba(255,255,255,.75)">SPOT KICK</span>
</div>`;

const launchPreview = `<div style="width: 236px; background: #06070a; padding: 24px 18px; display: flex; flex-direction: column; align-items: center; gap: 14px">
  <span style="font-family: ${MONO}; font-size: 11px; letter-spacing: 0.12em; text-transform: uppercase; color: rgba(255,255,255,.55)">Building</span>
  <div style="width: 190px; height: 3px; border-radius: 3px; background: #24262e; overflow: hidden">
    <div style="width: 118px; height: 3px; border-radius: 3px; background: ${C.accent}"></div>
  </div>
</div>`;

const rulesPreview = `<div style="width: 250px; display: flex; flex-direction: column; gap: 0">
  <div style="display: flex; align-items: center; justify-content: space-between; padding-bottom: 7px; border-bottom: 4px double ${C.ruleHard}">
    <span style="font-family: ${SERIF}; font-size: 10.5px; font-weight: 800; letter-spacing: 0.2em; text-transform: uppercase; color: ${C.ink}">More editions</span>
  </div>
  <div style="display: flex; border-top: 1px solid ${C.rule}; border-bottom: 1px solid ${C.rule}; margin-top: 20px">
    ${[['4', 'Plays'], ['12,480', 'Best'], ['31 Jul', 'Last']]
      .map(
        ([v, l], i) => `<div style="flex: 1; text-align: center; padding: 11px 4px; ${i ? `border-left: 1px solid ${C.rule}` : ''}">
      <b style="display: block; font-family: ${SERIF}; font-size: 17px; font-weight: 700; color: ${C.ink}">${v}</b>
      <span style="font-family: ${SERIF}; font-size: 9px; letter-spacing: 0.1em; text-transform: uppercase; color: ${C.ink3}">${l}</span>
    </div>`
      )
      .join('')}
  </div>
</div>`;

const cardPreview = `<div style="display: grid; grid-template-columns: 1fr 1fr; gap: 0; width: 250px">
  ${[0, 1]
    .map(
      (i) => `<div style="display: flex; flex-direction: column; padding: 0 0 8px; ${i === 0 ? `padding-right: 14px; border-right: 1px solid ${C.rule}` : 'padding-left: 14px'}">
    <div style="width: 100%; aspect-ratio: 16 / 10; background: ${C.sunk}; margin-bottom: 8px"></div>
    <span style="font-family: ${SERIF}; font-size: 13px; font-weight: 700; line-height: 1.18; color: ${C.ink}">Heatwave Pushes The Grid</span>
    <span style="margin-top: 6px; font-family: ${SERIF}; font-size: 8.5px; letter-spacing: 0.11em; text-transform: uppercase; color: ${C.ink3}">30 Jul</span>
  </div>`
    )
    .join('')}
</div>`;

const recipes = [
  recipe(
    'LetterpressButton',
    chip('7.3 &middot; Selectable', 'accent'),
    letterpressPreview,
    [
      layer('HitTarget', '&mdash;', 'Invisible Graphic, 44&times;44 minimum, <b>never</b> animated. The only thing in the prefab that raycasts.'),
      layer('Plate', 'Accent', 'The ledge. Offset +4, &minus;4 units behind the face &mdash; the depth a press consumes.'),
      layer('Plate', 'ButtonFace', 'The face. DOTween to +4, &minus;4 over Motion.ButtonPress (80&thinsp;ms), then back.'),
      layer('TMP label', 'ButtonInk', 'TextStyle <b>ButtonLabel</b> &mdash; 13&thinsp;u, 800, upper, +14 tracking.')
    ],
    'Disabled fades a CanvasGroup rather than swapping sprites. <code style="font-family: ' + MONO + '; font-size: 11px">Navigation = None</code>; the EventSystem still gives slide-off-cancel for free.'
  ),
  recipe(
    'Button variants',
    chip('7.2 inventory', 'quiet'),
    pressedPreview,
    [
      layer('PlateLine', 'RuleHard', 'Outline variant &mdash; the archive button. 1.5&thinsp;u keyline, ledge tinted Rule.'),
      layer('Plate', 'Sunk', 'Quiet variant &mdash; a sheet button with no ledge at all.'),
      layer('Plate + Icon*', 'ButtonFace', 'Icon variant &mdash; face plus a 24&thinsp;u glyph, no label.'),
      layer('&mdash; pressed &mdash;', '&mdash;', 'The face sits on the ledge; the ledge is gone because the face is standing on it.')
    ],
    null
  ),
  recipe(
    'Played badge',
    chip('7.5 &middot; art only', 'ink'),
    playedPreview,
    [
      layer('art Image', 'ArtBackground', 'Alpha to 0.5 (light) / 0.42 (dark). The fade goes on the <b>art</b>, never its container.'),
      layer('Disc', 'Ink', '18&thinsp;u, inset 6&thinsp;u from the art\'s top-left corner.'),
      layer('IconTick', 'Paper', '18&thinsp;u inside the disc, at full opacity &mdash; the badge is what stops grey reading as "failed to load".')
    ],
    'Detail art takes the same badge at 24&thinsp;u, an archive row at 15&thinsp;u.'
  ),
  recipe(
    'PLAY stamp',
    chip('11.7 &middot; hero', 'accent'),
    stampPreview,
    [
      layer('StampFrame', 'Accent', '2.5&thinsp;u keyline, radius 3&thinsp;u. Rotate the RectTransform &minus;8&deg;.'),
      layer('TMP label', 'Accent', 'TextStyle <b>Stamp</b> &mdash; 11&thinsp;u, 800, upper, +18 tracking.')
    ],
    'Scale-slams from 2.4&times; on enter. Struck on unplayed lead art only; the played hero shows the tick and "Played today" instead.'
  ),
  recipe(
    'Result slip',
    chip('8.6 &middot; set piece', 'accent'),
    slipPreview,
    [
      layer('Plate', 'Accent', 'The slip\'s ledge &mdash; 8&thinsp;u offset here, twice a button\'s.'),
      layer('Plate', 'Surface', 'The slip ground.'),
      layer('PlateHairline', 'RuleHard', '1&thinsp;u keyline over the ground.'),
      layer('RuleDouble', 'RuleHard', '4&thinsp;u tall, under the kicker.'),
      layer('VerdictFrame', 'Good / Bad', '3&thinsp;u keyline, rotate &minus;4&deg;, Motion.VerdictStamp.')
    ],
    'The odometer wraps its digits in <code style="font-family: ' + MONO + '; font-size: 11px">&lt;mspace&gt;</code> while counting &mdash; TMP has no tabular figures.'
  ),
  recipe(
    'SheetFrame',
    chip('3.6 &middot; overlay', 'ink'),
    sheetPreview,
    [
      layer('Fill', 'black &alpha;.72', 'The scrim, full-bleed outside the safe area.'),
      layer('SheetTop', 'Surface', '16&thinsp;u top corners, square bottom &mdash; it sits flush on the screen edge.'),
      layer('PillSmall', 'Rule', 'The grab handle, 38&thinsp;&times;&thinsp;4&thinsp;u.')
    ],
    'Overlays are not screens and never enter the back stack. Their tweens run unscaled &mdash; they animate at <code style="font-family: ' + MONO + '; font-size: 11px">timeScale = 0</code>.'
  ),
  recipe(
    'Search field and rows',
    chip('6.4 &middot; archive', 'quiet'),
    searchPreview,
    [
      layer('Field', 'Sunk', 'Radius 8&thinsp;u. The one rounded surface in the shell.'),
      layer('IconSearch', 'InkTertiary', '20&thinsp;u, leading the field.'),
      layer('Disc + IconClose', 'InkTertiary / Paper', 'The clear button, 18&thinsp;u, in its own 44&thinsp;u HitTarget.'),
      layer('Fill', 'Rule', 'The 1&thinsp;u rule under each ArchiveRow.'),
      layer('IconChevronRight', 'InkTertiary', 'Row disclosure, 16&thinsp;u at 50% alpha.')
    ],
    null
  ),
  recipe(
    'Toggle and segments',
    chip('11.8 &middot; settings', 'quiet'),
    togglePreview,
    [
      layer('Pill', 'Sunk &rarr; Good', '46&thinsp;&times;&thinsp;28&thinsp;u track. Never render Pill below 16&thinsp;u tall.'),
      layer('Disc', 'Paper', '22&thinsp;u knob, travels 18&thinsp;u.'),
      layer('Segment', 'Sunk', 'Radius 6&thinsp;u track, 2&thinsp;u padding.'),
      layer('Chip', 'Paper', 'Radius 4&thinsp;u thumb.')
    ],
    'The theme row is where dark mode (#575) lands. No sound or haptics row &mdash; v1 has nothing to turn down.'
  ),
  recipe(
    'Section rules and stat band',
    chip('6.1 / 7.1', 'ink'),
    rulesPreview,
    [
      layer('RuleDouble', 'RuleHard', 'Under a SectionHeader. Pin to 4&thinsp;u tall; stretch horizontally only.'),
      layer('Fill', 'RuleHard', 'The 3&thinsp;u masthead rule and the 1&thinsp;u folio rule.'),
      layer('Fill', 'Rule', 'Stat band top and bottom hairlines, and the 1&thinsp;u dividers between cells.')
    ],
    'The stat band stays inline in the detail screen by fiat (7.1) &mdash; no reuse is expected, so it earns no prefab.'
  ),
  recipe(
    'GameCard grid',
    chip('6.3 &middot; feed', 'quiet'),
    cardPreview,
    [
      layer('Fill', 'ArtBackground', 'The art ground, 16:10, behind the generated placeholder (7.6).'),
      layer('Fill', 'Rule', 'The column rule &mdash; 1&thinsp;u, owned by the <b>grid</b>, not the cell.'),
      layer('Fill', 'Rule', 'The 1&thinsp;u rule above every row after the first.')
    ],
    'A newspaper grid is defined by the rules between cells, so the gutter is zero and the rules do the spacing. A trailing odd card must not hang a rule with nothing beside it.'
  ),
  recipe(
    'GameStrip',
    chip('10.7 &middot; in game', 'ink'),
    stripPreview,
    [
      layer('Fill', 'black', 'The strip ground, full-bleed, safe-area-padded content.'),
      layer('Field', 'white &alpha;.12', '34&thinsp;u pause plate.'),
      layer('IconPause', 'white', '20&thinsp;u glyph in a 44&thinsp;u HitTarget.'),
      layer('TMP title', 'white &alpha;.75', 'Monospace &mdash; the machine room does not speak serif (5.7).')
    ],
    'Right side is reserved-empty. Contended real estate: validate it against a real game (#573).'
  ),
  recipe(
    'Launch overlay',
    chip('10.6 &middot; states', 'ink'),
    launchPreview,
    [
      layer('Fill', '#06070a', 'Full-bleed black, outside the safe area.'),
      layer('PillSmall', 'white &alpha;.12', '190&thinsp;&times;&thinsp;3&thinsp;u progress track.'),
      layer('PillSmall', 'Accent', 'The fill, driven over Motion.LaunchProgress.'),
      layer('TMP state', 'white &alpha;.55', 'Cached / Downloading / Parsing / Building / Failed, in mono.')
    ],
    null
  )
];

writeFileSync(
  join(here, 'Assembly.dc.html'),
  doc(
    `code { font-family: ${MONO}; }
    b { font-weight: 700; }`,
    `<div style="background: ${C.paper}; min-height: 100%; padding: 34px 36px 44px">
  ${masthead('Assembly', 'Sprites &rarr; the 7.2 inventory')}

  <p style="margin: 0 0 26px; font-family: ${SERIF}; font-size: 14px; line-height: 1.6; color: ${C.ink2}; max-width: 84ch; text-wrap: pretty">
    Each recipe lists its layers back to front, with the colour role each one binds. Two rules cut across all of them: <b>nothing raycasts except things named HitTarget</b> (7.4), so every decorative Graphic sets <code style="font-size: 11px">raycastTarget = false</code>; and every tween takes <code style="font-size: 11px">SetLink(gameObject)</code> (8.2), because cached screens deactivate constantly and an unlinked tween completing invisibly corrupts the next OnEnter.
  </p>

  <div style="display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 24px">
    ${recipes.join('\n    ')}
  </div>
</div>`
  )
);

// =============================================================================
// canvas.json
// =============================================================================
writeFileSync(
  join(here, 'canvas.json'),
  `${JSON.stringify(
    {
      artboards: [
        { file: 'Main.dc.html', x: 0, y: 0, w: 1104, h: 1010 },
        { file: 'Icons.dc.html', x: 1224, y: 0, w: 940, h: 1290 },
        { file: 'SliceTable.dc.html', x: 2264, y: 0, w: 1180, h: 1950 },
        { file: 'Assembly.dc.html', x: 0, y: 1420, w: 1320, h: 2530 }
      ],
      annotations: [
        {
          id: 'ground-note',
          x: 0,
          y: -190,
          w: 520,
          text:
            'The sheet is drawn white on transparent. It is shown here on a dark ground only so the sprites are visible — the shipped PNG has a real alpha channel and no ground at all.'
        },
        {
          id: 'files-note',
          x: 600,
          y: -190,
          w: 500,
          text:
            'Generated alongside this canvas:\nUIAtlas.png — 1024×512, white on transparent\nUIAtlas.slices.json — the same rects and borders as data\nboth in Prototypes/ui-atlas/.'
        }
      ],
      launch: { view: 'canvas' }
    },
    null,
    2
  )}\n`
);

console.log(`artboards: Main, Icons, SliceTable, Assembly + canvas.json (${SPRITES.length} sprites, ${iconNames.length} icons)`);

// The shell's UI sprite atlas — geometry and slice data.
//
// One source of truth: build-atlas.mjs rasterises it, build-artboards.mjs documents it.
//
// Everything is drawn WHITE on transparent at 4x its canvas-unit size (UIPLAN 2.3:
// "bitmap art authored at 3-4x its unit size"), so the sheet imports at PixelsPerUnit 4
// and every graphic takes its colour from a ThemeColor role binder at runtime (UIPLAN 5.2).
// That is also what makes dark mode (#575) a second theme asset rather than a second atlas.
//
// Outputs:
//   UIAtlas.svg            the sheet, white on transparent
//   UIAtlas.slices.json    rect + 9-slice border table
//   atlas-plain.frag.svg   sheet fragment for embedding in the design canvas
//   atlas-noted.frag.svg   the same, with rect outlines and names

export const SHEET_W = 1024;
export const SHEET_H = 512;
export const PPU = 4; // px per canvas unit

// ---------------------------------------------------------------------------
// geometry helpers, all in sheet pixels
// ---------------------------------------------------------------------------
const rr = (x, y, w, h, r) =>
  `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" ry="${r}" fill="#fff"/>`;

// A stroked round-rect: the stroke sits INSIDE the rect, so the sprite's own
// bounds are the element's bounds and the 9-slice border maths stays honest.
const rrLine = (x, y, w, h, r, s) =>
  `<rect x="${x + s / 2}" y="${y + s / 2}" width="${w - s}" height="${h - s}" rx="${Math.max(0, r - s / 2)}" ry="${Math.max(0, r - s / 2)}" fill="none" stroke="#fff" stroke-width="${s}"/>`;

// Top-two-corners-only round rect (the pause sheet's chrome).
const rrTop = (x, y, w, h, r) =>
  `<path d="M${x} ${y + h} L${x} ${y + r} A${r} ${r} 0 0 1 ${x + r} ${y} L${x + w - r} ${y} A${r} ${r} 0 0 1 ${x + w} ${y + r} L${x + w} ${y + h} Z" fill="#fff"/>`;

// The 4px-double section rule: bands of 1u / 2u / 1u, baked at natural height so a
// 9-slice with zero vertical border reproduces CSS `border-bottom: 4px double`.
const ruleDouble = (x, y, w, h) => {
  const unit = h / 4;
  return rr(x, y, w, unit, 0) + rr(x, y + unit * 3, w, unit, 0);
};

// ---------------------------------------------------------------------------
// icons — authored on a 24-unit grid, 2u stroke, round caps and joins, then
// scaled x4 into a 96px cell. Line icons are stroked; the three badge glyphs
// (pause, play, sparkle) are filled, because a 2u stroke closes up at 13u.
// ---------------------------------------------------------------------------
export const ICON_GRID = 24;
export const ICON_PX = 96;
export const STROKE = 2; // grid units

export const strokeAttrs = `fill="none" stroke="#fff" stroke-width="${STROKE}" stroke-linecap="round" stroke-linejoin="round"`;

// A four-point sparkle, tips clockwise from the top. `waist` pulls each edge in
// toward the centre — that concave pull is what makes it a sparkle and not a
// diamond — and `spread` sets how wide a tip opens before the round join blunts
// it. The tips are rounded by a round-joined stroke of the fill colour, so the
// silhouette grows by half the stroke: keep R + strokeWidth / 2 under 10 or the
// glyph leaves the 2-unit safe area.
const sparklePath = (R, waist, spread, C = 12) => {
  const dirs = [[0, -1], [1, 0], [0, 1], [-1, 0]];
  const f = (n) => Number(n.toFixed(2));
  const tips = dirs.map(([dx, dy]) => [C + dx * R, C + dy * R]);

  let out = `M${f(tips[0][0])} ${f(tips[0][1])}`;

  for (let i = 0; i < 4; i++) {
    const d1 = dirs[i];
    const d2 = dirs[(i + 1) % 4];
    const end = tips[(i + 1) % 4];
    const c1 = [C + d1[0] * R * waist + d2[0] * spread, C + d1[1] * R * waist + d2[1] * spread];
    const c2 = [C + d2[0] * R * waist + d1[0] * spread, C + d2[1] * R * waist + d1[1] * spread];
    out += ` C${f(c1[0])} ${f(c1[1])}, ${f(c2[0])} ${f(c2[1])}, ${f(end[0])} ${f(end[1])}`;
  }

  return `${out} Z`;
};

export const ICONS = {
  // masthead -> archive
  Search: `<circle cx="10.6" cy="10.6" r="6.4" ${strokeAttrs}/><path d="M15.3 15.3 L20.2 20.2" ${strokeAttrs}/>`,

  // masthead -> settings. Three ruled lines with a handle on each, not a gear: a
  // toothed outline fills in solid at a 2u stroke on a 24u grid, and the settings
  // screen is three controls anyway. Ruled lines are the app's own vocabulary.
  Settings: [
    { y: 6.8, cx: 13.4 },
    { y: 12, cx: 9.6 },
    { y: 17.2, cx: 14.6 }
  ].map(({ y, cx }) => {
    const gap = 2.9;
    const left = `<path d="M4 ${y} L${(cx - gap).toFixed(2)} ${y}" ${strokeAttrs}/>`;
    const right = `<path d="M${(cx + gap).toFixed(2)} ${y} L20 ${y}" ${strokeAttrs}/>`;
    return `${left}${right}<circle cx="${cx}" cy="${y}" r="1.9" ${strokeAttrs}/>`;
  }).join(''),

  // back button
  ChevronLeft: `<path d="M15 4.6 L8.4 12 L15 19.4" ${strokeAttrs}/>`,

  // archive row + settings row disclosure
  ChevronRight: `<path d="M9 4.6 L15.6 12 L9 19.4" ${strokeAttrs}/>`,

  // the played mark, struck inside an 18u disc
  Tick: `<path d="M5 12.7 L9.9 17.6 L19 7.2" ${strokeAttrs}/>`,

  // clears the archive search field
  Close: `<path d="M6.6 6.6 L17.4 17.4" ${strokeAttrs}/><path d="M17.4 6.6 L6.6 17.4" ${strokeAttrs}/>`,

  // the only chrome the shell imposes on a running game (UIPLAN 10.7)
  Pause: `<rect x="7.4" y="4.8" width="3.4" height="14.4" rx="1.4" fill="#fff"/><rect x="13.2" y="4.8" width="3.4" height="14.4" rx="1.4" fill="#fff"/>`,

  // "Next game" on the result slip
  Play: `<path d="M8.2 5.1 L19 12 L8.2 18.9 Z" fill="#fff" stroke="#fff" stroke-width="1.4" stroke-linejoin="round"/>`,

  // "New best" on the result slip
  Sparkle: `<path d="${sparklePath(8.7, 0.3404, 0.9)}" fill="#fff" stroke="#fff" stroke-width="1.6" stroke-linejoin="round" stroke-linecap="round"/>`
};

export const icon = (name, x, y) =>
  `<g transform="translate(${x} ${y}) scale(${ICON_PX / ICON_GRID})">${ICONS[name]}</g>`;

// ---------------------------------------------------------------------------
// THE SHEET
//
// border is [left, bottom, right, top] — the order Unity's Sprite Editor and
// SpriteMetaData.border use.
// ---------------------------------------------------------------------------
export const SPRITES = [
  // ---- band 1: 9-sliced plates and frames -------------------------------
  {
    name: 'Plate', x: 16, y: 32, w: 128, h: 128, border: [9, 9, 9, 9], mode: 'Sliced',
    role: 'ButtonFace / Surface / Offset',
    use: 'LetterpressButton plate and face, sheet buttons, the result slip body, feed art frames. Corner radius 2u.',
    draw: (s) => rr(s.x, s.y, s.w, s.h, 8)
  },
  {
    name: 'PlateLine', x: 160, y: 32, w: 128, h: 128, border: [11, 11, 11, 11], mode: 'Sliced',
    role: 'RuleHard / Rule',
    use: 'The outlined "Open the archive" button and the played play-button (1.5u keyline, radius 2u).',
    draw: (s) => rrLine(s.x, s.y, s.w, s.h, 8, 6)
  },
  {
    name: 'PlateHairline', x: 304, y: 32, w: 128, h: 128, border: [10, 10, 10, 10], mode: 'Sliced',
    role: 'Rule / RuleHard',
    use: 'The result slip border and the how-to-play block (1u keyline, radius 2u).',
    draw: (s) => rrLine(s.x, s.y, s.w, s.h, 8, 4)
  },
  {
    name: 'StampFrame', x: 448, y: 32, w: 128, h: 128, border: [17, 17, 17, 17], mode: 'Sliced',
    role: 'Accent',
    use: 'The rubber PLAY stamp struck on unplayed lead art (2.5u keyline, radius 3u). Rotate the RectTransform -8 degrees.',
    draw: (s) => rrLine(s.x, s.y, s.w, s.h, 12, 10)
  },
  {
    name: 'VerdictFrame', x: 592, y: 32, w: 128, h: 128, border: [18, 18, 18, 18], mode: 'Sliced',
    role: 'Good / Bad',
    use: 'The stamped verdict on the result slip (3u keyline, radius 3u). Rotate -4 degrees; scale-slam per Motion.VerdictStamp.',
    draw: (s) => rrLine(s.x, s.y, s.w, s.h, 12, 12)
  },
  {
    name: 'Field', x: 736, y: 32, w: 128, h: 128, border: [33, 33, 33, 33], mode: 'Sliced',
    role: 'Sunk',
    use: 'The archive search field and the game strip pause button (radius 8u).',
    draw: (s) => rr(s.x, s.y, s.w, s.h, 32)
  },
  {
    name: 'Segment', x: 880, y: 32, w: 96, h: 96, border: [25, 25, 25, 25], mode: 'Sliced',
    role: 'Sunk',
    use: 'The settings theme segmented-control track (radius 6u).',
    draw: (s) => rr(s.x, s.y, s.w, s.h, 24)
  },

  // ---- band 2: sheets, chips, discs, pills, rules ------------------------
  {
    name: 'SheetTop', x: 16, y: 192, w: 160, h: 160, border: [65, 4, 65, 65], mode: 'Sliced',
    role: 'Surface',
    use: 'SheetFrame — the pause sheet chrome. Top corners 16u, bottom square, so it can sit flush on the safe-area edge.',
    draw: (s) => rrTop(s.x, s.y, s.w, s.h, 64)
  },
  {
    name: 'Chip', x: 208, y: 192, w: 64, h: 64, border: [17, 17, 17, 17], mode: 'Sliced',
    role: 'ButtonFace / Paper',
    use: 'The archive filter chips and the segmented-control thumb (radius 4u).',
    draw: (s) => rr(s.x, s.y, s.w, s.h, 16)
  },
  {
    name: 'Disc', x: 288, y: 192, w: 96, h: 96, border: [0, 0, 0, 0], mode: 'Simple',
    role: 'Ink / Accent / Paper',
    use: 'The played tick badge (18u), the toggle knob (22u), the how-to-play bullet and the staging pulse dot (5u). Simple mode, Preserve Aspect on.',
    draw: (s) => `<circle cx="${s.x + s.w / 2}" cy="${s.y + s.h / 2}" r="${s.w / 2}" fill="#fff"/>`
  },
  {
    name: 'Pill', x: 400, y: 192, w: 128, h: 64, border: [31, 31, 31, 31], mode: 'Sliced',
    role: 'Sunk / Good',
    use: 'The dev-mode toggle track (46 x 28u). Fully round; never render it below 16u tall.',
    draw: (s) => rr(s.x, s.y, s.w, s.h, 32)
  },
  {
    name: 'PillSmall', x: 560, y: 192, w: 24, h: 12, border: [5, 5, 5, 5], mode: 'Sliced',
    role: 'Rule / Accent',
    use: 'The pause sheet grab handle (38 x 4u) and the launch overlay progress track and fill (3u). Never below 3u tall.',
    draw: (s) => rr(s.x, s.y, s.w, s.h, 6)
  },
  {
    name: 'RuleDouble', x: 656, y: 192, w: 16, h: 16, border: [7, 0, 7, 0], mode: 'Sliced',
    role: 'RuleHard',
    use: 'The double rule under a SectionHeader and over the slip kicker. Bands are 1u / 2u / 1u — stretch horizontally only, height fixed at 4u.',
    draw: (s) => ruleDouble(s.x, s.y, s.w, s.h)
  },
  {
    name: 'Fill', x: 752, y: 192, w: 16, h: 16, border: [0, 0, 0, 0], mode: 'Simple',
    role: 'any',
    use: 'Every square surface: hairlines, the masthead rule, the letterpress ledge, stat-band dividers, column rules, the overlay scrim, full-bleed grounds.',
    draw: (s) => rr(s.x, s.y, s.w, s.h, 0)
  },

  // ---- band 3: icons -----------------------------------------------------
  ...['Search', 'Settings', 'ChevronLeft', 'ChevronRight', 'Tick', 'Close', 'Pause', 'Play', 'Sparkle'].map(
    (name, i) => ({
      name: `Icon${name}`,
      x: 16 + i * 112, y: 384, w: 96, h: 96,
      border: [0, 0, 0, 0], mode: 'Simple',
      role: {
        Search: 'Ink', Settings: 'Ink', ChevronLeft: 'Ink', ChevronRight: 'InkTertiary',
        Tick: 'Paper', Close: 'Paper', Pause: 'OnAccent', Play: 'ButtonInk', Sparkle: 'Good'
      }[name],
      use: {
        Search: 'Masthead action -> Archive.',
        Settings: 'Masthead action -> Settings.',
        ChevronLeft: 'The back button, which names the screen beneath the top of the stack (UIPLAN 3.3).',
        ChevronRight: 'Archive row and settings row disclosure.',
        Tick: 'The played mark inside an 18u Disc — full opacity while the art behind it fades (UIPLAN 7.5).',
        Close: 'Clears the archive search field.',
        Pause: 'The game strip pause glyph, in a 44u HitTarget (UIPLAN 10.7).',
        Play: '"Next game" on the result slip.',
        Sparkle: '"New best" on the result slip.'
      }[name],
      icon: name,
      draw: (s) => icon(s.icon, s.x, s.y)
    })
  )
];


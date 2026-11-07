import { Application, Container, Graphics, Sprite, Text, Texture, type FederatedPointerEvent } from 'pixi.js';
import type { BarrierView, Ping, SceneGrid, SceneTemplate, SceneView, TemplateShapeName, TokenView } from '../../../api/tableTypes';
import { barrierLook, barrierState } from './barriers';
import { cellToPx, formatMeters, gridDistance, pxToCell, snapCell, type Point } from '../grid';
import { templateShape } from '../templates';
import { initials, tokenStatus, type TokenFighter } from '../tokens';
import { HOLD_MS, inRect, MOVE_TOLERANCE, rectFrom, toggleSelection } from './gestures';
import { missPoint, type AttackEffect } from './effects';
import { spriteFit } from '../../../lib/portraitFocus';
import { arcPositions, drawInjuryIcon, injuryIcon } from './injuryIcons';

export type BoardTool = 'select' | 'ruler' | 'template' | 'token';

/** Where a moved token or area ends up, in cells. */
export interface BoardMove { id: string; x: number; y: number }

export interface TemplateDraft { shape: TemplateShapeName; sizeMeters: number; color: string }

export interface BoardData {
  scene: SceneView;
  /** Fighters of the linked fight by combatant id, as this viewer may see them. */
  fighters: Map<string, TokenFighter>;
  activeCombatantId: string | null;
  selectedTokens: readonly string[];
  selectedTemplates: readonly string[];
  /** Whether this viewer may drag a template (the GM: unlocked ones; a player: any player's). */
  canMoveTemplate: (t: SceneTemplate) => boolean;
  /** The GM sees hidden tokens dimmed. */
  gm: boolean;
  lang: 'pt' | 'en';
}

export interface BoardCallbacks {
  /** The selection changed (a click, Shift+click or the lasso). */
  onSelect: (tokens: string[], templates: string[]) => void;
  onTokenDrag: (id: string, x: number, y: number) => void;
  /** A drag of one or more tokens and areas ended. */
  onMoveDrop: (tokens: BoardMove[], templates: BoardMove[]) => void;
  onTemplatePlace: (x: number, y: number, angle: number) => void;
  onPing: (x: number, y: number) => void;
  /** With the 'token' tool: the cell a player picked for their own token. */
  onTokenPlace?: (x: number, y: number) => void;
}

const FONT = 'Rajdhani, "Segoe UI", sans-serif';
const MIN_ZOOM = 0.08;
const MAX_ZOOM = 4;
/** Cells shown when the scene has no map yet. */
const EMPTY_COLS = 30;
const EMPTY_ROWS = 20;
const ACTIVE_COLOR = 0xf5e663;
const PING_MS = 2200;
const TRACER = 0xffcf3a;
const BLOOD = 0xff2a3d;
const FLASH = 0xfff3b0;
const FIRE = 0xff8a00;

const hex = (color: string) => Number.parseInt(color.replace('#', ''), 16) || 0x00f0ff;

/** Images load through an <img>: the API's media URLs have no file extension for Pixi's own loader to go by. */
const textures = new Map<string, Promise<Texture>>();
function loadTexture(url: string): Promise<Texture> {
  let texture = textures.get(url);
  if (!texture) {
    texture = new Promise<Texture>((resolve, reject) => {
      const img = new Image();
      img.onload = () => resolve(Texture.from(img));
      img.onerror = () => { textures.delete(url); reject(new Error(`image ${url}`)); };
      img.src = url;
    });
    textures.set(url, texture);
  }
  return texture;
}

interface Override { x: number; y: number; until: number }

/** What a press landed on. */
interface Target { tokenId?: string; template?: SceneTemplate }

/**
 * The virtual table's board, drawn with PixiJS (WebGL). It owns pan and zoom, token and template drags, the ruler, the
 * template tool and pings; everything that lasts goes out through the callbacks and comes back as data.
 * World units are the map image's pixels.
 */
export class BoardController {
  private readonly app = new Application();
  private readonly world = new Container();
  private readonly backgroundLayer = new Container();
  private readonly gridLayer = new Graphics();
  private readonly sceneryLayer = new Container();
  private readonly templateLayer = new Container();
  private readonly tokenLayer = new Container();
  private readonly overlay = new Container();
  private readonly ruler = new Graphics();
  private readonly rulerText = new Text({ text: '', style: { fontFamily: FONT, fontSize: 18, fontWeight: '700', fill: 0xffffff, stroke: { color: 0x000000, width: 4 } }, resolution: 2 });
  private readonly preview = new Graphics();

  private data: BoardData | null = null;
  private tool: BoardTool = 'select';
  private draft: TemplateDraft = { shape: 'Circle', sizeMeters: 4, color: '#ff2a6d' };
  private backgroundUrl: string | null = null;
  private tokens = new Map<string, Container>();
  private overrides = new Map<string, Override>();
  private activeRings: Graphics[] = [];
  private pings: { node: Container; born: number }[] = [];
  /** Attacks being drawn: each redraws itself by age (0 to 1) until it ends. */
  private effects: { node: Container; born: number; duration: number; update: (age: number) => void }[] = [];
  private effectCount = 0;
  private ready = false;
  private destroyed = false;
  private fitted = false;

  /**
   * What the pointer is doing between down and up. A press waits to see what it becomes: a click (select), a drag
   * (move the selection, or a lasso on empty ground; a finger pans instead), or a hold (ping).
   */
  private gesture:
    | { kind: 'pan'; start: Point; origin: Point }
    | { kind: 'press'; target: Target | null; screen: Point; cell: Point; additive: boolean; touch: boolean; timer: number }
    | { kind: 'group'; start: Point; tokens: { id: string; x: number; y: number; size: number }[]; templates: { id: string; x: number; y: number }[] }
    | { kind: 'lasso'; start: Point; additive: boolean }
    | { kind: 'pinch' }
    | { kind: 'done' }
    | { kind: 'ruler'; from: Point }
    | { kind: 'place'; origin: Point }
    | null = null;
  /** Fingers on the board, for the two-finger pinch. */
  private touches = new Map<number, Point>();
  private pinch: { distance: number; mid: Point; scale: number; origin: Point } | null = null;
  private readonly lasso = new Graphics();

  private resizer: ResizeObserver | null = null;

  constructor(private readonly host: HTMLElement, private readonly callbacks: BoardCallbacks) {}

  async init() {
    await this.app.init({
      resizeTo: this.host, background: 0x050607, antialias: true, autoDensity: true, resolution: window.devicePixelRatio || 1,
    });
    if (this.destroyed) { this.app.destroy(true, { children: true }); return; }
    this.host.appendChild(this.app.canvas);
    this.app.stage.eventMode = 'static';
    this.app.stage.hitArea = this.app.screen;
    this.app.stage.addChild(this.world);
    // Scenery (barriers, cars) over the map and under the areas and the characters.
    this.world.addChild(this.backgroundLayer, this.gridLayer, this.sceneryLayer, this.templateLayer, this.tokenLayer, this.overlay);
    this.overlay.addChild(this.preview, this.ruler, this.rulerText, this.lasso);
    this.app.stage.on('pointerdown', (e) => this.onDown(e, null));
    this.app.stage.on('globalpointermove', (e) => this.onMove(e));
    this.app.stage.on('pointerup', (e) => this.onUp(e));
    this.app.stage.on('pointerupoutside', (e) => this.onUp(e));
    this.app.canvas.addEventListener('wheel', this.onWheel, { passive: false });
    this.app.canvas.addEventListener('contextmenu', (e) => e.preventDefault());
    this.app.ticker.add(() => this.animate());
    // resizeTo only follows the window: a panel dragged wider or hidden resizes the host too.
    this.resizer = new ResizeObserver(() => this.app.resize());
    this.resizer.observe(this.host);
    this.ready = true;
    if (this.data) this.render();
  }

  destroy() {
    this.destroyed = true;
    if (!this.ready) return;
    this.resizer?.disconnect();
    this.app.canvas.removeEventListener('wheel', this.onWheel);
    this.app.destroy(true, { children: true });
  }

  setData(data: BoardData) {
    const sceneChanged = this.data?.scene.id !== data.scene.id;
    this.data = data;
    if (sceneChanged) {
      this.fitted = false;
      this.overrides.clear();
      for (const fx of this.effects) fx.node.destroy({ children: true });
      this.effects = [];
    }
    if (this.ready) this.render();
  }

  setTool(tool: BoardTool) {
    this.tool = tool;
    this.gesture = null;
    this.preview.clear();
    if (this.ready) this.app.canvas.style.cursor = tool === 'select' ? 'default' : 'crosshair';
  }

  setDraft(draft: TemplateDraft) { this.draft = draft; }

  /** Someone else is dragging a token: slide it there for a moment. */
  showDrag(tokenId: string, x: number, y: number) {
    if (this.gesture?.kind === 'group' && this.gesture.tokens.some((t) => t.id === tokenId)) return;
    this.overrides.set(tokenId, { x, y, until: performance.now() + 1500 });
    this.placeToken(tokenId);
  }

  showPing(ping: Ping) {
    if (!this.ready || !this.data) return;
    const g = this.data.scene.grid;
    const p = cellToPx(g, ping.x, ping.y);
    const node = new Container();
    node.position.set(p.x, p.y);
    const ring = new Graphics();
    const label = new Text({ text: ping.label, style: { fontFamily: FONT, fontSize: 16, fontWeight: '700', fill: hex(ping.color), stroke: { color: 0x000000, width: 4 } }, resolution: 2 });
    label.anchor.set(0.5, 1.6);
    node.addChild(ring, label);
    node.label = ping.color;
    this.overlay.addChild(node);
    this.pings.push({ node, born: performance.now() });
  }

  /**
   * An attack on the map: a muzzle flash and a tracer to each target (several for a burst), a blade's arc, or an
   * explosion; then sparks on a hit and the floating label (the HP lost, a hit or a miss). Without motion (the app's
   * effects switch or the system's reduced motion), only a flash and the label.
   */
  showAttack(effect: AttackEffect, motion: boolean) {
    if (!this.ready || !this.data) return;
    const g = this.data.scene.grid;
    const size = g.sizePx;
    const px = (p: Point) => cellToPx(g, p.x, p.y);
    const lerp = (a: Point, b: Point, k: number): Point => ({ x: a.x + (b.x - a.x) * k, y: a.y + (b.y - a.y) * k });
    const clamp = (v: number) => Math.max(0, Math.min(1, v));
    const from = px(effect.from);
    const targets = effect.shots.map((s) => px(s.to));
    const ends = effect.shots.map((s, i) => (s.hit || effect.kind !== 'shot' && effect.kind !== 'burst' ? targets[i] : px(missPoint(effect.from, s.to))));

    const node = new Container();
    const lines = new Graphics();
    node.addChild(lines);
    const labels = effect.shots.map((s, i) => {
      const text = new Text({
        text: s.label,
        style: { fontFamily: FONT, fontSize: Math.max(18, size * 0.4), fontWeight: '700', fill: s.hit ? BLOOD : 0xd0d6de, stroke: { color: 0x000000, width: 4 } },
        resolution: 2,
      });
      text.anchor.set(0.5, 1);
      text.position.set(targets[i].x, targets[i].y - size * 0.45);
      text.alpha = 0;
      node.addChild(text);
      return text;
    });
    this.overlay.addChild(node);

    const sparks = (p: Point, k: number) => {
      for (let n = 0; n < 8; n++) {
        const angle = (n / 8) * Math.PI * 2 + 0.3;
        const r0 = size * 0.15 * k, r1 = size * (0.2 + 0.45 * k);
        lines.moveTo(p.x + Math.cos(angle) * r0, p.y + Math.sin(angle) * r0).lineTo(p.x + Math.cos(angle) * r1, p.y + Math.sin(angle) * r1);
      }
      lines.stroke({ width: 3, color: BLOOD, alpha: 1 - k });
    };

    const update = (age: number) => {
      lines.clear();
      const impact = 0.28;
      if (motion) {
        if ((effect.kind === 'shot' || effect.kind === 'burst') && age < 0.14) {
          lines.circle(from.x, from.y, size * 0.35 * (1 - age / 0.14)).fill({ color: FLASH, alpha: 0.9 });
        }
        effect.shots.forEach((shot, i) => {
          const end = ends[i];
          if (effect.kind === 'shot' || effect.kind === 'burst') {
            const rounds = effect.kind === 'burst' ? 3 : 1;
            for (let r = 0; r < rounds; r++) {
              const delay = r * 0.06;
              const travel = clamp((age - delay) / 0.25);
              if (travel <= 0) continue;
              const jitter = (r - (rounds - 1) / 2) * size * 0.12;
              const dx = end.x - from.x, dy = end.y - from.y, len = Math.hypot(dx, dy) || 1;
              const off = { x: (-dy / len) * jitter, y: (dx / len) * jitter };
              const a = lerp(from, { x: end.x + off.x, y: end.y + off.y }, Math.max(0, travel - 0.35));
              const b = lerp(from, { x: end.x + off.x, y: end.y + off.y }, travel);
              lines.moveTo(a.x, a.y).lineTo(b.x, b.y).stroke({ width: 3, color: TRACER, alpha: 1 - clamp((age - 0.35) / 0.2) });
            }
          } else if (effect.kind === 'melee') {
            const sweep = clamp(age / 0.3);
            const start = Math.atan2(targets[i].y - from.y, targets[i].x - from.x) - 1.1;
            lines.arc(targets[i].x, targets[i].y, size * 0.6, start, start + 2.2 * sweep).stroke({ width: 5, color: 0xffffff, alpha: 1 - clamp((age - 0.3) / 0.25) });
          } else {
            const k = clamp(age / 0.6);
            lines.circle(targets[i].x, targets[i].y, size * (0.3 + 2 * k)).fill({ color: FIRE, alpha: (1 - k) * 0.55 });
            lines.circle(targets[i].x, targets[i].y, size * (0.5 + 2.4 * k)).stroke({ width: 4, color: FLASH, alpha: 1 - k });
          }
          if (shot.hit && age >= impact && effect.kind !== 'blast') sparks(targets[i], clamp((age - impact) / 0.35));
        });
      } else if (age < 0.5) {
        targets.forEach((p, i) => lines.circle(p.x, p.y, size * 0.5).fill({ color: effect.shots[i].hit ? BLOOD : 0xd0d6de, alpha: 0.35 * (1 - age / 0.5) }));
      }
      labels.forEach((label, i) => {
        const start = motion ? impact : 0;
        label.alpha = age < start ? 0 : age < 0.82 ? 1 : 1 - (age - 0.82) / 0.18;
        label.y = targets[i].y - size * 0.45 - (motion ? size * 0.6 * Math.max(0, age - start) : 0);
      });
    };
    update(0);
    this.effects.push({ node, born: performance.now(), duration: motion ? 1500 : 1200, update });
    // Counted on the board element, so the page (and its tests) can tell an effect played.
    this.host.dataset.effects = String(++this.effectCount);
  }

  /** Zoom to show the whole map. */
  fit() {
    if (!this.ready || !this.data) return;
    const { width, height } = this.extent();
    const view = this.app.screen;
    const scale = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, Math.min(view.width / (width + 80), view.height / (height + 80))));
    this.world.scale.set(scale);
    this.world.position.set((view.width - width * scale) / 2, (view.height - height * scale) / 2);
    this.fitted = true;
  }

  zoomBy(factor: number) {
    const view = this.app.screen;
    this.zoomAt({ x: view.width / 2, y: view.height / 2 }, factor);
  }

  // ------------------------------------------------------------------ drawing

  private extent() {
    const { scene } = this.data!;
    const g = scene.grid;
    if (scene.background.url && scene.background.width > 0) return { width: scene.background.width, height: scene.background.height };
    return { width: g.offsetX + EMPTY_COLS * g.sizePx, height: g.offsetY + EMPTY_ROWS * g.sizePx };
  }

  private render() {
    const data = this.data!;
    this.renderBackground(data.scene);
    this.renderGrid(data.scene.grid);
    this.renderTemplates(data);
    this.renderTokens(data);
    if (!this.fitted && this.app.screen.width > 0) this.fit();
  }

  private renderBackground(scene: SceneView) {
    const url = scene.background.url;
    if (url === this.backgroundUrl) return;
    this.backgroundUrl = url;
    this.backgroundLayer.removeChildren().forEach((c) => c.destroy());
    const { width, height } = this.extent();
    const floor = new Graphics().rect(0, 0, width, height).fill({ color: 0x0d0f13 });
    this.backgroundLayer.addChild(floor);
    if (!url) return;
    loadTexture(url).then((texture) => {
      if (this.destroyed || this.backgroundUrl !== url) return;
      const sprite = new Sprite(texture);
      sprite.width = scene.background.width || texture.width;
      sprite.height = scene.background.height || texture.height;
      this.backgroundLayer.addChild(sprite);
    }).catch(() => { /* broken image: the dark floor stays */ });
  }

  private renderGrid(g: SceneGrid) {
    this.gridLayer.clear();
    const { width, height } = this.extent();
    this.gridLayer.rect(0, 0, width, height).stroke({ width: 2, color: 0xff2a3d, alpha: 0.35 });
    if (g.type !== 'Square' || g.opacity <= 0) return;
    const color = hex(g.color);
    const startX = ((g.offsetX % g.sizePx) + g.sizePx) % g.sizePx;
    const startY = ((g.offsetY % g.sizePx) + g.sizePx) % g.sizePx;
    for (let x = startX; x <= width; x += g.sizePx) this.gridLayer.moveTo(x, 0).lineTo(x, height);
    for (let y = startY; y <= height; y += g.sizePx) this.gridLayer.moveTo(0, y).lineTo(width, y);
    this.gridLayer.stroke({ width: 1, color, alpha: g.opacity, pixelLine: true });
  }

  private renderTemplates(data: BoardData) {
    this.templateLayer.removeChildren().forEach((c) => c.destroy({ children: true }));
    for (const tpl of data.scene.templates) {
      const node = new Graphics();
      this.drawShape(node, data.scene.grid, tpl, hex(tpl.color), data.selectedTemplates.includes(tpl.id));
      node.eventMode = 'static';
      node.cursor = data.canMoveTemplate(tpl) ? 'move' : 'pointer';
      node.on('pointerdown', (e) => this.onDown(e, { template: tpl }));
      if (tpl.label) {
        const o = cellToPx(data.scene.grid, tpl.x, tpl.y);
        const label = new Text({ text: tpl.label, style: { fontFamily: FONT, fontSize: Math.max(16, data.scene.grid.sizePx * 0.4), fontWeight: '700', fill: hex(tpl.color), stroke: { color: 0x000000, width: 3 } }, resolution: 2 });
        label.anchor.set(0.5);
        label.position.set(o.x, o.y);
        node.addChild(label);
      }
      this.templateLayer.addChild(node);
    }
  }

  private drawShape(node: Graphics, g: SceneGrid, tpl: Pick<SceneTemplate, 'shape' | 'x' | 'y' | 'sizeMeters' | 'angle'>, color: number, selected: boolean) {
    const shape = templateShape(g, tpl);
    if (shape.kind === 'circle') node.circle(shape.cx, shape.cy, shape.r);
    else node.poly(shape.points);
    node.fill({ color, alpha: 0.18 }).stroke({ width: selected ? 4 : 2, color: selected ? 0xffffff : color, alpha: 0.9 });
  }

  private renderTokens(data: BoardData) {
    const seen = new Set<string>();
    this.activeRings = [];
    for (const token of data.scene.tokens) {
      seen.add(token.id);
      let node = this.tokens.get(token.id);
      if (!node) {
        node = new Container();
        node.eventMode = 'static';
        const id = token.id;
        node.on('pointerdown', (e) => this.onDown(e, { tokenId: id }));
        this.tokens.set(token.id, node);
      }
      const layer = token.layer === 'Scenery' ? this.sceneryLayer : this.tokenLayer;
      if (node.parent !== layer) layer.addChild(node);
      this.drawToken(node, token, data);
      this.placeToken(token.id);
    }
    for (const [id, node] of this.tokens) {
      if (seen.has(id)) continue;
      node.destroy({ children: true });
      this.tokens.delete(id);
    }
  }

  private drawToken(node: Container, token: TokenView, data: BoardData) {
    const g = data.scene.grid;
    node.removeChildren().forEach((c) => c.destroy({ children: true }));
    node.cursor = token.controllable && !token.locked ? 'grab' : 'pointer';
    const w = token.width * g.sizePx;
    const h = token.height * g.sizePx;
    const round = Math.abs(token.width - token.height) < 0.01;
    const color = hex(token.color);
    const fighter = token.combatantId ? data.fighters.get(token.combatantId) : undefined;
    const status = fighter ? tokenStatus(fighter) : null;

    if (token.rangeRingMeters) {
      const r = (token.rangeRingMeters / g.metersPerCell) * g.sizePx;
      node.addChild(new Graphics().circle(0, 0, r).fill({ color, alpha: 0.05 }).stroke({ width: 2, color, alpha: 0.6 }));
    }
    if (token.combatantId && token.combatantId === data.activeCombatantId) {
      const ring = new Graphics();
      if (round) ring.circle(0, 0, Math.max(w, h) / 2 + 8); else ring.roundRect(-w / 2 - 8, -h / 2 - 8, w + 16, h + 16, 10);
      ring.stroke({ width: 5, color: ACTIVE_COLOR });
      node.addChild(ring);
      this.activeRings.push(ring);
    }

    if (token.barrier) {
      this.drawBarrier(node, token, token.barrier, w, h, data.selectedTokens.includes(token.id));
      node.alpha = data.gm && token.visibility !== 'All' ? 0.55 : 1;
      return;
    }

    const body = new Container();
    body.rotation = (token.rotation * Math.PI) / 180;
    const inset = Math.min(w, h) * 0.06;
    const shape = (gr: Graphics) => (round ? gr.circle(0, 0, w / 2 - inset) : gr.roundRect(-w / 2 + inset, -h / 2 + inset, w - inset * 2, h - inset * 2, 8));
    body.addChild(shape(new Graphics()).fill({ color: 0x0d0f13, alpha: 0.9 }));
    if (token.imageUrl) {
      const mask = shape(new Graphics()).fill({ color: 0xffffff });
      const url = token.imageUrl;
      body.addChild(mask);
      loadTexture(url).then((texture) => {
        if (body.destroyed) return;
        const sprite = new Sprite(texture);
        // Cover the shape, keeping the picture's proportion, framed as the PC's portrait is on the sheet.
        const fit = spriteFit(texture.width, texture.height, w - inset * 2, h - inset * 2, token.imageFocus);
        sprite.scale.set(fit.scale);
        sprite.anchor.set(0.5);
        sprite.position.set(fit.x, fit.y);
        sprite.mask = mask;
        body.addChildAt(sprite, 1);
      }).catch(() => { /* broken image: the initials stay */ });
    }
    if (!token.imageUrl) {
      const text = new Text({ text: initials(token.label), style: { fontFamily: FONT, fontSize: Math.max(12, Math.min(w, h) * 0.38), fontWeight: '700', fill: color }, resolution: 2 });
      text.anchor.set(0.5);
      body.addChild(text);
    }
    const selected = data.selectedTokens.includes(token.id);
    body.addChild(shape(new Graphics()).stroke({ width: selected ? 4 : 3, color: selected ? 0xffffff : color }));
    if (fighter?.dead) {
      const r = Math.min(w, h) * 0.35;
      body.addChild(new Graphics().moveTo(-r, -r).lineTo(r, r).moveTo(r, -r).lineTo(-r, r).stroke({ width: 5, color: 0xff2a3d, alpha: 0.9 }));
    }
    node.addChild(body);

    if (status) {
      const barW = w * 0.9;
      const bar = new Graphics()
        .rect(-barW / 2, h / 2 + 3, barW, 7).fill({ color: 0x000000, alpha: 0.75 })
        .rect(-barW / 2 + 1, h / 2 + 4, Math.max(0, (barW - 2) * status.ratio), 5).fill({ color: status.color });
      node.addChild(bar);
      let bx = -w / 2;
      for (const badge of status.conditions.slice(0, 3)) {
        const b = new Text({ text: badge.slice(0, 10), style: { fontFamily: FONT, fontSize: 11, fontWeight: '700', fill: 0xffcf3a, stroke: { color: 0x000000, width: 3 } }, resolution: 2 });
        b.position.set(bx, h / 2 + 30);
        bx += b.width + 4;
        node.addChild(b);
      }
      this.drawInjuries(node, status.injuries, w, h);
    }
    const label = new Text({ text: token.label, style: { fontFamily: FONT, fontSize: 14, fontWeight: '600', fill: 0xe9edf2, stroke: { color: 0x000000, width: 3 } }, resolution: 2 });
    // Neighbours' names must not run into each other: long ones are cut to about the token's width.
    for (let text = token.label; label.width > Math.max(w * 1.3, 40) && text.length > 3;) {
      text = text.slice(0, -1);
      label.text = `${text.trimEnd()}…`;
    }
    label.anchor.set(0.5, 0);
    label.position.set(0, h / 2 + (status ? 12 : 3));
    node.addChild(label);

    node.alpha = data.gm && token.visibility !== 'All' ? 0.55 : 1;
  }

  /**
   * Critical Injuries as small icons in an arc over the token (the eighth slot shows "+N" when there are more);
   * hovering one names it.
   */
  private drawInjuries(node: Container, injuries: readonly string[], w: number, h: number) {
    if (injuries.length === 0) return;
    const r = Math.max(7, Math.min(11, Math.min(w, h) * 0.14));
    const slots = arcPositions(injuries.length, Math.max(w, h) / 2 + r * 0.6);
    slots.forEach((p, i) => {
      const more = i === 7 && injuries.length > 8;
      const icon = new Container();
      icon.position.set(p.x, p.y);
      icon.eventMode = 'static';
      icon.cursor = 'help';
      const g = new Graphics().circle(0, 0, r).fill({ color: 0x14070a, alpha: 0.95 }).stroke({ width: 2, color: 0xff2a3d });
      if (!more) drawInjuryIcon(g, injuryIcon(injuries[i]), r);
      icon.addChild(g);
      if (more) {
        const plus = new Text({ text: `+${injuries.length - 7}`, style: { fontFamily: FONT, fontSize: r, fontWeight: '700', fill: 0xffffff }, resolution: 2 });
        plus.anchor.set(0.5);
        icon.addChild(plus);
      }
      const name = more ? injuries.slice(7).join(', ') : injuries[i];
      let label: Text | null = null;
      icon.on('pointerover', () => {
        label = new Text({ text: name, style: { fontFamily: FONT, fontSize: 12, fontWeight: '700', fill: 0xffffff, stroke: { color: 0x000000, width: 3 } }, resolution: 2 });
        label.anchor.set(0.5, 1);
        label.position.set(0, -r - 2);
        icon.addChild(label);
      });
      icon.on('pointerout', () => { label?.destroy(); label = null; });
      node.addChild(icon);
    });
  }

  /** A barrier: the material's drawn texture, its HP under it, cracked and faded at 0 HP. */
  private drawBarrier(node: Container, token: TokenView, barrier: BarrierView, w: number, h: number, selected: boolean) {
    const look = barrierLook(barrier.material);
    const state = barrierState(barrier);
    const body = new Container();
    body.rotation = (token.rotation * Math.PI) / 180;
    const x0 = -w / 2;
    const y0 = -h / 2;
    const tex = new Graphics().rect(x0, y0, w, h).fill({ color: look.base, alpha: look.alpha });
    const step = Math.max(10, Math.min(w, h) / 2);
    if (look.pattern === 'plates') {
      for (let x = x0 + step; x < x0 + w - 1; x += step) tex.moveTo(x, y0).lineTo(x, y0 + h);
      for (let y = y0 + step; y < y0 + h - 1; y += step) tex.moveTo(x0, y).lineTo(x0 + w, y);
      tex.stroke({ width: 2, color: look.line, alpha: 0.7 });
      for (let x = x0 + 5; x < x0 + w; x += step) for (let y = y0 + 5; y < y0 + h; y += step) tex.circle(x, y, 1.6);
      tex.fill({ color: look.line, alpha: 0.9 });
    } else if (look.pattern === 'blocks') {
      const bh = step / 1.5;
      let row = 0;
      for (let y = y0; y < y0 + h; y += bh, row++) {
        tex.moveTo(x0, y).lineTo(x0 + w, y);
        for (let x = x0 + (row % 2 ? step / 2 : step); x < x0 + w; x += step) tex.moveTo(x, y).lineTo(x, Math.min(y + bh, y0 + h));
      }
      tex.stroke({ width: 2, color: look.line, alpha: 0.8 });
    } else if (look.pattern === 'grain') {
      const along = w >= h;
      const span = along ? h : w;
      for (let i = 1; i < 6; i++) {
        const o = (span / 6) * i;
        if (along) tex.moveTo(x0, y0 + o).bezierCurveTo(x0 + w / 3, y0 + o - 3, x0 + (2 * w) / 3, y0 + o + 3, x0 + w, y0 + o);
        else tex.moveTo(x0 + o, y0).bezierCurveTo(x0 + o - 3, y0 + h / 3, x0 + o + 3, y0 + (2 * h) / 3, x0 + o, y0 + h);
      }
      tex.stroke({ width: 1.5, color: look.line, alpha: 0.8 });
    } else if (look.pattern === 'glass') {
      for (let d = step / 2; d < w + h; d += step) tex.moveTo(x0 + Math.max(0, d - h), y0 + Math.min(d, h)).lineTo(x0 + Math.min(d, w), y0 + Math.max(0, d - w));
      tex.stroke({ width: 2, color: look.line, alpha: 0.6 });
    }
    if (state.destroyed) {
      tex.moveTo(x0, y0).lineTo(x0 + w * 0.4, y0 + h * 0.55).lineTo(x0 + w * 0.55, y0 + h * 0.35).lineTo(x0 + w, y0 + h)
        .moveTo(x0 + w, y0).lineTo(x0 + w * 0.6, y0 + h * 0.6).lineTo(x0 + w * 0.3, y0 + h)
        .stroke({ width: 3, color: 0x111111, alpha: 0.9 });
    }
    tex.rect(x0, y0, w, h).stroke({ width: selected ? 4 : 2, color: selected ? 0xffffff : look.line, alpha: 1 });
    body.addChild(tex);
    body.alpha = state.destroyed ? 0.45 : 1;
    node.addChild(body);

    const barW = Math.max(30, w * 0.9);
    node.addChild(new Graphics()
      .rect(-barW / 2, h / 2 + 3, barW, 6).fill({ color: 0x000000, alpha: 0.75 })
      .rect(-barW / 2 + 1, h / 2 + 4, Math.max(0, (barW - 2) * state.ratio), 4).fill({ color: state.ratio > 0.5 ? 0x3ddc84 : state.ratio > 0 ? 0xffcf3a : 0xff2a3d }));
    const label = new Text({ text: `${token.label} · ${state.text}`, style: { fontFamily: FONT, fontSize: 12, fontWeight: '600', fill: 0xe9edf2, stroke: { color: 0x000000, width: 3 } }, resolution: 2 });
    label.anchor.set(0.5, 0);
    label.position.set(0, h / 2 + 11);
    node.addChild(label);
  }

  /** Puts a token where the data (or a live drag) says; position is the token's center. */
  private placeToken(id: string) {
    const node = this.tokens.get(id);
    const token = this.data?.scene.tokens.find((t) => t.id === id);
    if (!node || !token || !this.data) return;
    const g = this.data.scene.grid;
    const override = this.overrides.get(id);
    let { x, y } = token;
    if (override) {
      if (performance.now() > override.until || (Math.abs(override.x - x) < 0.001 && Math.abs(override.y - y) < 0.001)) this.overrides.delete(id);
      else ({ x, y } = override);
    }
    const p = cellToPx(g, x + token.width / 2, y + token.height / 2);
    node.position.set(p.x, p.y);
  }

  private animate() {
    const now = performance.now();
    const pulse = 0.55 + 0.45 * Math.sin(now / 260);
    for (const ring of this.activeRings) if (!ring.destroyed) ring.alpha = pulse;
    for (const id of this.overrides.keys()) this.placeToken(id);
    this.effects = this.effects.filter((fx) => {
      const age = (now - fx.born) / fx.duration;
      if (age >= 1) { fx.node.destroy({ children: true }); return false; }
      fx.update(age);
      return true;
    });
    this.pings = this.pings.filter(({ node, born }) => {
      const age = (now - born) / PING_MS;
      if (age >= 1) { node.destroy({ children: true }); return false; }
      const ring = node.children[0] as Graphics;
      const size = this.data ? this.data.scene.grid.sizePx : 70;
      const color = hex(node.label);
      ring.clear()
        .circle(0, 0, size * (0.3 + age * 1.6)).stroke({ width: 4, color, alpha: 1 - age })
        .circle(0, 0, size * (0.15 + ((age * 2) % 1) * 0.8)).stroke({ width: 2, color, alpha: (1 - age) * 0.7 });
      node.alpha = 1 - age * 0.3;
      return true;
    });
  }

  // ------------------------------------------------------------------ pointer

  private worldPoint(e: FederatedPointerEvent): Point {
    const p = this.world.toLocal(e.global);
    return { x: p.x, y: p.y };
  }

  private cellPoint(e: FederatedPointerEvent): Point {
    const p = this.worldPoint(e);
    return pxToCell(this.data!.scene.grid, p.x, p.y);
  }

  private onDown(e: FederatedPointerEvent, target: Target | null) {
    if (!this.data) return;
    e.stopPropagation();
    const touch = e.pointerType === 'touch';
    if (touch) {
      this.touches.set(e.pointerId, { x: e.global.x, y: e.global.y });
      // A second finger turns whatever the first was doing into a pinch.
      if (this.touches.size === 2) { this.cancelGesture(); this.startPinch(); return; }
    }
    if (this.gesture) return;
    const cell = this.cellPoint(e);

    if (e.button === 2 || e.button === 1) { this.startPan(e); return; }
    if (this.tool === 'select' && e.altKey) { this.callbacks.onPing(cell.x, cell.y); return; }
    if (this.tool === 'ruler') { this.gesture = { kind: 'ruler', from: cell }; return; }
    if (this.tool === 'token') { this.callbacks.onTokenPlace?.(Math.floor(cell.x), Math.floor(cell.y)); return; }
    if (this.tool === 'template') { this.gesture = { kind: 'place', origin: this.snapHalf(cell) }; this.drawPreview(cell); return; }

    const additive = e.shiftKey || e.ctrlKey || e.metaKey;
    // Grabbing something outside the selection makes it the selection (or adds it with Shift), so it can be dragged now.
    if (target && !this.isSelected(target)) this.select(target, additive);
    const timer = window.setTimeout(() => {
      if (this.gesture?.kind !== 'press') return;
      this.callbacks.onPing(this.gesture.cell.x, this.gesture.cell.y);
      this.gesture = { kind: 'done' };
    }, HOLD_MS);
    this.gesture = { kind: 'press', target, screen: { x: e.global.x, y: e.global.y }, cell, additive, touch, timer };
  }

  private startPan(e: FederatedPointerEvent | { global: Point }) {
    this.gesture = { kind: 'pan', start: { x: e.global.x, y: e.global.y }, origin: { x: this.world.position.x, y: this.world.position.y } };
  }

  private startPinch() {
    const [a, b] = [...this.touches.values()];
    this.pinch = {
      distance: Math.max(1, Math.hypot(a.x - b.x, a.y - b.y)), mid: { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 },
      scale: this.world.scale.x, origin: { x: this.world.position.x, y: this.world.position.y },
    };
    this.gesture = { kind: 'pinch' };
  }

  private cancelGesture() {
    const g = this.gesture;
    if (g?.kind === 'press') window.clearTimeout(g.timer);
    if (g?.kind === 'group') for (const t of g.tokens) { this.overrides.delete(t.id); this.placeToken(t.id); }
    if (g?.kind === 'group' && this.data) this.renderTemplates(this.data);
    this.lasso.clear();
    this.preview.clear();
    this.gesture = null;
  }

  private isSelected(target: Target) {
    const d = this.data!;
    return target.tokenId ? d.selectedTokens.includes(target.tokenId) : d.selectedTemplates.includes(target.template!.id);
  }

  /** A click on a token or area: it alone, or toggled in the selection with Shift. */
  private select(target: Target, additive: boolean) {
    const d = this.data!;
    if (target.tokenId) this.callbacks.onSelect(toggleSelection(d.selectedTokens, target.tokenId, additive), additive ? [...d.selectedTemplates] : []);
    else this.callbacks.onSelect(additive ? [...d.selectedTokens] : [], toggleSelection(d.selectedTemplates, target.template!.id, additive));
  }

  /** The selected things this viewer may drag right now. */
  private draggable() {
    const d = this.data!;
    const tokens = d.scene.tokens.filter((t) => d.selectedTokens.includes(t.id) && t.controllable && !t.locked)
      .map((t) => ({ id: t.id, x: t.x, y: t.y, size: Math.min(t.width, t.height) }));
    const templates = d.scene.templates.filter((t) => d.selectedTemplates.includes(t.id) && d.canMoveTemplate(t)).map((t) => ({ id: t.id, x: t.x, y: t.y }));
    return { tokens, templates };
  }

  private onMove(e: FederatedPointerEvent) {
    if (e.pointerType === 'touch' && this.touches.has(e.pointerId)) this.touches.set(e.pointerId, { x: e.global.x, y: e.global.y });
    const gesture = this.gesture;
    if (!gesture || !this.data) return;
    const g = this.data.scene.grid;
    switch (gesture.kind) {
      case 'pan':
        this.world.position.set(gesture.origin.x + e.global.x - gesture.start.x, gesture.origin.y + e.global.y - gesture.start.y);
        break;
      case 'pinch': {
        if (!this.pinch || this.touches.size < 2) break;
        const [a, b] = [...this.touches.values()];
        const distance = Math.max(1, Math.hypot(a.x - b.x, a.y - b.y));
        const mid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
        const p = this.pinch;
        const scale = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, p.scale * (distance / p.distance)));
        const wx = (p.mid.x - p.origin.x) / p.scale;
        const wy = (p.mid.y - p.origin.y) / p.scale;
        this.world.scale.set(scale);
        this.world.position.set(mid.x - wx * scale, mid.y - wy * scale);
        break;
      }
      case 'press': {
        if (Math.hypot(e.global.x - gesture.screen.x, e.global.y - gesture.screen.y) <= MOVE_TOLERANCE) break;
        window.clearTimeout(gesture.timer);
        if (gesture.target) {
          const items = this.draggable();
          if (items.tokens.length + items.templates.length === 0) { this.gesture = { kind: 'done' }; break; }
          this.gesture = { kind: 'group', start: gesture.cell, ...items };
          for (const t of items.tokens) { const node = this.tokens.get(t.id); if (node) node.cursor = 'grabbing'; }
        } else if (gesture.touch) {
          // A finger on empty ground pans: there is no right button on a phone.
          this.gesture = { kind: 'pan', start: gesture.screen, origin: { x: this.world.position.x, y: this.world.position.y } };
        } else {
          this.gesture = { kind: 'lasso', start: this.cellToWorld(gesture.cell), additive: gesture.additive };
        }
        this.onMove(e);
        break;
      }
      case 'group': {
        const cell = this.cellPoint(e);
        const dx = cell.x - gesture.start.x;
        const dy = cell.y - gesture.start.y;
        for (const t of gesture.tokens) {
          this.overrides.set(t.id, { x: t.x + dx, y: t.y + dy, until: Number.POSITIVE_INFINITY });
          this.placeToken(t.id);
          this.callbacks.onTokenDrag(t.id, t.x + dx, t.y + dy);
        }
        for (const t of gesture.templates) {
          const index = this.data.scene.templates.findIndex((x) => x.id === t.id);
          const node = this.templateLayer.children[index];
          if (!node) continue;
          const o = cellToPx(g, t.x, t.y);
          const p = cellToPx(g, t.x + dx, t.y + dy);
          node.position.set(p.x - o.x, p.y - o.y);
        }
        break;
      }
      case 'lasso': {
        const r = rectFrom(gesture.start, this.worldPoint(e));
        this.lasso.clear().rect(r.x, r.y, r.width, r.height).fill({ color: 0x00f0ff, alpha: 0.08 })
          .stroke({ width: 1.5 / this.world.scale.x, color: 0x00f0ff, alpha: 0.9 });
        break;
      }
      case 'ruler':
        this.drawRuler(gesture.from, this.cellPoint(e));
        break;
      case 'place':
        this.drawPreview(this.cellPoint(e));
        break;
    }
  }

  private onUp(e: FederatedPointerEvent) {
    if (e.pointerType === 'touch') {
      this.touches.delete(e.pointerId);
      if (this.gesture?.kind === 'pinch') { if (this.touches.size < 2) { this.pinch = null; this.gesture = null; } return; }
    }
    const gesture = this.gesture;
    this.gesture = null;
    if (!gesture || !this.data) return;
    const g = this.data.scene.grid;
    switch (gesture.kind) {
      case 'press': {
        window.clearTimeout(gesture.timer);
        const d = this.data;
        if (!gesture.target) { if (!gesture.additive) this.callbacks.onSelect([], []); break; }
        // A plain click on something already selected keeps only it; Shift+click on it takes it out.
        const id = gesture.target.tokenId ?? gesture.target.template!.id;
        const wasSelected = gesture.target.tokenId ? d.selectedTokens.includes(id) : d.selectedTemplates.includes(id);
        const onlyIt = d.selectedTokens.length + d.selectedTemplates.length === 1;
        if (wasSelected && (gesture.additive || !onlyIt)) this.select(gesture.target, gesture.additive);
        break;
      }
      case 'group': {
        const cell = this.cellPoint(e);
        const dx = cell.x - gesture.start.x;
        const dy = cell.y - gesture.start.y;
        const snap = g.snap && g.type === 'Square';
        const tokens = gesture.tokens.map((t) => ({ id: t.id, x: snap ? snapCell(t.x + dx, t.size) : t.x + dx, y: snap ? snapCell(t.y + dy, t.size) : t.y + dy }));
        const templates = gesture.templates.map((t) => ({ id: t.id, ...this.snapHalf({ x: t.x + dx, y: t.y + dy }) }));
        for (const t of tokens) {
          const node = this.tokens.get(t.id);
          if (node) node.cursor = 'grab';
          this.overrides.set(t.id, { x: t.x, y: t.y, until: performance.now() + 4000 });
          this.placeToken(t.id);
        }
        this.callbacks.onMoveDrop(tokens, templates);
        break;
      }
      case 'lasso': {
        this.lasso.clear();
        const r = rectFrom(gesture.start, this.worldPoint(e));
        const d = this.data;
        const tokens = d.scene.tokens.filter((t) => (d.gm || t.controllable) && inRect(cellToPx(g, t.x + t.width / 2, t.y + t.height / 2), r)).map((t) => t.id);
        const templates = d.scene.templates.filter((t) => (d.gm || d.canMoveTemplate(t)) && inRect(cellToPx(g, t.x, t.y), r)).map((t) => t.id);
        const merge = (old: readonly string[], add: string[]) => (gesture.additive ? [...new Set([...old, ...add])] : add);
        this.callbacks.onSelect(merge(d.selectedTokens, tokens), merge(d.selectedTemplates, templates));
        break;
      }
      case 'ruler':
        setTimeout(() => { if (this.gesture?.kind !== 'ruler') { this.ruler.clear(); this.rulerText.text = ''; } }, 1600);
        break;
      case 'place': {
        const cell = this.cellPoint(e);
        this.preview.clear();
        this.callbacks.onTemplatePlace(gesture.origin.x, gesture.origin.y, this.angleOf(gesture.origin, cell));
        break;
      }
    }
  }

  private cellToWorld(cell: Point): Point {
    return cellToPx(this.data!.scene.grid, cell.x, cell.y);
  }

  private onWheel = (e: WheelEvent) => {
    e.preventDefault();
    const rect = this.app.canvas.getBoundingClientRect();
    this.zoomAt({ x: e.clientX - rect.left, y: e.clientY - rect.top }, Math.exp(-e.deltaY * 0.0015));
  };

  private zoomAt(screen: Point, factor: number) {
    const old = this.world.scale.x;
    const scale = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, old * factor));
    const wx = (screen.x - this.world.position.x) / old;
    const wy = (screen.y - this.world.position.y) / old;
    this.world.scale.set(scale);
    this.world.position.set(screen.x - wx * scale, screen.y - wy * scale);
  }

  // ------------------------------------------------------------------ tools

  private snapHalf(p: Point): Point {
    const g = this.data!.scene.grid;
    if (!g.snap || g.type !== 'Square') return p;
    return { x: Math.round(p.x * 2) / 2, y: Math.round(p.y * 2) / 2 };
  }

  private angleOf(from: Point, to: Point) {
    if (Math.hypot(to.x - from.x, to.y - from.y) < 0.2) return 0;
    return Math.round((Math.atan2(to.y - from.y, to.x - from.x) * 180) / Math.PI);
  }

  private drawPreview(cell: Point) {
    const g = this.data!.scene.grid;
    const origin = this.gesture?.kind === 'place' ? this.gesture.origin : this.snapHalf(cell);
    this.preview.clear();
    this.drawShape(this.preview, g, { shape: this.draft.shape, x: origin.x, y: origin.y, sizeMeters: this.draft.sizeMeters, angle: this.angleOf(origin, cell) }, hex(this.draft.color), false);
  }

  private drawRuler(from: Point, to: Point) {
    const g = this.data!.scene.grid;
    const lang = this.data!.lang;
    const a = cellToPx(g, from.x, from.y);
    const b = cellToPx(g, to.x, to.y);
    const d = gridDistance(g, from, to);
    this.ruler.clear()
      .moveTo(a.x, a.y).lineTo(b.x, b.y).stroke({ width: 3, color: 0xf5e663 })
      .circle(a.x, a.y, 5).fill({ color: 0xf5e663 }).circle(b.x, b.y, 5).fill({ color: 0xf5e663 });
    const squares = d.squares == null ? '' : `${d.squares} ${lang === 'pt' ? (d.squares === 1 ? 'quadrado' : 'quadrados') : (d.squares === 1 ? 'square' : 'squares')} · `;
    const straight = d.squares == null ? '' : ` (${lang === 'pt' ? 'reta' : 'straight'} ${formatMeters(d.straightMeters, lang)})`;
    this.rulerText.text = `${squares}${formatMeters(d.meters, lang)}${straight}`;
    this.rulerText.scale.set(1 / this.world.scale.x);
    this.rulerText.position.set(b.x + 12 / this.world.scale.x, b.y - 28 / this.world.scale.x);
  }
}

import { Application, Container, Graphics, Sprite, Text, Texture, type FederatedPointerEvent } from 'pixi.js';
import type { Ping, SceneGrid, SceneTemplate, SceneView, TemplateShapeName, TokenView } from '../../../api/tableTypes';
import { cellToPx, formatMeters, gridDistance, pxToCell, snapCell, type Point } from '../grid';
import { templateShape } from '../templates';
import { initials, tokenStatus, type TokenFighter } from '../tokens';

export type BoardTool = 'select' | 'ruler' | 'template' | 'ping';

export interface TemplateDraft { shape: TemplateShapeName; sizeMeters: number; color: string }

export interface BoardData {
  scene: SceneView;
  /** Fighters of the linked fight by combatant id, as this viewer may see them. */
  fighters: Map<string, TokenFighter>;
  activeCombatantId: string | null;
  selectedTokenId: string | null;
  selectedTemplateId: string | null;
  /** Whether this viewer may drag a template (the GM: unlocked ones; a player: their own). */
  canMoveTemplate: (t: SceneTemplate) => boolean;
  /** The GM sees hidden tokens dimmed. */
  gm: boolean;
  lang: 'pt' | 'en';
}

export interface BoardCallbacks {
  onTokenSelect: (id: string | null) => void;
  onTokenDrag: (id: string, x: number, y: number) => void;
  onTokenDrop: (id: string, x: number, y: number) => void;
  onTemplateSelect: (id: string | null) => void;
  onTemplateDrop: (id: string, x: number, y: number) => void;
  onTemplatePlace: (x: number, y: number, angle: number) => void;
  onPing: (x: number, y: number) => void;
}

const FONT = 'Rajdhani, "Segoe UI", sans-serif';
const MIN_ZOOM = 0.08;
const MAX_ZOOM = 4;
/** Cells shown when the scene has no map yet. */
const EMPTY_COLS = 30;
const EMPTY_ROWS = 20;
const ACTIVE_COLOR = 0xf5e663;
const PING_MS = 2200;

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
  private ready = false;
  private destroyed = false;
  private fitted = false;

  /** What the pointer is doing between down and up. */
  private gesture:
    | { kind: 'pan'; start: Point; origin: Point }
    | { kind: 'token'; id: string; offset: Point; size: number; moved: boolean }
    | { kind: 'template'; id: string; offset: Point; moved: boolean }
    | { kind: 'ruler'; from: Point }
    | { kind: 'place'; origin: Point }
    | null = null;

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
    this.world.addChild(this.backgroundLayer, this.gridLayer, this.templateLayer, this.tokenLayer, this.overlay);
    this.overlay.addChild(this.preview, this.ruler, this.rulerText);
    this.app.stage.on('pointerdown', (e) => this.onDown(e, null));
    this.app.stage.on('globalpointermove', (e) => this.onMove(e));
    this.app.stage.on('pointerup', (e) => this.onUp(e));
    this.app.stage.on('pointerupoutside', (e) => this.onUp(e));
    this.app.canvas.addEventListener('wheel', this.onWheel, { passive: false });
    this.app.canvas.addEventListener('contextmenu', (e) => e.preventDefault());
    this.app.ticker.add(() => this.animate());
    this.ready = true;
    if (this.data) this.render();
  }

  destroy() {
    this.destroyed = true;
    if (!this.ready) return;
    this.app.canvas.removeEventListener('wheel', this.onWheel);
    this.app.destroy(true, { children: true });
  }

  setData(data: BoardData) {
    const sceneChanged = this.data?.scene.id !== data.scene.id;
    this.data = data;
    if (sceneChanged) { this.fitted = false; this.overrides.clear(); }
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
    if (this.gesture?.kind === 'token' && this.gesture.id === tokenId) return;
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
      this.drawShape(node, data.scene.grid, tpl, hex(tpl.color), tpl.id === data.selectedTemplateId);
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
        this.tokenLayer.addChild(node);
      }
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
        // Cover the shape, keeping the picture's proportion.
        const scale = Math.max((w - inset * 2) / texture.width, (h - inset * 2) / texture.height);
        sprite.scale.set(scale);
        sprite.anchor.set(0.5);
        sprite.mask = mask;
        body.addChildAt(sprite, 1);
      }).catch(() => { /* broken image: the initials stay */ });
    }
    if (!token.imageUrl) {
      const text = new Text({ text: initials(token.label), style: { fontFamily: FONT, fontSize: Math.max(12, Math.min(w, h) * 0.38), fontWeight: '700', fill: color }, resolution: 2 });
      text.anchor.set(0.5);
      body.addChild(text);
    }
    const selected = token.id === data.selectedTokenId;
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
      for (const badge of status.badges.slice(0, 4)) {
        const b = new Text({ text: badge.slice(0, 10), style: { fontFamily: FONT, fontSize: 11, fontWeight: '700', fill: 0xffcf3a, stroke: { color: 0x000000, width: 3 } }, resolution: 2 });
        b.position.set(bx, -h / 2 - 15);
        bx += b.width + 4;
        node.addChild(b);
      }
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

  private onDown(e: FederatedPointerEvent, target: { tokenId?: string; template?: SceneTemplate } | null) {
    if (!this.data || this.gesture) return;
    e.stopPropagation();
    const cell = this.cellPoint(e);

    if (e.button === 2 || e.button === 1) { this.startPan(e); return; }
    if (this.tool === 'ping' || (this.tool === 'select' && e.altKey)) { this.callbacks.onPing(cell.x, cell.y); return; }
    if (this.tool === 'ruler') { this.gesture = { kind: 'ruler', from: cell }; return; }
    if (this.tool === 'template') { this.gesture = { kind: 'place', origin: this.snapHalf(cell) }; this.drawPreview(cell); return; }

    if (target?.tokenId) {
      const token = this.data.scene.tokens.find((t) => t.id === target.tokenId);
      if (!token) return;
      this.callbacks.onTokenSelect(token.id);
      if (token.controllable && !token.locked) {
        this.gesture = { kind: 'token', id: token.id, offset: { x: cell.x - token.x, y: cell.y - token.y }, size: Math.min(token.width, token.height), moved: false };
        this.tokens.get(token.id)!.cursor = 'grabbing';
      }
      return;
    }
    if (target?.template) {
      const tpl = target.template;
      this.callbacks.onTemplateSelect(tpl.id);
      if (this.data.canMoveTemplate(tpl)) this.gesture = { kind: 'template', id: tpl.id, offset: { x: cell.x - tpl.x, y: cell.y - tpl.y }, moved: false };
      return;
    }
    this.callbacks.onTokenSelect(null);
    this.callbacks.onTemplateSelect(null);
    this.startPan(e);
  }

  private startPan(e: FederatedPointerEvent) {
    this.gesture = { kind: 'pan', start: { x: e.global.x, y: e.global.y }, origin: { x: this.world.position.x, y: this.world.position.y } };
  }

  private onMove(e: FederatedPointerEvent) {
    const gesture = this.gesture;
    if (!gesture || !this.data) return;
    const g = this.data.scene.grid;
    switch (gesture.kind) {
      case 'pan':
        this.world.position.set(gesture.origin.x + e.global.x - gesture.start.x, gesture.origin.y + e.global.y - gesture.start.y);
        break;
      case 'token': {
        const cell = this.cellPoint(e);
        const x = cell.x - gesture.offset.x;
        const y = cell.y - gesture.offset.y;
        gesture.moved = true;
        this.overrides.set(gesture.id, { x, y, until: Number.POSITIVE_INFINITY });
        this.placeToken(gesture.id);
        this.callbacks.onTokenDrag(gesture.id, x, y);
        break;
      }
      case 'template': {
        const cell = this.cellPoint(e);
        const node = this.templateLayer.children[this.data.scene.templates.findIndex((t) => t.id === gesture.id)];
        const tpl = this.data.scene.templates.find((t) => t.id === gesture.id);
        if (node && tpl) {
          gesture.moved = true;
          const o = cellToPx(g, tpl.x, tpl.y);
          const p = cellToPx(g, cell.x - gesture.offset.x, cell.y - gesture.offset.y);
          node.position.set(p.x - o.x, p.y - o.y);
        }
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
    const gesture = this.gesture;
    this.gesture = null;
    if (!gesture || !this.data) return;
    const g = this.data.scene.grid;
    switch (gesture.kind) {
      case 'token': {
        const node = this.tokens.get(gesture.id);
        if (node) node.cursor = 'grab';
        if (!gesture.moved) { this.overrides.delete(gesture.id); return; }
        const cell = this.cellPoint(e);
        let x = cell.x - gesture.offset.x;
        let y = cell.y - gesture.offset.y;
        if (g.snap && g.type === 'Square') { x = snapCell(x, gesture.size); y = snapCell(y, gesture.size); }
        this.overrides.set(gesture.id, { x, y, until: performance.now() + 4000 });
        this.placeToken(gesture.id);
        this.callbacks.onTokenDrop(gesture.id, x, y);
        break;
      }
      case 'template': {
        if (!gesture.moved) return;
        const cell = this.cellPoint(e);
        const p = this.snapHalf({ x: cell.x - gesture.offset.x, y: cell.y - gesture.offset.y });
        this.callbacks.onTemplateDrop(gesture.id, p.x, p.y);
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

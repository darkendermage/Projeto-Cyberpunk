import {
  AmbientLight, BoxGeometry, BufferGeometry, CanvasTexture, DirectionalLight, Float32BufferAttribute, Mesh, MeshStandardMaterial,
  PerspectiveCamera, PointLight, Quaternion, Scene, SRGBColorSpace, Vector3, WebGLRenderer, type Material,
} from 'three';
import { inkFor, type DiceThrow, type DieRoll } from './dice';
import { D6_VALUES, d10Normal, d10Shape, d10TextureUp, d6Normal, faceUpright } from './faces';

const D6_COLOR = '#3de0ff';
const D10_COLOR = '#ff2a3d';
const FACE_BG = '#0b0f14';
const FLIGHT_MS = 1150;
const STAGGER_MS = 70;
const CAMERA_HEIGHT = 14;
const FOV = 35;

/** A d10 face laid flat: where its apex, sides and far point sit on the texture (u right, v up), true to its shape. */
interface KiteUv { apex: [number, number]; left: [number, number]; far: [number, number]; right: [number, number] }

function kiteUv(): KiteUv {
  const shape = d10Shape(1);
  const [apex, a, far, b] = shape.faces[0].map((i) => shape.vertices[i]);
  const center = new Vector3().add(apex).add(a).add(far).add(b).multiplyScalar(0.25);
  let n = new Vector3().subVectors(a, apex).cross(new Vector3().subVectors(far, apex)).normalize();
  let [left, right] = [a, b];
  if (n.dot(center) < 0) { [left, right] = [b, a]; n = n.negate(); }
  const v = new Vector3().subVectors(apex, far).normalize();
  const u = new Vector3().crossVectors(v, n).normalize();
  const flat = (p: Vector3) => { const d = new Vector3().subVectors(p, far); return [d.dot(u), d.dot(v)] as [number, number]; };
  const pts = [flat(apex), flat(left), flat(far), flat(right)];
  const w = Math.max(...pts.map((p) => p[0])) - Math.min(...pts.map((p) => p[0]));
  const h = Math.max(...pts.map((p) => p[1]));
  const scale = Math.max(w, h) / 0.94;
  const uv = (p: [number, number]): [number, number] => [0.5 + p[0] / scale, 0.5 + (p[1] - h / 2) / scale];
  return { apex: uv(pts[0]), left: uv(pts[1]), far: uv(pts[2]), right: uv(pts[3]) };
}

const KITE = kiteUv();

/** A face of a die: the number in the app's display font and neon color, with a thin frame; 6 and 9 underlined. */
function faceTexture(value: number, color: string, kite: boolean, background = FACE_BG): CanvasTexture {
  const size = 256;
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = size;
  const g = canvas.getContext('2d')!;
  g.fillStyle = background;
  g.fillRect(0, 0, size, size);
  g.strokeStyle = color;
  g.globalAlpha = 0.55;
  g.lineWidth = 10;
  const at = ([u, v]: [number, number]) => [u * size, (1 - v) * size] as const;
  if (kite) {
    g.beginPath();
    g.moveTo(...at(KITE.apex)); g.lineTo(...at(KITE.left)); g.lineTo(...at(KITE.far)); g.lineTo(...at(KITE.right)); g.closePath();
    g.stroke();
  } else g.strokeRect(10, 10, size - 20, size - 20);
  g.globalAlpha = 1;
  g.fillStyle = color;
  g.shadowColor = color;
  g.shadowBlur = 14;
  g.textAlign = 'center';
  g.textBaseline = 'middle';
  // On a kite the number sits in its wide belly, just above the line between the sides.
  const belly = KITE.left[1] + (KITE.apex[1] - KITE.left[1]) * 0.2;
  const fontPx = kite ? size * (KITE.right[0] - KITE.left[0]) * 0.42 : 128;
  g.font = `700 ${Math.round(fontPx)}px "Chakra Petch", "Rajdhani", sans-serif`;
  const y = kite ? (1 - belly) * size : size / 2;
  g.fillText(String(value), size / 2, y);
  if (value === 6 || value === 9) g.fillRect(size / 2 - fontPx * 0.25, y + fontPx * 0.48, fontPx * 0.5, Math.max(4, fontPx * 0.07));
  const texture = new CanvasTexture(canvas);
  texture.colorSpace = SRGBColorSpace;
  return texture;
}

const material = (map: CanvasTexture) => new MeshStandardMaterial({ map, roughness: 0.45, metalness: 0.15 });

/** The d10 as a mesh: ten kites, two triangles each, one material (and number) per face. */
function d10Geometry(): BufferGeometry {
  const shape = d10Shape(1);
  const positions: number[] = [];
  const uvs: number[] = [];
  const geometry = new BufferGeometry();
  shape.faces.forEach((face, f) => {
    const [apex, side1, far, side2] = face.map((i) => shape.vertices[i]);
    let [left, right] = [side1, side2];
    // Counter-clockwise seen from outside, so the face is lit and its number reads the right way round.
    const n = new Vector3().subVectors(left, apex).cross(new Vector3().subVectors(far, apex));
    const center = new Vector3().add(apex).add(left).add(far).add(right).multiplyScalar(0.25);
    if (n.dot(center) < 0) [left, right] = [right, left];
    const uv = KITE;
    for (const [p, t] of [[apex, uv.apex], [left, uv.left], [far, uv.far], [apex, uv.apex], [far, uv.far], [right, uv.right]] as [Vector3, number[]][]) {
      positions.push(p.x, p.y, p.z);
      uvs.push(t[0], t[1]);
    }
    geometry.addGroup(f * 6, 6, f);
  });
  geometry.setAttribute('position', new Float32BufferAttribute(positions, 3));
  geometry.setAttribute('uv', new Float32BufferAttribute(uvs, 2));
  geometry.computeVertexNormals();
  return geometry;
}

interface Flying { mesh: Mesh; start: Vector3; end: Vector3; final: Quaternion; axis: Vector3; spin: number; delay: number; rest: number }

// three.js on a transparent canvas over the map. Dice tumble in and stop on the value the server rolled;
// nothing here decides results.
export class DiceScene {
  private readonly renderer: WebGLRenderer;
  private readonly scene = new Scene();
  private readonly camera = new PerspectiveCamera(FOV, 1, 0.1, 100);
  private readonly d10 = d10Shape(1);
  private readonly d10Mesh = d10Geometry();
  private readonly d6Mesh = new BoxGeometry(1.35, 1.35, 1.35);
  private readonly textures = new Map<string, CanvasTexture>();
  private dice: Mesh[] = [];
  private frame = 0;

  constructor(canvas: HTMLCanvasElement) {
    this.renderer = new WebGLRenderer({ canvas, alpha: true, antialias: true });
    this.renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
    this.renderer.setClearColor(0x000000, 0);
    this.camera.position.set(0, CAMERA_HEIGHT, 0);
    this.camera.up.set(0, 0, -1);
    this.camera.lookAt(0, 0, 0);
    this.scene.add(new AmbientLight(0xffffff, 0.75));
    const key = new DirectionalLight(0xffffff, 1.6);
    key.position.set(4, 12, 6);
    this.scene.add(key);
    const rim = new PointLight(0x3de0ff, 30, 30);
    rim.position.set(-6, 5, -4);
    this.scene.add(rim);
  }

  resize(width: number, height: number) {
    this.renderer.setSize(width, height, false);
    this.camera.aspect = width / Math.max(1, height);
    this.camera.updateProjectionMatrix();
  }

  /** Throws the dice and resolves once they have all stopped. */
  play(roll: DiceThrow): Promise<void> {
    this.clear();
    const halfH = CAMERA_HEIGHT * Math.tan((FOV * Math.PI) / 360);
    const halfW = halfH * this.camera.aspect;
    const perRow = Math.max(1, Math.min(6, Math.floor((halfW * 2 - 1) / 2.1)));
    const rows = Math.ceil(roll.dice.length / perRow);
    const flying: Flying[] = roll.dice.map((die, i) => {
      const row = Math.floor(i / perRow);
      const inRow = Math.min(perRow, roll.dice.length - row * perRow);
      const col = i % perRow;
      const end = new Vector3((col - (inRow - 1) / 2) * 2.1, 0, (row - (rows - 1) / 2) * 2.1);
      const mesh = this.mesh(die, roll.color ?? null);
      this.scene.add(mesh);
      this.dice.push(mesh);
      return {
        mesh, end, final: this.orientation(die),
        start: new Vector3(-halfW - 2 - Math.random() * 2, 0, halfH * (0.3 + Math.random() * 0.5)),
        axis: new Vector3(Math.random() - 0.5, Math.random() - 0.5, Math.random() - 0.5).normalize(),
        spin: Math.PI * (5 + Math.random() * 3), delay: i * STAGGER_MS, rest: die.sides === 6 ? 0.68 : 0.62,
      };
    });
    const started = performance.now();
    return new Promise((resolve) => {
      const step = () => {
        const now = performance.now() - started;
        let moving = false;
        for (const f of flying) {
          const t = Math.max(0, Math.min(1, (now - f.delay) / FLIGHT_MS));
          if (t < 1) moving = true;
          const ease = 1 - (1 - t) ** 3;
          f.mesh.position.set(
            f.start.x + (f.end.x - f.start.x) * ease,
            f.rest + 3.2 * (1 - t) ** 1.6 * Math.abs(Math.cos(Math.PI * 2.5 * t)),
            f.start.z + (f.end.z - f.start.z) * ease,
          );
          f.mesh.quaternion.copy(f.final).multiply(new Quaternion().setFromAxisAngle(f.axis, f.spin * (1 - ease)));
        }
        this.renderer.render(this.scene, this.camera);
        if (moving) this.frame = requestAnimationFrame(step);
        else resolve();
      };
      this.frame = requestAnimationFrame(step);
    });
  }

  clear() {
    cancelAnimationFrame(this.frame);
    for (const mesh of this.dice) this.scene.remove(mesh);
    this.dice = [];
    this.renderer.render(this.scene, this.camera);
  }

  dispose() {
    this.clear();
    this.d10Mesh.dispose();
    this.d6Mesh.dispose();
    for (const texture of this.textures.values()) texture.dispose();
    this.renderer.dispose();
  }

  private orientation(die: DieRoll): Quaternion {
    const tilt = (Math.random() - 0.5) * 0.5;
    if (die.sides === 10) return faceUpright(d10Normal(this.d10, die.value), d10TextureUp(this.d10, die.value), tilt);
    return faceUpright(d6Normal(die.value), this.boxTextureUp(D6_VALUES.indexOf(die.value)), tilt);
  }

  /** On a box face, the way its texture's v grows (the top of the number), read from the geometry itself. */
  private boxTextureUp(face: number): Vector3 {
    const position = this.d6Mesh.getAttribute('position');
    const uv = this.d6Mesh.getAttribute('uv');
    const corners = [0, 1, 2, 3].map((k) => face * 4 + k);
    const [a, b] = corners.flatMap((i, n) => corners.slice(n + 1).map((j) => [i, j])).find(([i, j]) => uv.getX(i) === uv.getX(j) && uv.getY(i) !== uv.getY(j))!;
    const [low, high] = uv.getY(a) < uv.getY(b) ? [a, b] : [b, a];
    return new Vector3(position.getX(high) - position.getX(low), position.getY(high) - position.getY(low), position.getZ(high) - position.getZ(low)).normalize();
  }

  private texture(value: number, color: string, kite: boolean, background = FACE_BG): CanvasTexture {
    const key = `${value}:${color}:${kite}:${background}`;
    let texture = this.textures.get(key);
    if (!texture) this.textures.set(key, texture = faceTexture(value, color, kite, background));
    return texture;
  }

  /** A die in the roller's color (the body), with numbers that read on it; without one, the neon dice of the table. */
  private mesh(die: DieRoll, body: string | null): Mesh {
    const ink = body ? inkFor(body) : null;
    if (die.sides === 6) return new Mesh(this.d6Mesh, D6_VALUES.map((v) => material(this.texture(v, ink ?? D6_COLOR, false, body ?? FACE_BG))) as Material[]);
    return new Mesh(this.d10Mesh, this.d10.values.map((v) => material(this.texture(v, ink ?? D10_COLOR, true, body ?? FACE_BG))) as Material[]);
  }
}

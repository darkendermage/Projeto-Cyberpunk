import { Quaternion, Vector3 } from 'three';

/**
 * The geometry of the table's dice and which way each value faces, so a die can be turned to show exactly what the
 * server rolled. The camera looks down: the face shown is the one whose normal points up (+Y).
 */
export const UP = new Vector3(0, 1, 0);

/** The value on each face of a box, in three.js' material order (+X, −X, +Y, −Y, +Z, −Z); opposite faces add to 7. */
export const D6_VALUES = [3, 4, 1, 6, 2, 5];
const D6_NORMALS = [new Vector3(1, 0, 0), new Vector3(-1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, -1, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1)];

export function d6Normal(value: number): Vector3 {
  const face = D6_VALUES.indexOf(value);
  if (face < 0) throw new Error(`d6 has no ${value}`);
  return D6_NORMALS[face].clone();
}

/**
 * A pentagonal trapezohedron: two apexes and a zigzag ring of ten vertices. The ring height that keeps each kite flat
 * is h·(1 − cos 36°)/(1 + cos 36°).
 */
export interface D10Shape { vertices: Vector3[]; faces: [number, number, number, number][]; values: number[] }

export function d10Shape(height = 1): D10Shape {
  const c = Math.cos(Math.PI / 5);
  const ring = height * (1 - c) / (1 + c);
  const vertices = [new Vector3(0, height, 0), new Vector3(0, -height, 0)];
  for (let i = 0; i < 10; i++) {
    const angle = (i * Math.PI) / 5;
    vertices.push(new Vector3(Math.cos(angle), i % 2 === 0 ? ring : -ring, Math.sin(angle)));
  }
  const v = (i: number) => 2 + (i % 10);
  // Kites: [apex, side, far point, side]; even ones meet at the top apex, odd ones at the bottom.
  const faces: [number, number, number, number][] = [];
  for (let i = 0; i < 10; i++) faces.push(i % 2 === 0 ? [0, v(i + 2), v(i + 1), v(i)] : [1, v(i), v(i + 1), v(i + 2)]);
  // Odd values on the top kites; each bottom kite takes 11 minus the top one facing away from it, like a real d10.
  const values = new Array<number>(10).fill(0);
  const shape = { vertices, faces, values };
  const normals = faces.map((_, f) => faceNormal(shape, f));
  for (let f = 0; f < 10; f += 2) values[f] = f + 1;
  for (let f = 1; f < 10; f += 2) {
    let opposite = 0;
    for (let g = 0; g < 10; g += 2) if (normals[f].dot(normals[g]) < normals[f].dot(normals[opposite])) opposite = g;
    values[f] = 11 - values[opposite];
  }
  return shape;
}

/** The outward normal of a kite face. */
export function faceNormal(shape: D10Shape, face: number): Vector3 {
  const [a, b, c] = shape.faces[face].map((i) => shape.vertices[i]);
  const n = new Vector3().subVectors(b, a).cross(new Vector3().subVectors(c, a)).normalize();
  const center = shape.faces[face].reduce((sum, i) => sum.add(shape.vertices[i]), new Vector3()).multiplyScalar(1 / 4);
  return n.dot(center) < 0 ? n.negate() : n;
}

export function d10Normal(shape: D10Shape, value: number): Vector3 {
  const face = shape.values.indexOf(value);
  if (face < 0) throw new Error(`d10 has no ${value}`);
  return faceNormal(shape, face);
}

/** Which way is screen-up on the table: the camera looks down with −Z at the top of the picture. */
export const SCREEN_UP = new Vector3(0, 0, -1);

/** A d10 face's "up" on its texture: from the far point to the apex, where the number's top points. */
export function d10TextureUp(shape: D10Shape, value: number): Vector3 {
  const [apex, , far] = shape.faces[shape.values.indexOf(value)].map((i) => shape.vertices[i]);
  return new Vector3().subVectors(apex, far).normalize();
}

/**
 * The turn that brings the face up and stands its number upright on screen (the texture's up pointing to the top of
 * the picture), give or take <paramref name="tilt"/> radians so the dice don't look lined up by a ruler.
 */
export function faceUpright(normal: Vector3, textureUp: Vector3, tilt = 0): Quaternion {
  const toUp = faceUp(normal);
  const up = textureUp.clone().applyQuaternion(toUp);
  up.y = 0;
  const angle = Math.atan2(up.x * SCREEN_UP.z - up.z * SCREEN_UP.x, up.x * SCREEN_UP.x + up.z * SCREEN_UP.z);
  // Rotating about +Y by θ takes (x, z) to (x cosθ + z sinθ, −x sinθ + z cosθ): solve for the angle that lands on screen-up.
  return new Quaternion().setFromAxisAngle(UP, -angle + tilt).multiply(toUp);
}

/** The turn that brings this face normal up, then spins the die by <paramref name="yaw"/> about the vertical. */
export function faceUp(normal: Vector3, yaw = 0): Quaternion {
  const toUp = new Quaternion().setFromUnitVectors(normal.clone().normalize(), UP);
  return new Quaternion().setFromAxisAngle(UP, yaw).multiply(toUp);
}

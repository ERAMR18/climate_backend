import { pbkdf2, randomBytes, timingSafeEqual } from 'node:crypto';
import { promisify } from 'node:util';

const derive = promisify(pbkdf2);
const iterations = 210000;

/** ASP.NET Identity V2 and V3 wire format; no plaintext passwords are persisted. */
export async function verifyPassword(password: string, encoded: string): Promise<boolean> {
  try {
    if (encoded.length > 512 || !/^[A-Za-z0-9+/]+={0,2}$/.test(encoded)) return false;
    const bytes = Buffer.from(encoded, 'base64');
    if (bytes[0] === 0 && bytes.length === 49) {
      const actual = await derive(password, bytes.subarray(1, 17), 1000, 32, 'sha1');
      return timingSafeEqual(actual, bytes.subarray(17));
    }
    if (bytes[0] !== 1 || bytes.length < 45) return false;
    const prf = bytes.readUInt32BE(1);
    const count = bytes.readUInt32BE(5);
    const saltLength = bytes.readUInt32BE(9);
    const algorithms = ['sha1', 'sha256', 'sha512'];
    if (prf > 2 || count < 1 || count > 2000000 || saltLength < 16 || saltLength > 128) return false;
    const saltEnd = 13 + saltLength;
    const expected = bytes.subarray(saltEnd);
    if (expected.length < 16 || expected.length > 128) return false;
    const actual = await derive(password, bytes.subarray(13, saltEnd), count, expected.length, algorithms[prf]);
    return timingSafeEqual(actual, expected);
  } catch { return false; }
}

export async function hashPassword(password: string): Promise<string> {
  const salt = randomBytes(16);
  const derived = await derive(password, salt, iterations, 32, 'sha512');
  const header = Buffer.alloc(13);
  header[0] = 1;
  header.writeUInt32BE(2, 1);
  header.writeUInt32BE(iterations, 5);
  header.writeUInt32BE(salt.length, 9);
  return Buffer.concat([header, salt, derived]).toString('base64');
}

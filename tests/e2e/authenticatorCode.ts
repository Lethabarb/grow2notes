import { createHmac } from 'node:crypto';

const base32Alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
const stepSeconds = 30;

/**
 * The code that an authenticator app holding `key` shows at `time`, now unless given (tests/README.md): RFC 6238's
 * time-based code, as Identity's authenticator provider checks it, an HMAC-SHA1 of the 30-second step since 1970, cut
 * to six digits, from the key in base32 as view G shows it. Identity checks a code against the real time, and accepts
 * the codes of the steps either side of it, so a wrong code is the right key's code for a step well away from now.
 */
export function authenticatorCode(key: string, time = new Date()): string {
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(time.getTime() / 1000 / stepSeconds)));
  const hash = createHmac('sha1', fromBase32(key)).update(counter).digest();

  // RFC 4226's dynamic truncation: the last byte's low four bits say where to read four bytes, less the top bit.
  const offset = hash.readUInt8(hash.length - 1) & 0x0f;
  const value = hash.readUInt32BE(offset) & 0x7fffffff;
  return String(value % 1_000_000).padStart(6, '0');
}

// RFC 4648's base32, without padding, as Identity writes its keys: five bits for each character, most significant
// first.
function fromBase32(key: string): Buffer {
  const bytes: number[] = [];
  let buffer = 0;
  let bits = 0;
  for (const character of key) {
    const value = base32Alphabet.indexOf(character);
    if (value < 0) {
      throw new Error(`'${character}' is not a base32 character.`);
    }

    buffer = (buffer << 5) | value;
    bits += 5;
    if (bits >= 8) {
      bits -= 8;
      bytes.push(buffer >> bits);
      buffer &= (1 << bits) - 1;
    }
  }

  return Buffer.from(bytes);
}

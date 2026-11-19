# Prints docs/vdb/conversations/sealed-lines.txt, sealed with Python's cryptography package, an implementation
# independent of both VentureOS's C# and the vdb tool's swift-crypto, so each is checked against bytes neither produced.
# Run it with: python3 docs/vdb/sealed-lines.py > docs/vdb/conversations/sealed-lines.txt (needs the cryptography package).
import base64, hashlib, struct, sys
from cryptography.hazmat.primitives.ciphers.aead import ChaCha20Poly1305
from cryptography.hazmat.primitives.kdf.hkdf import HKDF
from cryptography.hazmat.primitives import hashes

ID = "0123456789abcdef0123456789abcdef"
CN = "00112233445566778899aabbccddeeff"
SN = "f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff"
kid = hashlib.sha256(("vdb key-id " + ID).encode()).hexdigest()[:16]
salt = bytes.fromhex(CN) + bytes.fromhex(SN)

def key(info):
    return HKDF(algorithm=hashes.SHA256(), length=32, salt=salt, info=info.encode()).derive(bytes.fromhex(ID))

to_venture, to_computer = ChaCha20Poly1305(key("vdb/1 to venture")), ChaCha20Poly1305(key("vdb/1 to computer"))
counts = {"v": 0, "c": 0}

def seal(aead, side, line):
    nonce = b"\0\0\0\0" + struct.pack("<Q", counts[side])
    counts[side] += 1
    return "sealed " + base64.b64encode(aead.encrypt(nonce, line.encode(), None)).decode()

out = ["# The bytes on the network, sealed by Python's cryptography package, so VentureOS and the vdb tool are each",
       "# checked against lines neither of them sealed. The lines inside are: ok vdb/1 install launch; launch Dice.app;",
       "# ok; out Rolled a 4; an empty line, which keeps the connection open; bye; ok.",
       "! identity " + ID,
       "! trusted " + ID,
       f"> hello vdb/1 {kid} {CN}",
       f"< hello vdb/1 {SN}",
       "! raw"]
out.append("< " + seal(to_computer, "c", "ok vdb/1 install launch"))
out.append("> " + seal(to_venture, "v", "launch Dice.app"))
out.append("< " + seal(to_computer, "c", "ok"))
out.append("! print Rolled a 4")
out.append("< " + seal(to_computer, "c", "out Rolled a 4"))
out.append("> " + seal(to_venture, "v", ""))
out.append("> " + seal(to_venture, "v", "bye"))
out.append("< " + seal(to_computer, "c", "ok"))
out.append("! closed")
sys.stdout.write("\n".join(out) + "\n")

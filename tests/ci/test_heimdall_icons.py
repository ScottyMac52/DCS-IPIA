"""Regress the ICO encoding that caused WIC's 0x88982F60 startup failure."""
from pathlib import Path
import struct
import unittest

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'src/Heimdall.App/Branding'


class HeimdallIconTests(unittest.TestCase):
    def test_every_icon_frame_is_a_complete_32_bit_dib_with_an_and_mask(self):
        for name in ('heimdall.ico', 'heimdall-monochrome.ico'):
            with self.subTest(icon=name):
                data = (ASSETS / name).read_bytes()
                self.assertEqual((0, 1, 9), struct.unpack_from('<HHH', data))
                next_offset = 6 + 9 * 16
                for i in range(9):
                    width, height, colors, reserved, planes, bits, length, offset = struct.unpack_from('<BBBBHHII', data, 6 + i * 16)
                    width, height = width or 256, height or 256
                    self.assertEqual((0, 0, 1, 32), (colors, reserved, planes, bits))
                    self.assertEqual(next_offset, offset)
                    dib = data[offset:offset + length]
                    header, dw, dh, dp, db, compression, image_size = struct.unpack_from('<IiiHHII', dib)
                    self.assertEqual((40, width, height * 2, 1, 32, 0), (header, dw, dh, dp, db, compression))
                    pixels = width * height * 4
                    mask = ((width + 31) // 32) * 4 * height
                    self.assertEqual(40 + pixels + mask, length)
                    self.assertEqual(0, image_size)
                    self.assertEqual(bytes(mask), dib[40 + pixels:])
                    self.assertEqual({255}, set(dib[43:40 + pixels:4]))
                    next_offset += length
                self.assertEqual(len(data), next_offset)


if __name__ == '__main__':
    unittest.main()

# PlayerOne.SDK

Player One cameras and filter wheels from .NET, wrapping the vendor SDK and implementing
SharpAstro's `TianWen.DAL` device abstraction so a Player One body is driven by the same code that
drives a ZWO or a QHYCCD one.

Vendor SDKs wrapped:

| Part | Vendor SDK |
|---|---|
| Camera (`PlayerOneCamera`) | 3.10.1 |
| Filter wheel (`PlayerOnePW`) | 1.2.3 |

The package version tracks the **camera** SDK, so 3.10.x wraps Player One Camera SDK 3.10.1. The
filter wheel runs on its own version line and is stated here rather than in the package number,
because one package cannot carry two.

## Native coverage

Wider than either sibling. Player One ships binaries for every RID below, where ZWO ships none for
macOS at all:

```
win-x86     win-x64
linux-x86   linux-x64   linux-arm   linux-arm64
osx-x64     osx-arm64
```

Two things about how they are committed here:

- **The macOS dylib is one file for both Mac RIDs.** The vendor ships a universal binary carrying
  x86_64 and arm64 together, so the two RIDs take the same bytes. That is not a copy-paste error.
- **The Linux `.so` files are the versioned real files, renamed.** The vendor archive ships
  `libPlayerOneCamera.so` as a symlink down to `libPlayerOneCamera.so.3.10.1`, and a symlink is
  neither creatable on a plain Windows checkout nor meaningful inside a NuGet package.

## White balance is centred on zero here

Worth knowing before comparing a Player One frame with a ZWO one. `POA_WB_R` / `POA_WB_G` /
`POA_WB_B` run `[-1200, 1200]` and **0 applies no gain**, where ZWO's channels run about `[1, 99]`
with unity at 50. A caller reading one vendor's neutral value as the other's writes a colour cast
into the raw stream, which then has to be matched by every dark frame taken for it.

Both numbers are measured rather than read off a datasheet: a Player One frame captured at 0
measures exactly 1.000 per Bayer phase, and a ZWO frame captured at 65 measures 1.312, which is
65/50. Nothing in a FITS header records a white balance, so the only way to check after the fact is
the per-photosite quantisation step.

`ICMOSNativeInterface.TryGetWhiteBalanceRange` is how a caller asks instead of assuming, and
`HasThreeChannelWhiteBalance` is true here: Player One balances green explicitly, so writing only
red and blue leaves the third channel wherever it was last put.

## Licence

The vendor SDK binaries and headers are redistributed under Player One's own licence, `license.txt`
in this repository. The wrapper code is SharpAstro's.

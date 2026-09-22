# Vendor reference, not source

Player One's own C# bindings, exactly as shipped in the SDK archives
(camera 3.10.1, filter wheel 1.2.3). **Nothing here is compiled**, and nothing here should be
copied from without reading this file first.

They are kept because they are the clearest statement of what the C API expects, especially the
marshalling of the `POAConfigValue` union and the `byte[]` name fields. Our own binding is written
against the C headers in `include/` instead, the way `ZWOptical.SDK/include/ASICamera2.cs` and
`QHYCCD.SDK/include/QHYCamera.cs` are, so that it is nullable-aware and implements
`TianWen.DAL.ICMOSNativeInterface` directly rather than sitting under another layer.

## Known defects in these files

Recorded rather than patched. Patching would make the next SDK drop a merge instead of a copy, and
the code that ships is ours.

**`POAGetPWFilterAlias` and `POAGetPWCustomName` decode their buffer whether or not the call
succeeded.**

```csharp
error = POAGetPWFilterAlias64(Handle, position, pNameBuf, bufLen);
strfilterAlias = Marshal.PtrToStringAnsi(pNameBuf);   // error never consulted
```

`Marshal.AllocCoTaskMem` does not zero its allocation, so when the call fails the buffer still holds
whatever was in the heap and `PtrToStringAnsi` returns it up to the first NUL byte. The caller gets
a plausible-looking string rather than an error, which is how this survives: a wrong filter name in
a UI, not a crash. **Our binding checks the error before reading the buffer.**

**Six nullability holes.** Every one is `Marshal.PtrToStringAnsi`, which returns `string?`, flowing
into a non-nullable `string`: `POAGetErrorString`, `POAGetSDKVersion`, `POAGetPWErrorString`,
`POAGetPWSDKVer` (returns), and the two buffer sites above (assignments). Four are unreachable in
practice, the pointer being a static literal inside the DLL; two cannot be null at all, the pointer
being the binding's own buffer. Compiling these files is what produced the CS8601/CS8603 warnings
that would otherwise have had to be suppressed project-wide, hiding the same class of bug in code we
do own.

**A 32/64 entry point pair for every function.** Not a defect, but not something we need: both
widths ship as `PlayerOneCamera.dll`, under `lib/x86` and `lib/x64`, so the RID has already chosen
by the time a `DllImport` resolves.

## If these ever are patched

Keep the pristine file and a `.patch` beside it rather than editing in place, so an SDK bump stays a
copy plus a re-apply, and so that what we changed is legible without a diff against a download.

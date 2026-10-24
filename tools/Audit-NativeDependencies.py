"""Build-time PE import audit. pefile lives in the ignored cache, never in the MSI."""
import argparse
import json
import pathlib
import sys

parser = argparse.ArgumentParser()
parser.add_argument("root", type=pathlib.Path)
parser.add_argument("--output", type=pathlib.Path)
parser.add_argument("--library-cache", type=pathlib.Path)
args = parser.parse_args()
if args.library_cache:
    sys.path.insert(0, str(args.library_cache))
import pefile

# Explicit Windows 11 inbox dependencies; do not classify arbitrary System32
# files as portable. API-set contracts are provided by the OS loader.
SYSTEM = set("""
advapi32.dll avrt.dll bcrypt.dll bcryptprimitives.dll cabinet.dll cfgmgr32.dll
clbcatq.dll comctl32.dll comdlg32.dll combase.dll crypt32.dll cryptbase.dll
d2d1.dll d3d11.dll d3d12.dll d3dcompiler_47.dll dcomp.dll dbghelp.dll
dinput8.dll dnsapi.dll dsound.dll dwmapi.dll dwrite.dll dxgi.dll dxva2.dll
gdi32.dll gdi32full.dll hid.dll imagehlp.dll imm32.dll iphlpapi.dll kernel32.dll
kernelbase.dll mf.dll mfplat.dll mfreadwrite.dll mfuuid.dll mmdevapi.dll
msacm32.dll msvcrt.dll msvcp_win.dll ncrypt.dll netapi32.dll normaliz.dll
ntdll.dll ole32.dll oleacc.dll oleaut32.dll opengl32.dll powrprof.dll propsys.dll
psapi.dll rpcrt4.dll secur32.dll setupapi.dll shell32.dll shlwapi.dll user32.dll
ucrtbase.dll userenv.dll usp10.dll uxtheme.dll version.dll winhttp.dll wininet.dll
winmm.dll winspool.drv wintrust.dll wldap32.dll ws2_32.dll wtsapi32.dll
mscoree.dll urlmon.dll windowscodecs.dll win32u.dll windows.storage.dll
shcore.dll ninput.dll profapi.dll bcp47langs.dll coremessaging.dll mscms.dll
uiautomationcore.dll xmllite.dll bcp47mrm.dll rometadata.dll sspicli.dll
avicap32.dll dxcore.dll elscore.dll
""".split())

root = args.root.resolve()
files = sorted(p for p in root.rglob("*") if p.suffix.lower() in (".dll", ".exe"))
bundled = {}
for path in files:
    bundled.setdefault(path.name.lower(), []).append(path)
records, unresolved, wrong_architecture = [], [], []
for path in files:
    pe = pefile.PE(str(path), fast_load=True)
    pe.parse_data_directories(directories=[1, 13])
    clr = pe.OPTIONAL_HEADER.DATA_DIRECTORY[14]
    managed = bool(clr.VirtualAddress)
    machine = pe.FILE_HEADER.Machine
    if machine != 0x8664 and not (managed and machine == 0x14C):
        wrong_architecture.append(path.relative_to(root).as_posix())
    imports = []
    for table, kind in (("DIRECTORY_ENTRY_IMPORT", "import"),
                        ("DIRECTORY_ENTRY_DELAY_IMPORT", "delay-import")):
        for entry in getattr(pe, table, []):
            name = entry.dll.decode("ascii").lower()
            # Normal loader search permits same-directory and application-root
            # DLLs. libmpv explicitly enables its own DLL directory + System32.
            candidates = [p for p in bundled.get(name, [])
                          if p.parent in (path.parent, root)]
            if candidates:
                resolution = "bundled"
                target = candidates[0].relative_to(root).as_posix()
            elif name in SYSTEM or name.startswith(("api-ms-", "ext-ms-")):
                resolution, target = "windows-11-inbox", None
            else:
                resolution, target = "unresolved", None
                unresolved.append({"file": path.relative_to(root).as_posix(), "dll": name, "kind": kind})
            imports.append({"dll": name, "kind": kind, "resolution": resolution, "target": target})
    records.append({"file": path.relative_to(root).as_posix(),
                    "machine": hex(machine), "managed": managed, "imports": imports})
    pe.close()
result = {"target": "Windows 11 x64", "peFiles": len(records),
          "unresolved": unresolved, "wrongArchitecture": wrong_architecture,
          "limitations": "Static import/delay-import audit; dynamically loaded plugins and GPU drivers require runtime/second-machine checks.",
          "files": records}
if args.output:
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps({k: v for k, v in result.items() if k != "files"}, indent=2))
if unresolved or wrong_architecture:
    sys.exit(1)

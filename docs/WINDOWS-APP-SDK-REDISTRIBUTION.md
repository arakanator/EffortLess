# Windows App SDK binary redistribution review

Reviewed October 24, 2026 against the restored application package graph.
Top-level package: Microsoft.WindowsAppSDK 1.8.260921001. This is a technical
inventory and unresolved release-gate assessment, not legal certification.

## Exact terms retained

third-party/NUGET-NOTICES.json identifies 21 license/notice files copied verbatim
from the restored application packages, with version and SHA-256. The full
27-package application/build/test metadata inventory is in
PUBLICATION-NUGET-INVENTORY.json; not every build/test package ships at runtime.

| Application package | Version | Preserved terms / status |
| --- | --- | --- |
| WindowsAppSDK.Runtime | 1.8.260921001 | Microsoft license.txt and NOTICE.txt |
| WindowsAppSDK.WinUI | 1.8.260803003 | Microsoft license.txt, NOTICE.txt, tools/NOTICE.txt |
| WindowsAppSDK.AI | 1.8.79 | Microsoft license.txt |
| WindowsAppSDK.ML | 1.8.2223 | Microsoft license.txt and ThirdPartyNotices.txt |
| WindowsAppSDK Base/Foundation/DWrite/InteractiveExperiences/Widgets | See inventory | Exact available package notices retained |
| Microsoft.Web.WebView2 | 1.0.3179.45 | Package LICENSE.txt and NOTICE.txt; SDK loader does not imply separately installed browser runtime |
| System.Numerics.Tensors | 9.0.0 | MIT metadata; separate runtime attribution must be preserved |

The actual Windows App SDK binary license section 3(a)(i) permits redistribution
of files binplaced by its NuGet package, including self-contained deployments.
Sections 3(b) and 3(c) impose requirements and restrictions: primary application
functionality, protection of Microsoft/distributable code in recipient terms,
indemnification, trademark restrictions and limits on subjecting Microsoft source
to source-disclosure licenses. Section 4 also retains restrictions with exceptions
for applicable third-party open-source terms. Read the full package texts; this
summary is not a replacement for them.

The MIT license from the WindowsAppSDK source repository does not replace those
binary package terms. Microsoft components retain their own terms; they need not
all be GPL licensed merely because the application is GPL licensed.

## Deployment evidence and remaining gate

Microsoft's authoritative self-contained deployment guide documents extraction
of the SDK framework package into the output directory for inclusion in a custom
unpackaged installer. .NET must separately be self-contained, as this app is:
https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps

This establishes the supported deployment mechanism. It does not decide whether
GPL system-library exceptions or another applicable permission cover every
Microsoft component in this particular combined installer. That compatibility
basis, recipient terms and full package-to-payload file mapping remain open.
Do not describe the combined installer as GPL-compliant while they are open.
The current payload contains transitive AI/ML/ONNX/WebView2 files even though
the player exposes no AI feature. No SDK file was removed to work around terms.

Future public packaging must preserve the exact applicable terms/notices in the
installed files, verify their payload mapping, and resolve compatibility before
publication. Acquisition of licenses alone does not close that gate.

## Byte-level payload mapping follow-up

Audit-RuntimePayload.py now maps actual payload DLL/EXE SHA-256 values to restored
NuGet package files, including files inside the x64 runtime framework MSIX. It
also reads framework downloadDependencies: these targeting/runtime packs are
missing from the usual libraries inventory. The existing private payload has
277 PE files: 272 match exact restored package bytes, three are original project
build outputs and two are separately audited native DLLs. Zero remain unmapped.
RUNTIME-PROVENANCE-BASELINE.json preserves this pre-rebuild baseline, with no
absolute development paths. Re-run the tool for any replacement release payload;
the baseline does not certify a future package.

The added targeting pack is Microsoft.Windows.SDK.NET.Ref 10.0.26100.57, providing
Microsoft.Windows.SDK.NET.dll and WinRT.Runtime.dll. Its nuspec points to
https://aka.ms/WinSDKLicenseURL, which resolved to the Microsoft-hosted SDK RTF
license now preserved in third-party/Windows-SDK-license.rtf with URL/hash in the
notice inventory. This SDK license has REDIST scope and excluded-license
restrictions; retaining the text does not decide their compatibility or scope.

The GNU Windows runtime FAQ distinguishes permission to link system runtimes
from bundling their DLLs. Consequently this project will not treat app-local
Microsoft binary redistribution permission as automatic GPL compatibility:
https://www.gnu.org/licenses/gpl-faq.en.html#WindowsRuntimeAndGPL
A framework-dependent installation through Microsoft's supported runtime
installer is a technical alternative, not an automatic legal determination
that every WinUI component qualifies for GPL's system-library exception.

Microsoft's authoritative Windows SDK REDIST list explicitly includes both
Microsoft.Windows.SDK.NET.dll and WinRT.Runtime.dll in its .NET targeting-pack
paths. This closes the question of whether these two names are redistributable
under the SDK agreement, subject to that agreement's requirements and restrictions:
https://learn.microsoft.com/en-us/legal/windows-sdk/redist
This name-level permission is distinct from GPL compatibility and does not
remove the need for a supported licensing arrangement for the combined installer.

A framework-dependent WinUI packaging probe built with zero errors/warnings via
WindowsAppSDKSelfContained=false while leaving .NET self-contained. It omits the
large native app-local WinUI/AI/ML payload but still includes managed projections,
C#/WinRT and bootstrapper files. This probe is not the release configuration and
is not a claim that framework-dependent deployment alone resolves licensing.

The owner has now approved ADDITIONAL-PERMISSION.md for original EffortLess code.
This supplies an explicit application-level Windows-runtime linking/conveyance
permission rather than assuming a system-library exception. The corresponding
installer may not use unexcepted third-party GPL-only native dependencies; the
authorized next configuration is LGPL-compatible and must be verified as built.
Microsoft's redistribution conditions and recipient terms still apply separately.

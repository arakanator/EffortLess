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

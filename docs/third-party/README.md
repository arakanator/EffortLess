# Supplemental runtime notices for the local installer

Existing NOTICES.md, native Copyright.txt and LICENSE.GPL.txt are preserved in the
payload. These additional upstream texts do not establish complete redistribution
compliance for the GPL-enabled native artifact.

Retrieved for the personal-use installer:

- .NET runtime 10.0.13 LICENSE.TXT and THIRD-PARTY-NOTICES.TXT:
  https://github.com/dotnet/runtime/tree/v10.0.13
- Windows App SDK MIT license (v1.8.1 source tag; pinned NuGet runtime
  Microsoft.WindowsAppSDK 1.8.260921001):
  https://github.com/microsoft/WindowsAppSDK/blob/v1.8.1/LICENSE
- Vulkan Loader upstream license for v1.4.341:
  https://github.com/KhronosGroup/Vulkan-Loader/blob/v1.4.341/LICENSE.txt

The official signed LunarG x64 Vulkan DLL reports 1.4.341.0. LOCAL-NATIVE-DEPENDENCIES.json
records its upstream archive URL/checksum, DLL hash and signer. The runtime archive license is included as native/win-x64/VulkanRT-License.txt. Public-release compliance is still unapproved.
Before sharing, revisit application license, exact native transitive sources,
patches/configuration, dependency notices and Vulkan binary provenance. This folder
is a supplemental notice collection, not a claim that those gates are complete.


October 24, 2026 correction: .NET notices were retrieved from the exact v10.0.13
tag matching includedFrameworks in EffortLess.runtimeconfig.json.
RUNTIME-NOTICES.json records their upstream URLs and hashes; installer packaging
checks the runtime version and hashes to prevent silently stale notices. Existing
installers are not changed in place.

nuget/ contains the license/NOTICE texts from the exact restored application
package graph, including Microsoft binary license terms, WinUI/Windows ML notices
and WebView2 terms. NUGET-NOTICES.json identifies each package, relative file and
hash. The WindowsAppSDK-LICENSE.txt source MIT text is supplemental and does not
replace those binary terms. See ../WINDOWS-APP-SDK-REDISTRIBUTION.md.

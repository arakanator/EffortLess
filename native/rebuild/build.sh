#!/usr/bin/env bash
set -euo pipefail
# Run in an MSYS2 UCRT64 shell. Never acquires a prebuilt libmpv/FFmpeg.
test "${MSYSTEM:-}" = UCRT64 || { echo 'UCRT64 shell required' >&2; exit 1; }
repo="$(pwd)"
work="$repo/.cache/native-rebuild"
prefix="$work/prefix"
mpv_revision=41f6a645068483470267271e1d09966ca3b9f413
ffmpeg_revision=8ae0b34901ba60a802f183ee75a250a9fc3e09a5
export SOURCE_DATE_EPOCH=1762473600
mkdir -p "$work" "$prefix" "$work/records"
exec > >(tee "$work/records/build.log") 2>&1
export PATH="$prefix/bin:$MINGW_PREFIX/bin:/usr/bin:$PATH"
export PKG_CONFIG_PATH="$prefix/lib/pkgconfig:$MINGW_PREFIX/lib/pkgconfig"
export LIBRARY_PATH="$prefix/lib:$MINGW_PREFIX/lib"
export C_INCLUDE_PATH="$prefix/include:$MINGW_PREFIX/include"
export CPLUS_INCLUDE_PATH="$C_INCLUDE_PATH"

source_checkout() {
    local name="$1" url="$2" revision="$3"
    test ! -e "$work/$name" || { echo "Fresh work directory required: $name"; exit 1; }
    git init "$work/$name"
    git -C "$work/$name" remote add origin "$url"
    git -C "$work/$name" fetch --depth=1 origin "$revision"
    git -C "$work/$name" checkout --detach FETCH_HEAD
    test "$(git -C "$work/$name" rev-parse HEAD)" = "$revision"
    git -C "$work/$name" archive --format=tar.gz --prefix="$name/" HEAD > "$work/records/$name-source.tar.gz"
    printf '%s %s %s\n' "$name" "$url" "$revision" >> "$work/records/revisions.txt"
}
source_checkout ffmpeg https://github.com/FFmpeg/FFmpeg.git "$ffmpeg_revision"
source_checkout mpv https://github.com/mpv-player/mpv.git "$mpv_revision"
pacman -Q > "$work/records/installed-packages.txt"
gcc --version > "$work/records/compiler.txt"
ld --version >> "$work/records/compiler.txt"
meson --version > "$work/records/meson-version.txt"
uname -a > "$work/records/platform.txt"

cd "$work/ffmpeg"
# All native demuxers/decoders/encoders remain enabled. x264/x265 encoders and
# other GPL/nonfree optional integrations are deliberately not autodetected.
ffmpeg_options=(
    --prefix="$prefix" --target-os=mingw32 --arch=x86_64
    --enable-shared --disable-static --disable-doc --disable-debug
    --disable-gpl --disable-nonfree --enable-version3 --disable-autodetect
    --enable-libdav1d --enable-libopus --enable-libvpx --enable-libvorbis
    --enable-zlib --enable-iconv
    --enable-schannel --enable-d3d11va --enable-dxva2 --disable-ffplay
    "--extra-cflags=-O2 -ffile-prefix-map=$work=."
    --extra-ldflags=-Wl,--no-insert-timestamp
)
printf '%q ' ./configure "${ffmpeg_options[@]}" > "$work/records/ffmpeg-options.sh"
./configure "${ffmpeg_options[@]}"
grep -qx '#define CONFIG_GPL 0' config.h
grep -qx '#define CONFIG_NONFREE 0' config.h
grep -qx '#define CONFIG_VERSION3 1' config.h
grep -qx '#define CONFIG_PNG_ENCODER 1' config_components.h
grep -qx '#define CONFIG_H264_DECODER 1' config_components.h
grep -qx '#define CONFIG_HEVC_DECODER 1' config_components.h
make -j"$(nproc)"
make install
cp config.h ffbuild/config.mak ffbuild/config.log "$work/records/"
"$prefix/bin/ffmpeg.exe" -L > "$work/records/ffmpeg-license.txt" 2>&1
grep -q 'Lesser General Public' "$work/records/ffmpeg-license.txt"
"$prefix/bin/ffmpeg.exe" -hide_banner -f lavfi -i color=size=32x32:duration=0.1 -frames:v 1 "$work/records/png-codec-probe.png"

cd "$work/mpv"
mpv_options=(
    --prefix="$prefix" --buildtype=release --wrap-mode=nodownload
    -Dgpl=false -Dlibmpv=true -Dcplayer=true -Dbuild-date=false
    -Dtests=true -Dd3d11=enabled -Dwasapi=enabled
    -Ddirect3d=disabled -Dlibarchive=disabled -Dlibbluray=disabled
    -Ddvdnav=disabled -Dcdda=disabled -Ddvbin=disabled
    -Dlua=disabled -Djavascript=disabled -Drubberband=disabled
    -Dvapoursynth=disabled -Dlibavdevice=disabled
    -Duchardet=enabled -Dlcms2=enabled
)
printf '%q ' meson setup build "${mpv_options[@]}" > "$work/records/mpv-options.sh"
meson setup build "${mpv_options[@]}"
meson compile -C build
meson test -C build --print-errorlogs
meson install -C build
cp build/meson-info/intro-buildoptions.json build/meson-logs/meson-log.txt "$work/records/"
"$prefix/bin/mpv.exe" --no-config --version > "$work/records/mpv-version.txt" 2>&1
"$prefix/bin/mpv.exe" --no-config --vo=help > "$work/records/video-outputs.txt" 2>&1
grep -qi gpu-next "$work/records/video-outputs.txt"
"$prefix/bin/mpv.exe" --no-config --gpu-context=help > "$work/records/video-contexts.txt" 2>&1
grep -qi d3d11 "$work/records/video-contexts.txt"
"$prefix/bin/mpv.exe" --no-config --ao=help > "$work/records/audio-outputs.txt" 2>&1
grep -qi wasapi "$work/records/audio-outputs.txt"
cd "$repo"
python native/rebuild/collect.py --prefix "$prefix" --mpv-source "$work/mpv" --ffmpeg-source "$work/ffmpeg" --output "$work/result"
cp -r "$work/records" "$work/result/records"
cp -r native/rebuild "$work/result/build-scripts"
# The collector must finish its source/license closure before any DLL artifact.
test -f "$work/result/manifest.json"
cd "$work/result"
find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS
echo 'Native build and source collection completed; parent playback acceptance still required.'

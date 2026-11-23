#!/usr/bin/env bash
# Compila la biblioteca nativa de naga para el RID indicado (por defecto, el del equipo) y la deja en
# runtimes/<rid>/native/. Uso:  build.sh [rid] [target-triple-de-rust]
set -euo pipefail
cd "$(dirname "$0")"
os="$(uname -s)"; arch="$(uname -m)"
case "$os-$arch" in
  Darwin-arm64) rid=osx-arm64; lib=libyotsuba_naga.dylib ;;
  Darwin-x86_64) rid=osx-x64; lib=libyotsuba_naga.dylib ;;
  Linux-aarch64) rid=linux-arm64; lib=libyotsuba_naga.so ;;
  Linux-x86_64) rid=linux-x64; lib=libyotsuba_naga.so ;;
  *) rid=win-x64; lib=yotsuba_naga.dll ;;
esac
rid="${1:-$rid}"
case "$rid" in win-*) lib=yotsuba_naga.dll ;; osx-*) lib=libyotsuba_naga.dylib ;; linux-*) lib=libyotsuba_naga.so ;; esac
if [ -n "${2:-}" ]; then
  cargo build --release --target "$2"; src="target/$2/release/$lib"
else
  cargo build --release; src="target/release/$lib"
fi
mkdir -p "runtimes/$rid/native"
cp "$src" "runtimes/$rid/native/$lib"
echo "naga -> runtimes/$rid/native/$lib"

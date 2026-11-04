# 0009. An English and a Polish version of every download
Date: 2026-10-27 · Status: Accepted (Mac's request, 2026-10-26)

## Context
Mac wants every download in two versions, one with everything in Polish by default and one with
everything in English: the installer, the portable build, the README and the licence agreement.
The coming website offers the download that matches the language picked on the site, so the file
names must name the language. Until now one installer asked for the language (#53) and the
portable build always started in English.

## Decision
- `scripts/build-release.ps1` makes both versions in one run, so every tagged release has
  `Somnus-Setup-<version>-en.exe`, `Somnus-Setup-<version>-pl.exe`,
  `Somnus-<version>-win-x64-portable-en.zip` and `Somnus-<version>-win-x64-portable-pl.zip`, next
  to the shared `THIRD_PARTY_LICENSES.txt` and `SHA256SUMS.txt`.
- `installer/Somnus.iss` is compiled once per language (`/DLang=en` or `/DLang=pl`) with only that
  language, so Setup never asks for one and shows the licence in it.
- `Somnus.exe` is the same in both versions and keeps both languages. A version only decides the
  language of the first start: the installer writes `Somnus.ini` (#53), and the portable zip
  carries the same file.
- `README.txt` is a short user guide per language (`installer/README.en.txt`,
  `installer/README.pl.txt`); the repository READMEs stay the documents for developers.
- The MIT licence has no official Polish text. The Polish `LICENSE.txt` is an informational
  translation (`installer/LICENSE.pl.txt`) followed by the binding English original, which the
  licence requires in every copy anyway. `THIRD_PARTY_LICENSES.txt` stays in English, since it
  holds the third parties' own texts.

## Consequences
- Twice as many files per release and a longer release build (two installer compilations).
- Picking the other language's download costs nothing: the settings switch the language at any
  time.
- Both installers share the AppId, so one installs over the other as an upgrade, and Somnus keeps
  a language already saved in its settings.
- The two package READMEs must follow feature changes, like the repository READMEs.

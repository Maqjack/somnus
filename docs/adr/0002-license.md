# 0002. License: MIT (SWPA variant A)
Date: 2026-10-22 · Status: Accepted

## Context
Mac wants Somnus as a portfolio piece. SWPA 3.13 requires choosing a license variant in stage 1.

## Decision
- Variant A: open source under the MIT License, `LICENSE` in the repo root, shown in the installer.
- Repo stays private until SWPA stage 7; Mac alone decides to publish (repo public + GitHub Release).
- Dependencies limited to the SWPA 3.5 allow-list; `THIRD_PARTY_LICENSES.txt` generated in CI and shipped with every release.

## Consequences
- Anyone may reuse, modify and sell the code once published; this cannot be undone for published versions.
- No EULA, CLA or license keys are needed.
- Enables the free open-source code-signing route later (ADR 0004).
